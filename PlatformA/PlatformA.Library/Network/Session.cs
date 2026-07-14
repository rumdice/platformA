using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;

namespace PlatformA.Library.Network
{

    public abstract class Session
    {
        private Socket? _socket;
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

            // 백그라운드에서 읽기/쓰기 파이프라인 가동 (Fire and Forget)
            var pipe = new Pipe();
            Task.Run(() => RunPipelineAsync(pipe));
        }

        private async Task RunPipelineAsync(Pipe pipe)
        {
            var fillTask = FillPipeAsync(pipe.Writer);
            var readTask = ReadPipeAsync(pipe.Reader);
            await Task.WhenAll(fillTask, readTask);
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

            // RemoteEndPoint를 먼저 캡처한다.
            // RST 수신 직후 OS 상태 갱신 타이밍에 따라 SocketException(107) ENOTCONN이 발생할 수 있으며,
            // 이 경우 OnDisconnected()가 호출되지 않으면 Redis 로그인 락이 해제되지 않는다.
            EndPoint? endPoint = null;
            try
            { endPoint = _socket?.RemoteEndPoint; }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }

            OnDisconnected(endPoint ?? new IPEndPoint(IPAddress.None, 0));

            try
            {
                _socket?.Shutdown(SocketShutdown.Both);
                _socket?.Close();
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
            finally
            {
                _socket = null;
            }
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
                    if (bytesRead == 0)
                        break; // 정상 종료

                    writer.Advance(bytesRead);

                    FlushResult result = await writer.FlushAsync();
                    if (result.IsCompleted)
                        break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FillPipeAsync Error {ex.ToString()}");
                    break; // 에러 종료
                }
            }

            await writer.CompleteAsync();
            Disconnect(); // 소켓 끊김 처리
        }

        private async Task ReadPipeAsync(PipeReader reader)
        {
            try
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
                    if (result.IsCompleted)
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ReadPipeAsync Error {ex.ToString()}");
            }
            finally
            {
                await reader.CompleteAsync();
                Disconnect();
            }
        }

        private bool TryReadPacket(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> packet)
        {
            // 1. 최소 사이즈 헤더(2바이트)가 올 때까지 대기
            if (buffer.Length < 2)
            {
                packet = default;
                return false;
            }

            // 2. 패킷 전체 길이 파악 (BitConverter 사용)
            var lengthBuffer = buffer.Slice(0, 2);
            //ushort packetLength = BitConverter.ToUInt16(lengthBuffer.ToArray(), 0);

            // REVIEW:딥다이브 리뷰 1-1
            //2. 패킷 전체 길이 파악(zero-allocate)
            ushort packetLength;

            if (lengthBuffer.IsSingleSegment)
            {
                // 조각나지 않았다면 직접 Span으로 읽음 (가장 빠른 경로)
                packetLength = BinaryPrimitives.ReadUInt16LittleEndian(lengthBuffer.FirstSpan);
            }
            else
            {
                // 조각났다면 스택 메모리에 임시 복사 후 읽음 (힙 할당 없음)
                Span<byte> tempSpan = stackalloc byte[2];
                lengthBuffer.CopyTo(tempSpan);
                packetLength = BinaryPrimitives.ReadUInt16LittleEndian(tempSpan);
            }


            // 3. 🚨 [수정됨] 패킷이 "전체 길이(packetLength)"만큼 다 안 왔으면 대기
            if (buffer.Length < packetLength)
            {
                packet = default;
                return false;
            }

            // 4. 🚨 [수정됨] 사이즈 헤더를 포함하여 통째로 잘라서 OnRecv로 넘김!
            packet = buffer.Slice(0, packetLength);

            // 5. 원본 버퍼에서는 꺼내간 만큼 포인터 이동
            buffer = buffer.Slice(packetLength);

            return true;
        }
    }
}
