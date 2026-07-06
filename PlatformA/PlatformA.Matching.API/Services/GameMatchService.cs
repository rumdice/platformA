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

        // KEYS[1]=queueKey, ARGV[1]=userId, ARGV[2]=minScore, ARGV[3]=maxScore
        // 범위 내 자신을 제외한 첫 번째 상대를 원자적으로 pop.
        private const string MATCH_STRICT_SCRIPT = @"
local candidates = redis.call('ZRANGEBYSCORE', KEYS[1], ARGV[2], ARGV[3])
for i = 1, #candidates do
    if candidates[i] ~= ARGV[1] then
        redis.call('ZREM', KEYS[1], candidates[i])
        return {candidates[i]}
    end
end
return {}";

        // KEYS[1]=queueKey, ARGV[1]=userId
        // ZPOPMIN fallback: 자신이 pop되면 원복.
        private const string MATCH_FALLBACK_SCRIPT = @"
local candidate = redis.call('ZPOPMIN', KEYS[1], 1)
if #candidate >= 2 and candidate[1] ~= ARGV[1] then
    return {candidate[1]}
end
if #candidate >= 2 then
    redis.call('ZADD', KEYS[1], candidate[2], candidate[1])
end
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

        /// <summary>플레이어 MMR을 조회합니다. Redis hit → 즉시 반환. Redis miss → DB fallback 후 재캐싱. DB miss → 기본값(1000).</summary>
        public async Task<int> GetPlayerRatingAsync(int userId)
        {
            string ratingKey = $"{Consts.PLAYER_RATING_KEY_PREFIX}{userId}";
            string? val = await _redisManager.ExecuteAsync(db => db.StringGetAsync(ratingKey));
            if (int.TryParse(val, out int cachedRating))
                return cachedRating;

            await using var dbContext = await _dbFactory.CreateDbContextAsync();
            PlayerRating? playerRating = await dbContext.PlayerRatings.FindAsync(userId);
            if (playerRating == null)
                return Consts.DEFAULT_PLAYER_RATING;

            int rating = (int)playerRating.Rating;
            await _redisManager.ExecuteAsync(db =>
                db.StringSetAsync(ratingKey, rating.ToString(), TimeSpan.FromHours(1)));
            return rating;
        }

        /// <summary>
        /// Lobby 서버가 호출하는 즉시 매칭 시도 메서드.
        /// ELO 레이팅 기반 3단계 범위 매칭: Stage1(±200) → Stage2/3(TTL 기반 ±400/±800) → Fallback(ZPOPMIN).
        /// 상대를 찾지 못하면 대기열에 추가 후 null을 반환합니다.
        /// </summary>
        public async Task<MatchResultDto?> TryMatchAsync(int userId, string gameType)
        {
            string queueKey = $"{Consts.MATCH_QUEUE_KEY}:{gameType}";
            string waitKey = $"{Consts.MATCH_WAIT_KEY_PREFIX}{gameType}:{userId}";
            int selfRating = await GetPlayerRatingAsync(userId);

            // Stage 1: 즉시 ±200 엄격 범위
            int? opponentId = await TryStrictMatchInternalAsync(queueKey, userId, selfRating, Consts.MATCH_RATING_RANGE);
            if (opponentId.HasValue)
                return await FinalizeMatchAsync(userId, opponentId.Value, gameType, selfRating);

            // Stage 2/3: 대기 시간 기반 확장 범위 (30s→±400, 60s→±800, 90s→ZPOPMIN)
            opponentId = await TryExpandedMatchAsync(queueKey, gameType, userId, selfRating);
            if (opponentId.HasValue)
                return await FinalizeMatchAsync(userId, opponentId.Value, gameType, selfRating);

            // 매칭 실패 → 대기열 진입
            await _redisManager.ExecuteAsync(db =>
                db.SortedSetAddAsync(queueKey, userId, selfRating));
            await _redisManager.ExecuteAsync(db =>
                db.StringSetAsync(waitKey, "1",
                    TimeSpan.FromSeconds(Consts.MATCH_TIMEOUT_SECONDS), When.NotExists));

            _logger.LogInformation("[Matching] 대기열 진입 — User:{U} Rating:{R} gameType:{T}",
                userId, selfRating, gameType);
            return null;
        }

        /// <summary>MATCH_STRICT_SCRIPT: ZRANGEBYSCORE 범위 내 원자적 매칭.</summary>
        private async Task<int?> TryStrictMatchInternalAsync(
            string queueKey, int userId, int selfRating, int range)
        {
            double minScore = selfRating - range;
            double maxScore = selfRating + range;

            var rawResult = await _redisManager.ExecuteAsync(db =>
                db.ScriptEvaluateAsync(
                    MATCH_STRICT_SCRIPT,
                    new RedisKey[] { queueKey },
                    new RedisValue[] { userId, minScore, maxScore }));

            var popResult = (RedisValue[]?)rawResult ?? Array.Empty<RedisValue>();
            if (popResult.Length >= 1 && int.TryParse((string?)popResult[0], out int opponentId))
                return opponentId;

            return null;
        }

        /// <summary>
        /// 큐 전체 순회 후 각 후보의 wait key TTL로 경과 시간을 계산하여 확장 범위 매칭 시도.
        /// 90s+ 후보가 있으면 ZPOPMIN fallback 사용.
        /// </summary>
        private async Task<int?> TryExpandedMatchAsync(
            string queueKey, string gameType, int userId, int selfRating)
        {
            var allMembers = await _redisManager.ExecuteAsync(db =>
                db.SortedSetRangeByRankWithScoresAsync(queueKey));

            if (allMembers == null || allMembers.Length == 0)
                return null;

            bool hasFallbackCandidate = false;

            foreach (var entry in allMembers)
            {
                if (!int.TryParse((string?)entry.Element, out int candidateId))
                    continue;
                if (candidateId == userId)
                    continue;

                string candidateWaitKey = $"{Consts.MATCH_WAIT_KEY_PREFIX}{gameType}:{candidateId}";
                TimeSpan? ttl = await _redisManager.ExecuteAsync(db =>
                    db.KeyTimeToLiveAsync(candidateWaitKey));

                if (ttl == null || ttl.Value.TotalSeconds <= 0)
                    continue; // wait key 만료 (자동 타임아웃)

                int elapsed = Consts.MATCH_TIMEOUT_SECONDS - (int)ttl.Value.TotalSeconds;

                if (elapsed >= 90)
                {
                    hasFallbackCandidate = true;
                    continue;
                }

                int range;
                if (elapsed >= 60)
                    range = Consts.MATCH_RATING_RANGE_WIDE; // ±800
                else if (elapsed >= 30)
                    range = Consts.MATCH_RATING_RANGE_MID;  // ±400
                else
                    continue; // Stage 1 범위 미확장

                if (Math.Abs(entry.Score - selfRating) <= range)
                {
                    bool removed = await _redisManager.ExecuteAsync(db =>
                        db.SortedSetRemoveAsync(queueKey, candidateId));
                    if (removed)
                        return candidateId;
                }
            }

            // 90s+ 후보가 있으면 ZPOPMIN fallback
            if (hasFallbackCandidate)
            {
                var rawResult = await _redisManager.ExecuteAsync(db =>
                    db.ScriptEvaluateAsync(
                        MATCH_FALLBACK_SCRIPT,
                        new RedisKey[] { queueKey },
                        new RedisValue[] { userId }));

                var popResult = (RedisValue[]?)rawResult ?? Array.Empty<RedisValue>();
                if (popResult.Length >= 1 && int.TryParse((string?)popResult[0], out int fallbackId))
                    return fallbackId;
            }

            return null;
        }

        /// <summary>
        /// 매칭 성사 후처리: wait key 정리, MatchRecord 생성, game_transfer 발급, SignalR 알림.
        /// </summary>
        private async Task<MatchResultDto?> FinalizeMatchAsync(
            int userId, int opponentId, string gameType, int selfRating)
        {
            // 상대의 wait key 삭제 (자동 만료 전 명시적 제거)
            string opponentWaitKey = $"{Consts.MATCH_WAIT_KEY_PREFIX}{gameType}:{opponentId}";
            await _redisManager.ExecuteAsync(db => db.KeyDeleteAsync(opponentWaitKey));

            string roomId = Guid.NewGuid().ToString("N")[..12];
            string host = GetGameServerHost(gameType);
            int port = GetGameServerPort(gameType);
            int opponentRating = await GetPlayerRatingAsync(opponentId);

            await RecordMatchStartAsync(userId, opponentId, gameType, roomId, selfRating, opponentRating);

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
                    "[Matching] MatchFound publish 실패 — User:{UserId}, Opponent:{OpponentId}, Room:{RoomId}",
                    userId, opponentId, roomId);
            }

            _logger.LogInformation(
                "[Matching] 매칭 성사 — User:{U}(R:{UR}) vs User:{O}(R:{OR}) room:{R} type:{T}",
                userId, selfRating, opponentId, opponentRating, roomId, gameType);

            return new MatchResultDto { Host = host, Port = port, RoomId = roomId };
        }

        /// <summary>gameType별 큐 + wait key에서 유저를 제거합니다. true이면 실제로 제거됨.</summary>
        public async Task<bool> CancelMatchAsync(int userId, string gameType)
        {
            string queueKey = $"{Consts.MATCH_QUEUE_KEY}:{gameType}";
            string waitKey = $"{Consts.MATCH_WAIT_KEY_PREFIX}{gameType}:{userId}";
            bool removed = await _redisManager.ExecuteAsync(db =>
                db.SortedSetRemoveAsync(queueKey, userId));
            await _redisManager.ExecuteAsync(db => db.KeyDeleteAsync(waitKey));
            return removed;
        }

        /// <summary>게임 종료 후 MatchRecord에 결과를 업데이트하고 ELO 레이팅을 갱신합니다.</summary>
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

                // 레이팅 차이에 따라 K-factor를 감소시켜 광범위 매칭 시 MMR 희석 방지
                int ratingDiff = Math.Abs(record.Player1Rating - record.Player2Rating);
                double kMultiplier = ratingDiff switch
                {
                    <= 200 => 1.0,
                    <= 400 => 0.5,
                    <= 800 => 0.25,
                    _ => 0.125
                };

                await UpdateEloRatingsAsync(record.Player1Id, record.Player2Id, winnerId, kMultiplier);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Matching] MatchRecord 결과 업데이트 실패 — RoomId: {RoomId}", roomId);
                return false;
            }
        }

        /// <summary>
        /// ELO 레이팅 계산 및 DB/Redis 갱신.
        /// K=32 (300전 미만), K=16 (300전 이상). winnerId=0은 무승부.
        /// kMultiplier: 광범위 매칭일수록 K를 줄여 MMR 희석 방지.
        /// </summary>
        private async Task UpdateEloRatingsAsync(
            int player1Id, int player2Id, int winnerId, double kMultiplier = 1.0)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();

                PlayerRating r1 = await db.PlayerRatings.FindAsync(player1Id)
                    ?? new PlayerRating { PlayerId = player1Id };
                PlayerRating r2 = await db.PlayerRatings.FindAsync(player2Id)
                    ?? new PlayerRating { PlayerId = player2Id };

                int totalGames1 = r1.WinCount + r1.LoseCount + r1.DrawCount;
                int totalGames2 = r2.WinCount + r2.LoseCount + r2.DrawCount;
                double k1 = (totalGames1 < 300 ? 32.0 : 16.0) * kMultiplier;
                double k2 = (totalGames2 < 300 ? 32.0 : 16.0) * kMultiplier;

                double e1 = 1.0 / (1.0 + Math.Pow(10.0, (r2.Rating - r1.Rating) / 400.0));
                double e2 = 1.0 - e1;

                double s1, s2;
                if (winnerId == 0)
                { s1 = 0.5; s2 = 0.5; r1.DrawCount++; r2.DrawCount++; }
                else if (winnerId == player1Id)
                { s1 = 1.0; s2 = 0.0; r1.WinCount++; r2.LoseCount++; }
                else
                { s1 = 0.0; s2 = 1.0; r1.LoseCount++; r2.WinCount++; }

                r1.Rating = Math.Max(0, r1.Rating + k1 * (s1 - e1));
                r2.Rating = Math.Max(0, r2.Rating + k2 * (s2 - e2));
                r1.UpdatedAt = DateTime.UtcNow;
                r2.UpdatedAt = DateTime.UtcNow;

                if (db.Entry(r1).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
                    db.PlayerRatings.Add(r1);
                if (db.Entry(r2).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
                    db.PlayerRatings.Add(r2);

                await db.SaveChangesAsync();

                // Redis 캐시 갱신 (TTL 1시간)
                await _redisManager.ExecuteAsync(db2 =>
                    db2.StringSetAsync(
                        $"{Consts.PLAYER_RATING_KEY_PREFIX}{player1Id}",
                        ((int)r1.Rating).ToString(),
                        TimeSpan.FromHours(1)));
                await _redisManager.ExecuteAsync(db2 =>
                    db2.StringSetAsync(
                        $"{Consts.PLAYER_RATING_KEY_PREFIX}{player2Id}",
                        ((int)r2.Rating).ToString(),
                        TimeSpan.FromHours(1)));

                _logger.LogInformation(
                    "[Matching] ELO 갱신 (K×{KM:.2f}) — P1:{P1} →{NR1:.0}, P2:{P2} →{NR2:.0}",
                    kMultiplier, player1Id, r1.Rating, player2Id, r2.Rating);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[Matching] ELO 레이팅 갱신 실패 — P1:{P1}, P2:{P2}", player1Id, player2Id);
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

        /// <summary>플레이어의 ELO 레이팅을 DB에서 조회합니다. 없으면 null.</summary>
        public async Task<PlayerRatingDto?> GetPlayerRatingDtoAsync(int userId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            PlayerRating? r = await db.PlayerRatings.FindAsync(userId);
            if (r == null)
                return null;
            return new PlayerRatingDto
            {
                PlayerId = r.PlayerId,
                Rating = r.Rating,
                WinCount = r.WinCount,
                LoseCount = r.LoseCount,
                DrawCount = r.DrawCount,
            };
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
