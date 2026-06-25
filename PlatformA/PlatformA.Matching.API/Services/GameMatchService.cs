using Microsoft.EntityFrameworkCore;
using PlatformA.Library.Common;
using PlatformA.Library.Core;
using PlatformA.Matching.API.Models;
using PlatformA.MySqlDB.Lib.DBWebApp;
using PlatformA.MySqlDB.Lib.DBWebApp.Entities;
using StackExchange.Redis;

namespace PlatformA.Matching.API.Services
{
    /// <summary>
    /// 게임서버용 매칭 엔진.
    /// 모든 Redis 명령은 RedisManager.ExecuteAsync를 통해 Polly 파이프라인이 적용됩니다.
    /// </summary>
    public class GameMatchService : BackgroundService
    {
        private readonly RedisManager _redisManager;
        private readonly ILogger<GameMatchService> _logger;
        private readonly IDbContextFactory<DbWebAppContext> _dbFactory;

        // Lua: 원자적 pop-or-enqueue 매칭 스크립트
        // ARGV[1]=score(timestamp), ARGV[2]=userId
        // 반환값: 상대 userId 문자열 (매칭 성사) 또는 {} (큐 대기)
        // POP_TWO_SCRIPT 방식(2명 pop 후 C# ZADD)은 동시 요청 시 레이스 조건으로
        // 모든 유저가 빈 큐를 보고 큐에만 쌓혀 매칭이 발생하지 않는 버그가 있었음.
        private const string MATCH_OR_QUEUE_SCRIPT = @"
local candidate = redis.call('ZPOPMIN', KEYS[1], 1)
if #candidate >= 2 then
    if candidate[1] == ARGV[2] then
        redis.call('ZADD', KEYS[1], candidate[2], candidate[1])
        return {}
    end
    return {candidate[1]}
end
redis.call('ZADD', KEYS[1], ARGV[1], ARGV[2])
return {}";

        public GameMatchService(
            RedisManager redisManager,
            ILogger<GameMatchService> logger,
            IDbContextFactory<DbWebAppContext> dbFactory)
        {
            _redisManager = redisManager;
            _logger = logger;
            _dbFactory = dbFactory;
        }

        /// <summary>
        /// [Deprecated] 구 단일 큐에 유저를 추가합니다. GameMatchController.RequestMatch에서 호출.
        /// 현재 매칭 경로는 TryMatchAsync(gameType)를 통한 gameType별 큐입니다.
        /// </summary>
        public async Task AddPlayerToQueueAsync(int userId)
        {
            _logger.LogInformation("[Matching] 유저 큐 진입 — UserId: {UserId}", userId);
            double score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await _redisManager.ExecuteAsync(db =>
                db.SortedSetAddAsync(Consts.MATCH_QUEUE_KEY, userId, score));
            _logger.LogInformation("[Matching] Redis 대기열 진입 완료 — UserId: {UserId}", userId);
        }

        /// <summary>[Deprecated] 구 단일 큐에서 유저를 제거합니다. true이면 실제로 제거됨.</summary>
        public async Task<bool> RemovePlayerFromQueueAsync(int userId)
        {
            return await _redisManager.ExecuteAsync(db =>
                db.SortedSetRemoveAsync(Consts.MATCH_QUEUE_KEY, userId));
        }

        /// <summary>[Deprecated] 구 단일 큐에서 유저의 대기열 순위를 반환합니다. 없으면 -1.</summary>
        public async Task<long> GetQueueRankAsync(int userId)
        {
            long? rank = await _redisManager.ExecuteAsync(db =>
                db.SortedSetRankAsync(Consts.MATCH_QUEUE_KEY, userId));
            return rank ?? -1;
        }

        /// <summary>[Deprecated] 구 단일 큐의 현재 대기 총 인원을 반환합니다.</summary>
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

            // 원자적 pop-or-enqueue: 상대가 있으면 즉시 매칭, 없으면 큐에 추가
            // 스크립트 내부에서 ZADD까지 처리하므로 C# ZADD 분리 없음 (레이스 조건 해결)
            var rawResult = await _redisManager.ExecuteAsync(db =>
                db.ScriptEvaluateAsync(
                    MATCH_OR_QUEUE_SCRIPT,
                    new RedisKey[] { queueKey },
                    new RedisValue[] { score, userId }));
            var popResult = (RedisValue[]?)rawResult ?? Array.Empty<RedisValue>();

            if (popResult.Length >= 1 && int.TryParse((string?)popResult[0], out int opponentId))
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

                try
                {
                    await _redisManager.GetSubscriber().PublishAsync(
                        RedisChannel.Literal(Consts.MATCH_FOUND_CHANNEL), notifyOpponent);
                    await _redisManager.GetSubscriber().PublishAsync(
                        RedisChannel.Literal(Consts.MATCH_FOUND_CHANNEL), notifySelf);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "[Matching] MatchFound publish 실패 — User:{UserId}, Opponent:{OpponentId}, Room:{RoomId}, GameType:{GameType}",
                        userId, opponentId, roomId, gameType);
                }

                _logger.LogInformation("[Matching] 즉시 매칭 성사 — User:{U} vs User:{O} room:{R} type:{T}",
                    userId, opponentId, roomId, gameType);

                return new MatchResultDto { Host = host, Port = port, RoomId = roomId };
            }

            // 상대 없음 — 스크립트가 이미 큐에 추가함
            _logger.LogInformation("[Matching] 대기열 진입 — User:{U} gameType:{T}", userId, gameType);
            return null;
        }

        /// <summary>게임 종료 후 MatchRecord에 결과를 업데이트합니다.</summary>
        public async Task<bool> UpdateMatchResultAsync(string roomId, int winnerId, string reason)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                MatchRecord? record = await db.MatchRecords
                    .FirstOrDefaultAsync(m => m.RoomId == roomId
                        && m.Status != MatchStatus.Completed
                        && m.Status != MatchStatus.Cancelled);

                if (record == null)
                {
                    _logger.LogWarning("[Matching] MatchRecord 없음 또는 이미 종료 — RoomId: {RoomId}", roomId);
                    return false;
                }

                record.WinnerId = winnerId == 0 ? null : winnerId;
                record.Status = MatchStatus.Completed;
                record.EndedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                _logger.LogInformation(
                    "[Matching] MatchRecord 결과 업데이트 — RoomId: {RoomId}, WinnerId: {WinnerId}, Reason: {Reason}",
                    roomId, winnerId, reason);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Matching] MatchRecord 결과 업데이트 실패 — RoomId: {RoomId}", roomId);
                return false;
            }
        }

        /// <summary>게임 서버에서 두 플레이어가 입장했음을 알리고 Status를 InProgress로 전환합니다.</summary>
        public async Task<bool> NotifyMatchStartedAsync(string roomId)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                MatchRecord? record = await db.MatchRecords
                    .FirstOrDefaultAsync(m => m.RoomId == roomId && m.Status == MatchStatus.Pending);

                if (record == null)
                {
                    _logger.LogWarning("[Matching] NotifyMatchStarted — Pending 레코드 없음 RoomId: {RoomId}", roomId);
                    return false;
                }

                record.Status = MatchStatus.InProgress;
                record.StartedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                _logger.LogInformation("[Matching] 게임 시작 확인 — RoomId: {RoomId}", roomId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Matching] NotifyMatchStarted 실패 — RoomId: {RoomId}", roomId);
                return false;
            }
        }

        /// <summary>10분 이상 Pending 상태인 레코드를 Cancelled로 정리합니다.</summary>
        private async Task AbandonStaleMatchesAsync()
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                DateTime cutoff = DateTime.UtcNow.AddMinutes(-10);
                List<MatchRecord> stale = await db.MatchRecords
                    .Where(m => m.Status == MatchStatus.Pending && m.CreatedAt < cutoff)
                    .ToListAsync();

                if (stale.Count == 0)
                    return;

                foreach (var record in stale)
                    record.Status = MatchStatus.Cancelled;

                await db.SaveChangesAsync();
                _logger.LogInformation("[Matching] Stale Pending 정리 — {Count}건 Cancelled 처리", stale.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Matching] AbandonStaleMatches 실패");
            }
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
                    Result = m.Status != MatchStatus.Completed ? "미완료"
                                 : m.WinnerId == null ? "무승부"
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

        /// <summary>백그라운드 워커 — stale Pending 레코드 정리 전용.</summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int tickCount = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 5분(1500 × 200ms)마다 stale Pending 레코드 정리
                    tickCount++;
                    if (tickCount >= 1500)
                    {
                        tickCount = 0;
                        _ = AbandonStaleMatchesAsync();
                    }

                    await Task.Delay(200, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Matching] 백그라운드 정리 작업 중 예외");
                    await Task.Delay(2000, stoppingToken);
                }
            }
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
                    Status = MatchStatus.Pending,
                    GameType = gameType,
                    RoomId = roomId,
                    Player1Rating = player1Rating,
                    Player2Rating = player2Rating,
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
