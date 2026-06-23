using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Matching.API.Hubs;
using PlatformA.Matching.API.Models;
using PlatformA.MySqlDB.Lib.DBWebApp;
using PlatformA.MySqlDB.Lib.DBWebApp.Entities;
using Polly.CircuitBreaker;
using StackExchange.Redis;

namespace PlatformA.Matching.API.Services
{
    /// <summary>
    /// 게임서버용 매칭 엔진.
    /// 모든 Redis 명령은 RedisManager.ExecuteAsync를 통해 Polly 파이프라인이 적용됩니다.
    /// </summary>
    public class GameMatchService : BackgroundService
    {
        private readonly IHubContext<MatchingHub> _hubContext;
        private readonly RedisManager _redisManager;
        private readonly ILogger<GameMatchService> _logger;
        private readonly IDbContextFactory<DbWebAppContext> _dbFactory;

        // Lua: 타임아웃된 유저를 원자적으로 제거하고 반환
        private const string TIMEOUT_CLEANUP_SCRIPT = @"
local timedOut = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
if #timedOut > 0 then
    redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
end
return timedOut";

        // Lua: 가장 오래 기다린 유저 2명을 원자적으로 pop
        // ZPOPMIN은 [member, score, member, score, ...] 형식으로 반환
        private const string POP_TWO_SCRIPT = @"
local members = redis.call('ZPOPMIN', KEYS[1], 2)
if #members < 4 then
    if #members == 2 then
        redis.call('ZADD', KEYS[1], members[2], members[1])
    end
    return {}
end
return {members[1], members[3]}";

        public GameMatchService(
            IHubContext<MatchingHub> hubContext,
            RedisManager redisManager,
            ILogger<GameMatchService> logger,
            IDbContextFactory<DbWebAppContext> dbFactory)
        {
            _hubContext = hubContext;
            _redisManager = redisManager;
            _logger = logger;
            _dbFactory = dbFactory;
        }

        /// <summary>매칭 큐에 유저를 추가합니다. (Sorted Set, score = 입장 시각 UnixMs)</summary>
        public async Task AddPlayerToQueueAsync(int userId)
        {
            _logger.LogInformation("[Matching] 유저 큐 진입 — UserId: {UserId}", userId);
            double score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _redisManager.ExecuteAsync(db =>
                db.SortedSetAddAsync(Consts.MATCH_QUEUE_KEY, userId, score));
            _logger.LogInformation("[Matching] Redis 대기열 진입 완료 — UserId: {UserId}", userId);
        }

        /// <summary>매칭 대기열에서 유저를 제거합니다. true이면 실제로 제거됨.</summary>
        public async Task<bool> RemovePlayerFromQueueAsync(int userId)
        {
            return await _redisManager.ExecuteAsync(db =>
                db.SortedSetRemoveAsync(Consts.MATCH_QUEUE_KEY, userId));
        }

        /// <summary>유저의 대기열 순위를 반환합니다. 없으면 -1.</summary>
        public async Task<long> GetQueueRankAsync(int userId)
        {
            long? rank = await _redisManager.ExecuteAsync(db =>
                db.SortedSetRankAsync(Consts.MATCH_QUEUE_KEY, userId));
            return rank ?? -1;
        }

        /// <summary>현재 대기열 총 인원을 반환합니다.</summary>
        public async Task<long> GetQueueLengthAsync()
        {
            return await _redisManager.ExecuteAsync(db =>
                db.SortedSetLengthAsync(Consts.MATCH_QUEUE_KEY));
        }

        /// <summary>플레이어 MMR을 Redis에서 조회합니다. 없으면 기본값(1000)을 반환합니다.</summary>
        public async Task<int> GetPlayerRatingAsync(int userId)
        {
            string ratingKey = $"{Consts.PLAYER_RATING_KEY_PREFIX}{userId}";
            string? val = await _redisManager.ExecuteAsync(db => db.StringGetAsync(ratingKey));
            return int.TryParse(val, out int rating) ? rating : Consts.DEFAULT_PLAYER_RATING;
        }

        /// <summary>
        /// Lobby 서버가 호출하는 즉시 매칭 시도 메서드.
        /// 같은 gameType의 대기열에 상대가 있으면 즉시 매칭하고 게임 서버 접속 정보를 반환합니다.
        /// 상대가 없으면 대기열에 추가 후 null을 반환합니다.
        /// </summary>
        public async Task<MatchResultDto?> TryMatchAsync(int userId, string gameType)
        {
            string queueKey = $"{Consts.MATCH_QUEUE_KEY}:{gameType}";
            double score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // 원자적 pop — 대기 중인 상대가 있으면 즉시 매칭
            var rawResult = await _redisManager.ExecuteAsync(db =>
                db.ScriptEvaluateAsync(
                    POP_TWO_SCRIPT,
                    new RedisKey[] { queueKey },
                    Array.Empty<RedisValue>()));
            var popResult = (RedisValue[]?)rawResult ?? Array.Empty<RedisValue>();

            if (popResult.Length >= 1 && int.TryParse((string?)popResult[0], out int opponentId) && opponentId != userId)
            {
                // 상대 발견 — 즉시 매칭
                string roomId = Guid.NewGuid().ToString("N")[..12];
                string host = GetGameServerHost(gameType);
                int port = GetGameServerPort(gameType);
                int p1Rating = await GetPlayerRatingAsync(userId);
                int p2Rating = await GetPlayerRatingAsync(opponentId);

                await RecordMatchStartAsync(userId, opponentId, gameType, roomId, p1Rating, p2Rating);

                // 두 플레이어 모두 game_transfer 티켓 발급
                string matchJson = System.Text.Json.JsonSerializer.Serialize(
                    new { roomId, host, port, gameType });

                await _redisManager.ExecuteAsync(db =>
                    db.StringSetAsync(
                        $"{Consts.GAME_TRANSFER_KEY_PREFIX}{opponentId}",
                        matchJson,
                        TimeSpan.FromMinutes(5)));

                await _redisManager.ExecuteAsync(db =>
                    db.StringSetAsync(
                        $"{Consts.GAME_TRANSFER_KEY_PREFIX}{userId}",
                        matchJson,
                        TimeSpan.FromMinutes(5)));

                // 두 플레이어에게 Redis Pub/Sub으로 매칭 알림 (Game.Lobby SignalR push용)
                string notifyOpponent = System.Text.Json.JsonSerializer.Serialize(
                    new { userId = opponentId, host, port, roomId, gameType });
                string notifySelf = System.Text.Json.JsonSerializer.Serialize(
                    new { userId, host, port, roomId, gameType });

                await _redisManager.GetSubscriber().PublishAsync(
                    RedisChannel.Literal(Consts.MATCH_FOUND_CHANNEL), notifyOpponent);
                await _redisManager.GetSubscriber().PublishAsync(
                    RedisChannel.Literal(Consts.MATCH_FOUND_CHANNEL), notifySelf);

                _logger.LogInformation("[Matching] 즉시 매칭 성사 — User:{U} vs User:{O} room:{R} type:{T}",
                    userId, opponentId, roomId, gameType);

                return new MatchResultDto { Host = host, Port = port, RoomId = roomId };
            }

            // 상대 없음 — 대기열에 추가
            await _redisManager.ExecuteAsync(db =>
                db.SortedSetAddAsync(queueKey, userId, score));
            _logger.LogInformation("[Matching] 대기열 진입 — User:{U} gameType:{T}", userId, gameType);
            return null;
        }

        /// <summary>플레이어의 최근 매칭 이력을 반환합니다.</summary>
        public async Task<List<MatchHistoryDto>> GetMatchHistoryAsync(int userId, int limit = 20)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.MatchRecords
                .Where(m => m.Player1Id == userId || m.Player2Id == userId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(limit)
                .Select(m => new MatchHistoryDto
                {
                    MatchId = m.Id,
                    GameType = m.GameType,
                    OpponentId = m.Player1Id == userId ? m.Player2Id : m.Player1Id,
                    Result = m.WinnerId == null ? "미완료"
                                 : m.WinnerId == userId ? "승리" : "패배",
                    MatchedAt = m.CreatedAt,
                })
                .ToListAsync();
        }

        private static string GetGameServerHost(string gameType) => gameType switch
        {
            "gomoku" => Consts.GOMOKU_SERVER_IP,
            _ => Consts.GAME_SERVER_IP,
        };

        private static int GetGameServerPort(string gameType) => gameType switch
        {
            "gomoku" => Consts.GOMOKU_SERVER_PORT,
            _ => Consts.GAME_SERVER_PORT,
        };

        /// <summary>백그라운드 매칭 워커</summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessQueueAsync();
                    await Task.Delay(200, stoppingToken);
                }
                catch (BrokenCircuitException)
                {
                    _logger.LogWarning("[Matching] 회로차단기 OPEN — Redis 회복 대기 중 (5초 후 재시도)");
                    await Task.Delay(5000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Matching] 매칭 처리 중 예외 발생");
                    await Task.Delay(2000, stoppingToken);
                }
            }
        }

        private async Task ProcessQueueAsync()
        {
            // 1. 타임아웃 유저 정리 (MATCH_TIMEOUT_SECONDS 초 초과 대기 유저)
            double cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                - (Consts.MATCH_TIMEOUT_SECONDS * 1000L);

            var timeoutResult = (RedisValue[])await _redisManager.ExecuteAsync(db =>
                db.ScriptEvaluateAsync(
                    TIMEOUT_CLEANUP_SCRIPT,
                    new RedisKey[] { Consts.MATCH_QUEUE_KEY },
                    new RedisValue[] { cutoff }));

            foreach (var val in timeoutResult)
            {
                if (int.TryParse((string?)val, out int timedOutId))
                {
                    _logger.LogInformation("[Matching] 매칭 타임아웃 — UserId: {UserId}", timedOutId);
                    await _hubContext.Clients
                        .Group($"User_{timedOutId}")
                        .SendAsync("MatchTimeout", new { Message = "매칭 시간이 초과되었습니다." });
                }
            }

            // 2. 유저 2명 원자 pop (Lua로 race condition 방지)
            var popResult = (RedisValue[])await _redisManager.ExecuteAsync(db =>
                db.ScriptEvaluateAsync(
                    POP_TWO_SCRIPT,
                    new RedisKey[] { Consts.MATCH_QUEUE_KEY }));

            if (popResult.Length < 2)
                return;

            if (int.TryParse((string?)popResult[0], out int player1Id) &&
                int.TryParse((string?)popResult[1], out int player2Id))
            {
                _ = ProcessMatchingAsync(player1Id, player2Id);
            }
        }

        private async Task ProcessMatchingAsync(int player1Id, int player2Id)
        {
            int newRoomId = (int)await _redisManager.ExecuteAsync(
                db => db.StringIncrementAsync("global:room_id"));

            // room_id 1번은 서버 시작 시 생성된 광장(로비)이므로 건너뜁니다.
            if (newRoomId == 1)
                newRoomId = (int)await _redisManager.ExecuteAsync(
                    db => db.StringIncrementAsync("global:room_id"));

            _logger.LogInformation("[Matching] 매칭 성사 — 방: {RoomId}, Player1: {P1}, Player2: {P2}",
                newRoomId, player1Id, player2Id);

            await RecordMatchStartAsync(player1Id, player2Id);

            var matchEvent = new MatchSuccessEvent
            {
                RoomId = newRoomId,
                MatchedUserIds = new List<int> { player1Id, player2Id }
            };

            string jsonMessage = JsonSerializer.Serialize(matchEvent);

            await _redisManager.GetSubscriber().PublishAsync(
                RedisChannel.Literal("channel:match_success"), jsonMessage);

            await _hubContext.Clients.Group($"User_{player1Id}").SendAsync("MatchFound", matchEvent);
            await _hubContext.Clients.Group($"User_{player2Id}").SendAsync("MatchFound", matchEvent);
        }

        private async Task RecordMatchStartAsync(
            int player1Id, int player2Id,
            string gameType = "", string roomId = "",
            int player1Rating = Consts.DEFAULT_PLAYER_RATING,
            int player2Rating = Consts.DEFAULT_PLAYER_RATING)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                var record = new MatchRecord
                {
                    Player1Id = player1Id,
                    Player2Id = player2Id,
                    Status = MatchStatus.InProgress,
                    GameType = gameType,
                    RoomId = roomId,
                    Player1Rating = player1Rating,
                    Player2Rating = player2Rating,
                    StartedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
                db.MatchRecords.Add(record);
                await db.SaveChangesAsync();

                _logger.LogInformation(
                    "[Matching] MatchRecord 생성 완료 — RecordId: {Id}, P1: {P1}, P2: {P2}",
                    record.Id, player1Id, player2Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[Matching] MatchRecord 저장 실패 — P1: {P1}, P2: {P2} (매칭 흐름 계속)",
                    player1Id, player2Id);
            }
        }
    }
}
