using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using PlatformA.Library.Network;
using Xunit;

namespace PlatformA.Tests.Game.Gomoku
{
    // Session은 abstract이므로 테스트용 최소 구현체
    internal class TestSession : Session
    {
        public int OnDisconnectedCallCount { get; private set; } = 0;
        public EndPoint? LastEndPoint { get; private set; }

        protected override void OnConnected(EndPoint endPoint) { }
        protected override void OnRecv(ReadOnlySequence<byte> packet) { }

        protected override void OnDisconnected(EndPoint endPoint)
        {
            OnDisconnectedCallCount++;
            LastEndPoint = endPoint;
        }
    }

    public class SessionDisconnectSafetyTests
    {
        // _socket 필드에 Reflection으로 값을 주입한다.
        // Session은 다른 어셈블리의 private 필드이므로 NonPublic | Instance가 필요하다.
        private static void InjectSocket(Session session, Socket? socket)
        {
            FieldInfo field = typeof(Session).GetField(
                "_socket",
                BindingFlags.NonPublic | BindingFlags.Instance)!;
            field.SetValue(session, socket);
        }

        [Fact]
        public void Disconnect_WhenSocketIsNull_CallsOnDisconnected()
        {
            var session = new TestSession();
            // _socket이 null인 채로 Disconnect 호출

            session.Disconnect();

            Assert.Equal(1, session.OnDisconnectedCallCount);
        }

        [Fact]
        public void Disconnect_WhenSocketIsNull_FallbackEndPointIsIPAddressNone()
        {
            var session = new TestSession();

            session.Disconnect();

            var ep = Assert.IsType<IPEndPoint>(session.LastEndPoint);
            Assert.Equal(IPAddress.None, ep.Address);
        }

        [Fact]
        public void Disconnect_WhenSocketIsDisposed_CallsOnDisconnectedDespiteObjectDisposedException()
        {
            // 닫힌 소켓의 RemoteEndPoint 접근 → ObjectDisposedException
            // OnDisconnected가 그럼에도 반드시 호출되어야 한다
            var session = new TestSession();
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Close(); // Dispose됨
            InjectSocket(session, socket);

            session.Disconnect(); // 예외 없이 완료되어야 한다

            Assert.Equal(1, session.OnDisconnectedCallCount);
        }

        [Fact]
        public void Disconnect_WhenSocketIsDisposed_SocketSetToNullAfterwards()
        {
            var session = new TestSession();
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Close();
            InjectSocket(session, socket);

            session.Disconnect();

            FieldInfo field = typeof(Session).GetField(
                "_socket",
                BindingFlags.NonPublic | BindingFlags.Instance)!;
            Assert.Null(field.GetValue(session));
        }

        [Fact]
        public void Disconnect_CalledTwice_OnDisconnectedCalledOnlyOnce()
        {
            // Interlocked.Exchange 이중 해제 방지 검증
            var session = new TestSession();

            session.Disconnect();
            session.Disconnect();

            Assert.Equal(1, session.OnDisconnectedCallCount);
        }

        [Fact]
        public async Task Disconnect_CalledTwiceConcurrently_OnDisconnectedCalledOnlyOnce()
        {
            // 동시 호출 환경에서도 Interlocked 보호가 유지되는지 검증
            var session = new TestSession();

            await Task.WhenAll(
                Task.Run(() => session.Disconnect()),
                Task.Run(() => session.Disconnect()));

            Assert.Equal(1, session.OnDisconnectedCallCount);
        }

        [Fact]
        public void Disconnect_WithConnectedSocket_CallsOnDisconnectedWithRemoteEndPoint()
        {
            // 정상적으로 연결된 소켓 쌍에서 RemoteEndPoint가 올바르게 전달되는지 검증
            using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);
            int port = ((IPEndPoint)listener.LocalEndPoint!).Port;

            using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            client.Connect(IPAddress.Loopback, port);
            using var server = listener.Accept();

            var session = new TestSession();
            InjectSocket(session, client);

            session.Disconnect();

            Assert.Equal(1, session.OnDisconnectedCallCount);
            var ep = session.LastEndPoint as IPEndPoint;
            Assert.NotNull(ep);
            Assert.NotEqual(IPAddress.None, ep!.Address);
        }
    }
}
