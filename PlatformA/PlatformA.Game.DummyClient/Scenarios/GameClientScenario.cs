using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using PlatformA.Library.Common;
using PlatformA.Library.Packets;

namespace PlatformA.Game.DummyClient.Scenarios
{
    public class GameClientScenario
    {
        public static async Task RunAsync()
        {
            Console.Clear();
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine("   [시나리오 1] 게임 서버 직접 접속 테스트");
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine("  흐름: Auth → 대기열 → 게임 서버 TCP → C_Move 인터랙티브");
            Console.WriteLine();

            // Step 1: 로그인
            Console.Write("  아이디: ");
            string username = Console.ReadLine()?.Trim() ?? "";
            Console.Write("  비밀번호: ");
            string password = Console.ReadLine()?.Trim() ?? "";

            using var http = new HttpClient();
            var session = await AuthHelper.LoginAsync(http, username, password);
            if (session == null)
            {
                Console.WriteLine("  [실패] 로그인 실패. 서버 상태 및 계정을 확인하세요.");
                Console.ReadLine();
                return;
            }
            Console.WriteLine($"  [OK] 로그인 성공! PlayerId: {session.PlayerId}");
            AuthHelper.ApplyToken(http, session);

            // Step 2: 대기열 진입
            Console.WriteLine("\n  [대기열] 진입 중...");
            var enterResp = await http.PostAsync($"{Consts.TICKET_API_URL}/api/queue/enter", null);
            if (!enterResp.IsSuccessStatusCode)
            {
                Console.WriteLine($"  [실패] 대기열 진입 실패 ({enterResp.StatusCode}).");
                Console.ReadLine();
                return;
            }
            Console.WriteLine("  [OK] 대기열 진입 완료.");

            // Step 3: Active 대기 (폴링)
            Console.WriteLine("  [대기열] Active 상태 대기 중...");
            bool activated = false;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(300));
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var statusResp = await http.GetAsync(
                        $"{Consts.TICKET_API_URL}/api/queue/status", cts.Token);
                    if (!statusResp.IsSuccessStatusCode) break;

                    var data = await statusResp.Content.ReadFromJsonAsync<QueueStatusDto>(
                        cancellationToken: cts.Token);
                    if (data?.Status == "Active") { activated = true; break; }

                    Console.WriteLine($"  ⏳ 대기 중... (앞에 {data?.Rank}명 / {data?.NextPollDelay}ms 후 재확인)");
                    await Task.Delay(data?.NextPollDelay ?? 3000, cts.Token);
                }
            }
            catch (OperationCanceledException) { }

            if (!activated)
            {
                Console.WriteLine("  [실패] 대기열 타임아웃. 서버 상태를 확인하세요.");
                Console.ReadLine();
                return;
            }
            Console.WriteLine("  [OK] 대기열 통과!\n");

            // Step 4: 게임 서버 TCP 접속 (광장 roomId=1, 인터랙티브)
            Console.WriteLine("  [게임서버] 광장(roomId=1)에 접속합니다...");
            await ConnectAndLoginAsync(session.PlayerId, session.AccessToken);
        }

        // 부하 테스트용 비-인터랙티브 TCP 로그인 검증
        // 연결 → C_Login → S_Login 응답 확인 후 즉시 종료
        public static async Task<bool> VerifyGameServerLoginAsync(string token, int roomId = 1)
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(Consts.GAME_SERVER_IP, Consts.GAME_SERVER_PORT);

                byte[] packet = MakeLoginPacket(token, roomId);
                await socket.SendAsync(packet, SocketFlags.None);

                byte[] recvBuf = new byte[64];
                var recvTask = socket.ReceiveAsync(recvBuf, SocketFlags.None);
                if (await Task.WhenAny(recvTask, Task.Delay(10_000)) != recvTask)
                    return false;

                int received = await recvTask;
                if (received < 4 + S_LoginPacket.Size) return false;

                ushort respId = BitConverter.ToUInt16(recvBuf, 2);
                if (respId != (ushort)PacketID.S_Login) return false;

                var pkt = new S_LoginPacket();
                pkt.Deserialize(recvBuf.AsSpan(4, S_LoginPacket.Size));
                return pkt.ResultCode == S_LoginPacket.ResultSuccess;
            }
            catch { return false; }
        }

        // 게임서버 접속 (광장 Room 1, 인터랙티브)
        public static async Task ConnectAndLoginAsync(int userId, string realToken)
        {
            using Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                await client.ConnectAsync(Consts.GAME_SERVER_IP, Consts.GAME_SERVER_PORT);

                var loginTcs = new TaskCompletionSource<S_LoginPacket>(TaskCreationOptions.RunContinuationsAsynchronously);
                _ = ReceiveLoopAsync(client, loginTcs);

                await SendLoginPacketAsync(client, realToken, roomId: 1);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                cts.Token.Register(() => loginTcs.TrySetCanceled());

                S_LoginPacket result;
                try { result = await loginTcs.Task; }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("[GameServer] 로그인 응답 타임아웃. 서버 상태를 확인하세요.");
                    return;
                }

                if (result.ResultCode != S_LoginPacket.ResultSuccess)
                {
                    string reason = result.ResultCode switch
                    {
                        S_LoginPacket.ResultInvalidToken => "JWT 토큰이 유효하지 않습니다.",
                        S_LoginPacket.ResultNotInQueue => "대기열을 통과하지 않은 접속입니다.",
                        S_LoginPacket.ResultDuplicate => "이미 다른 기기에서 접속 중입니다.",
                        S_LoginPacket.ResultRoomNotFound => "입장할 방이 아직 준비되지 않았습니다.",
                        _ => $"알 수 없는 오류 (코드: {result.ResultCode})"
                    };
                    Console.WriteLine($"[GameServer] 로그인 실패: {reason}");
                    return;
                }

                Console.WriteLine($"[GameServer] 게임 서버 로그인 성공! PlayerID: {result.PlayerId}");
                Console.WriteLine("엔터를 누를 때마다 이동 패킷(C_Move)을 전송합니다. q로 종료");

                Random rand = new Random();
                while (true)
                {
                    string input = Console.ReadLine();
                    if (input?.ToLower() == "q")
                        break;
                    float x = rand.Next(-50, 50);
                    float y = rand.Next(-50, 50);
                    await SendMovePacketAsync(client, x, y, 0f);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"접속 실패: {ex.Message}");
            }
        }

        static byte[] MakeMovePacket(float x, float y, float z)
        {
            ushort packetSize = 16;
            ushort packetId = (ushort)PacketID.C_Move;
            C_MovePacket movePacket = new C_MovePacket { X = x, Y = y, Z = z };

            byte[] sendBuffer = new byte[packetSize];
            Span<byte> span = sendBuffer.AsSpan();
            BitConverter.TryWriteBytes(span.Slice(0, 2), packetSize);
            BitConverter.TryWriteBytes(span.Slice(2, 2), packetId);
            movePacket.Serialize(span.Slice(4));
            return sendBuffer;
        }

        // payload 포맷: 헤더(4) + roomId(4) + stringLen(2) + token(N)
        static byte[] MakeLoginPacket(string token, int roomId)
        {
            byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
            ushort stringLen = (ushort)tokenBytes.Length;
            ushort packetSize = (ushort)(4 + 4 + 2 + stringLen);
            ushort packetId = (ushort)PacketID.C_Login;

            byte[] buffer = new byte[packetSize];
            Span<byte> span = buffer.AsSpan();
            BitConverter.TryWriteBytes(span.Slice(0, 2), packetSize);
            BitConverter.TryWriteBytes(span.Slice(2, 2), packetId);
            BitConverter.TryWriteBytes(span.Slice(4, 4), roomId);
            BitConverter.TryWriteBytes(span.Slice(8, 2), stringLen);
            tokenBytes.CopyTo(span.Slice(10));
            return buffer;
        }

        static async Task SendLoginPacketAsync(Socket client, string token, int roomId)
        {
            byte[] packet = MakeLoginPacket(token, roomId);
            await client.SendAsync(packet, SocketFlags.None);
            Console.WriteLine($"[Send] C_Login (RoomId:{roomId}) - {packet.Length} bytes");
        }

        static async Task SendMovePacketAsync(Socket client, float x, float y, float z)
        {
            byte[] packet = MakeMovePacket(x, y, z);
            await client.SendAsync(packet, SocketFlags.None);
            Console.WriteLine($"[Send] C_Move ({x}, {y}, {z}) - 16 bytes");
        }

        static async Task ReceiveLoopAsync(Socket client, TaskCompletionSource<S_LoginPacket>? loginTcs = null)
        {
            byte[] buffer = new byte[1024];
            try
            {
                while (true)
                {
                    int received = await client.ReceiveAsync(buffer, SocketFlags.None);
                    if (received == 0)
                    {
                        Console.WriteLine("서버와 연결이 끊어졌습니다.");
                        loginTcs?.TrySetCanceled();
                        break;
                    }

                    if (received >= 4)
                    {
                        ushort packetId = BitConverter.ToUInt16(buffer, 2);

                        if (packetId == (ushort)PacketID.S_Move)
                        {
                            int playerId = BitConverter.ToInt32(buffer, 4);
                            float x = BitConverter.ToSingle(buffer, 8);
                            float y = BitConverter.ToSingle(buffer, 12);
                            float z = BitConverter.ToSingle(buffer, 16);
                            Console.WriteLine($"\n  [Broadcast] 플레이어 {playerId} 이동 -> X:{x}, Y:{y}, Z:{z}");
                        }
                        else if (packetId == (ushort)PacketID.S_Login && received >= 4 + S_LoginPacket.Size)
                        {
                            var pkt = new S_LoginPacket();
                            pkt.Deserialize(buffer.AsSpan(4, S_LoginPacket.Size));
                            loginTcs?.TrySetResult(pkt);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"수신 에러: {ex.Message}");
                loginTcs?.TrySetCanceled();
            }
        }
    }
}
