using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PlatformA.Library.Common;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using StackExchange.Redis;

namespace PlatformA.Library.Core
{
    public class RedisManager
    {
        public static RedisManager Instance { get; } = new RedisManager();

        private IConnectionMultiplexer _redis = null!;
        private ResiliencePipeline _pipeline = ResiliencePipeline.Empty;

        // DI 컨테이너 구성 완료 후 SetLogger()로 주입됩니다.
        // 그 전(Init 시점)에는 NullLogger가 사용됩니다.
        private ILogger<RedisManager> _logger = NullLogger<RedisManager>.Instance;

        public IConnectionMultiplexer Connection => _redis;
        public RedisLockManager LockManager { get; private set; } = null!;

        private ISubscriber _subscriber = null!;
        public event Action<MatchSuccessEvent>? OnMatchSuccessReceived;

        private RedisManager() { }

        /// <summary>
        /// DI 구성 후 app.Services에서 주입합니다.
        /// Init() 이후에 호출해야 Polly 이벤트 로그가 정상 동작합니다.
        /// </summary>
        public void SetLogger(ILogger<RedisManager> logger)
        {
            _logger = logger;
        }

        public void Init(string connectionString, ILogger<RedisManager>? logger = null)
        {
            if (logger != null)
                _logger = logger;
            try
            {
                // ── Redis Cluster 연결 설정 ────────────────────���────────────
                var options = ConfigurationOptions.Parse(connectionString);
                options.AbortOnConnectFail = false;  // 시작 시 연결 실패해도 앱 중단 안 함
                options.ConnectTimeout = 5000;   // 연결 타임아웃 (ms)
                options.SyncTimeout = 3000;   // 동기 명령 타임아웃 (ms)
                options.AsyncTimeout = 3000;   // 비동기 명령 타임아웃 (ms)

                // 추가 옵션 클러스터 환경에서 구성 변경 시 자동 반영 및 DNS 풀이 강화
                options.ResolveDns = true;
                options.AllowAdmin = true;
                options.ConnectRetry = 3;
                options.KeepAlive = 10;

                options.CommandMap = CommandMap.Create(new HashSet<string>
                {
                    // 특정 명령어 제한이 필요하다면 여기서 설정 (기본값은 전체 허용)
                }, available: false);

                // 클러스터 재연결: 500ms → 최대 10초 지수 백오프
                options.ReconnectRetryPolicy = new ExponentialRetry(500, 10_000);

                _redis = ConnectionMultiplexer.Connect(options);
                LockManager = new RedisLockManager(this);
                _subscriber = _redis.GetSubscriber();

                // ── Polly 회로차단기 + 재시도 파이프라인 ───────────────────
                // 실행 순서(안쪽 → 바깥쪽): Retry → CircuitBreaker
                //   1. 명령이 실패하면 Retry가 최대 3번 재시도
                //   2. 30초 윈도우 내 5회 이상 호출 중 50%가 실패하면 회로 개방
                //   3. 회로 개방 중에는 BrokenCircuitException을 즉시 던져 Redis 부하 차단
                _pipeline = new ResiliencePipelineBuilder()
                    .AddRetry(new RetryStrategyOptions
                    {
                        MaxRetryAttempts = 3,
                        Delay = TimeSpan.FromMilliseconds(300),
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                        ShouldHandle = new PredicateBuilder()
                            .Handle<RedisException>()
                            .Handle<RedisTimeoutException>()
                            .Handle<RedisConnectionException>(),
                        OnRetry = args =>
                        {
                            _logger.LogWarning(
                                "[Redis] 재시도 {Attempt}회 — {Exception}",
                                args.AttemptNumber + 1,
                                args.Outcome.Exception?.Message);
                            return ValueTask.CompletedTask;
                        }
                    })
                    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                    {
                        FailureRatio = 0.5,
                        SamplingDuration = TimeSpan.FromSeconds(30),
                        MinimumThroughput = 5,
                        BreakDuration = TimeSpan.FromSeconds(60),
                        ShouldHandle = new PredicateBuilder()
                            .Handle<RedisException>()
                            .Handle<RedisTimeoutException>()
                            .Handle<RedisConnectionException>(),
                        OnOpened = args =>
                        {
                            _logger.LogError(
                                "[Redis] 회로차단기 OPEN — Redis 응답 불가. {Duration}초간 요청 차단",
                                args.BreakDuration.TotalSeconds);
                            return ValueTask.CompletedTask;
                        },
                        OnClosed = _ =>
                        {
                            _logger.LogInformation("[Redis] 회로차단기 CLOSED — Redis 복구 확인됨");
                            return ValueTask.CompletedTask;
                        },
                        OnHalfOpened = _ =>
                        {
                            _logger.LogWarning("[Redis] 회로차단기 HALF-OPEN — 복구 탐침 중");
                            return ValueTask.CompletedTask;
                        }
                    })
                    .Build();

                SubscribeToMatchingEvents();

                Console.WriteLine($"[RedisManager] Redis 클러스터 연결 성공! ({connectionString})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RedisManager] Redis 연결 실패: {ex.Message}");
            }
        }

        // ── Polly 래퍼 ────────────────────────────────────────────────────

        /// <summary>
        /// Redis 명령을 Polly(재시도 + 회로차단기) 파이프라인으로 실행합니다.
        /// BrokenCircuitException 발생 시 호출 측에서 적절히 처리(fail-open 등)하세요.
        /// </summary>
        public async Task<T> ExecuteAsync<T>(Func<IDatabase, Task<T>> action)
        {
            return await _pipeline.ExecuteAsync<T>(async _ =>
                await action(Connection.GetDatabase()));
        }

        /// <summary>반환값 없는 Redis 명령용 오버로드</summary>
        public async Task ExecuteAsync(Func<IDatabase, Task> action)
        {
            await _pipeline.ExecuteAsync(async _ =>
                await action(Connection.GetDatabase()));
        }

        // ── Pub/Sub ─────────────────────────────────���─────────────────────

        private void SubscribeToMatchingEvents()
        {
            _subscriber.Subscribe(RedisChannel.Literal("channel:match_success"), (_, message) =>
            {
                try
                {
                    var matchEvent = JsonSerializer.Deserialize<MatchSuccessEvent>(message!);
                    if (matchEvent != null)
                    {
                        _logger.LogInformation(
                            "[Redis PubSub] 매칭 성사 수신 — 방: {RoomId}, 유저: [{Users}]",
                            matchEvent.RoomId,
                            string.Join(", ", matchEvent.MatchedUserIds));
                        OnMatchSuccessReceived?.Invoke(matchEvent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Redis PubSub] 메시지 처리 실패");
                }
            });
        }

        public ISubscriber GetSubscriber() => _subscriber;
    }
}
