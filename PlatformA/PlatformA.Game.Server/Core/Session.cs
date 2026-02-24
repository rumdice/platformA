using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PlatformA.Game.Server.Core
{

    public abstract class Session
    {
        private Socket _socket;
        private int _disconnected = 0; // 중복 해제 방지용 플래그

        // ----------------------------------------------------
        // 하위 클래스(게임 로직)에서 반드시 구현해야 할 이벤트들
        // ----------------------------------------------------
        protected abstract void OnConnected(EndPoint endPoint);
        protected abstract void OnRecv(ReadOnlySequence<byte> packet);
        protected abstract void OnDisconnected(EndPoint endPoint);

        // 프레임워크 시작점
        public void Start(Socket socket)
        {
            _socket = socket;
            OnConnected(_socket.RemoteEndPoint);

            var pipe = new Pipe();

            // 백그라운드에서 읽기/쓰기 파이프라인 가동 (Fire and Forget)
            Task.Run(() => FillPipeAsync(pipe.Writer));
            Task.Run(() => ReadPipeAsync(pipe.Reader));
        }

        // 데이터 전송 (프레임워크 사용자가 호출할 메서드)
        public async Task SendAsync(byte[] sendBuff)
        {
            try
            {
                await _socket.SendAsync(sendBuff, SocketFlags.None);
            }
            catch (Exception)
            {
                Disconnect();
            }
        }

        public void Disconnect()
        {
            // 이미 끊겼으면 무시 (Interlocked로 동시성 방어)
            if (Interlocked.Exchange(ref _disconnected, 1) == 1)
                return;

            OnDisconnected(_socket.RemoteEndPoint);
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }


        // ----------------------------------------------------
        // 파이프라인 엔진 영역 (외부에서 알 필요 없음 - private)
        // ----------------------------------------------------
        private async Task FillPipeAsync(PipeWriter writer)
        {
            const int minimumBufferSize = 512;
            while (true)
            {
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);
                try
                {
                    int bytesRead = await _socket.ReceiveAsync(memory, SocketFlags.None);
                    if (bytesRead == 0) break; // 정상 종료

                    writer.Advance(bytesRead);
                }
                catch (Exception)
                {
                    break; // 에러 종료
                }

                FlushResult result = await writer.FlushAsync();
                if (result.IsCompleted) break;
            }

            await writer.CompleteAsync();
            Disconnect(); // 소켓 끊김 처리
        }

        private async Task ReadPipeAsync(PipeReader reader)
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadPacket(ref buffer, out ReadOnlySequence<byte> packet))
                {
                    // 🔥 핵심: 패킷이 완성되면 하위 클래스의 OnRecv로 토스!
                    OnRecv(packet);
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted) break;
            }
            await reader.CompleteAsync();
        }

        private bool TryReadPacket(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> packet)
        {
            if (buffer.Length < 2)
            {
                packet = default;
                return false;
            }

            var lengthBuffer = buffer.Slice(0, 2);
            ushort packetLength = BitConverter.ToUInt16(lengthBuffer.ToArray(), 0);

            if (buffer.Length < 2 + packetLength)
            {
                packet = default;
                return false;
            }

            packet = buffer.Slice(2, packetLength);
            buffer = buffer.Slice(2 + packetLength);
            return true;
        }
    }
}
