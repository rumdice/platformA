using System.Reflection;
using Microsoft.Extensions.Logging;
using PlatformA.Game.Gomoku.Core;

namespace PlatformA.Tests.Game.Gomoku
{
    public class GomokuRoomLoggerTests
    {
        // internal SetLogger를 Reflection으로 호출하기 위한 헬퍼
        private static void InvokeSetLogger(ILogger logger)
        {
            var method = typeof(GomokuRoom).GetMethod(
                "SetLogger",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            method!.Invoke(null, new object[] { logger });
        }

        // ----------------------------------------------------------------
        // FakeLogger — 외부 의존성 없는 순수 인메모리 ILogger 구현
        // ----------------------------------------------------------------
        private class FakeLogger : ILogger
        {
            public List<string> Logs { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => Logs.Add(formatter(state, exception));
        }

        // ----------------------------------------------------------------
        // 테스트 1: Reflection으로 internal SetLogger 호출 시 예외 없음
        // ----------------------------------------------------------------
        [Fact]
        public void SetLogger_WithFakeLogger_DoesNotThrow()
        {
            var fakeLogger = new FakeLogger();

            var exception = Record.Exception(() => InvokeSetLogger(fakeLogger));

            Assert.Null(exception);
        }

        // ----------------------------------------------------------------
        // 테스트 2: logger 설정 후 GomokuRoomManager 생명주기(GetOrCreate/Remove) 정상 동작
        // ----------------------------------------------------------------
        [Fact]
        public void SetLogger_WithFakeLogger_RoomLifecycleStillWorks()
        {
            var fakeLogger = new FakeLogger();
            InvokeSetLogger(fakeLogger);

            string roomId = Guid.NewGuid().ToString();

            GomokuRoom room = GomokuRoomManager.Instance.GetOrCreate(roomId);

            Assert.NotNull(room);
            Assert.Same(room, GomokuRoomManager.Instance.GetOrCreate(roomId));

            bool removed = GomokuRoomManager.Instance.Remove(roomId);

            Assert.True(removed);
            Assert.Null(GomokuRoomManager.Instance.Find(roomId));
        }
    }
}
