using System.Buffers;
using System.Net;
using System.Reflection;
using PlatformA.Library.Network;

namespace PlatformA.Tests.Game.Lobby.Network
{
    /// <summary>
    /// Test-only Session implementation.
    /// Session.SendAsync calls _socket.SendAsync internally; when _socket is null
    /// the Task throws NullReferenceException, but Broadcast uses fire-and-forget
    /// (_ = session.SendAsync(...)) so the exception is silently swallowed.
    /// This means we can safely add FakeSession instances to a manager and call
    /// Broadcast without crashing the test process.
    /// </summary>
    internal class FakeSession : Session
    {
        protected override void OnConnected(EndPoint endPoint) { }
        protected override void OnRecv(ReadOnlySequence<byte> buffer) { }
        protected override void OnDisconnected(EndPoint endPoint) { }
    }

    public class SessionManagerTests
    {
        // Helper: read the private _sessions HashSet via reflection
        private static HashSet<Session> GetSessions(object manager)
        {
            var field = manager.GetType()
                .GetField("_sessions", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (HashSet<Session>)field.GetValue(manager)!;
        }

        [Fact]
        public void Add_NewSession_SessionExists()
        {
            var manager = new SessionManager<FakeSession>();
            var session = new FakeSession();

            manager.Add(session);

            var sessions = GetSessions(manager);
            Assert.Contains(session, sessions);
        }

        [Fact]
        public void Remove_ExistingSession_SessionRemoved()
        {
            var manager = new SessionManager<FakeSession>();
            var session = new FakeSession();

            manager.Add(session);
            manager.Remove(session);

            var sessions = GetSessions(manager);
            Assert.DoesNotContain(session, sessions);
        }

        [Fact]
        public void Add_ConcurrentAdd_AllSessionsAdded()
        {
            const int threadCount = 20;
            var manager = new SessionManager<FakeSession>();
            var fakeSessions = Enumerable.Range(0, threadCount)
                .Select(_ => new FakeSession())
                .ToArray();

            var threads = fakeSessions
                .Select(s => new Thread(() => manager.Add(s)))
                .ToArray();

            foreach (var t in threads)
                t.Start();
            foreach (var t in threads)
                t.Join();

            var sessions = GetSessions(manager);
            Assert.Equal(threadCount, sessions.Count);
            foreach (var s in fakeSessions)
                Assert.Contains(s, sessions);
        }

        [Fact]
        public void Broadcast_MultipleSessions_AllSessionsPresent()
        {
            // Verifies that Broadcast iterates all registered sessions without throwing.
            // Session.SendAsync is fire-and-forget; a null socket causes an async exception
            // that is silently discarded, so this test confirms the manager holds the correct
            // sessions at the time of the broadcast call.
            var manager = new SessionManager<FakeSession>();
            var session1 = new FakeSession();
            var session2 = new FakeSession();
            var session3 = new FakeSession();

            manager.Add(session1);
            manager.Add(session2);
            manager.Add(session3);

            var packet = new byte[] { 0x05, 0x00, 0x01, 0x02, 0x03 };

            // Should not throw — SendAsync exceptions are swallowed by fire-and-forget
            var exception = Record.Exception(() => manager.Broadcast(packet));
            Assert.Null(exception);

            // All three sessions are still registered
            var sessions = GetSessions(manager);
            Assert.Equal(3, sessions.Count);
        }
    }
}
