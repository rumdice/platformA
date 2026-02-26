using PlatformA.Game.Server.Core;
using PlatformA.Game.Server.Packet;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PlatformA.Game.Server
{
    public class GameSession : Session
    {
        // 임시로 부여할 플레이어 ID (실제로는 로그인할 때 DB에서 가져오거나 자동 발급)
        public int SessionId { get; set; }

        protected override void OnConnected(EndPoint endPoint)
        {
            // 임시 ID 발급 (해시코드 등 사용)
            SessionId = endPoint.GetHashCode(); // 개선 포인트 : 입장할때마다 ~님이 바뀜.

            Console.WriteLine($"[GameSession] 유저 입장: {endPoint} (ID: {SessionId})");

            // 📝 명부에 내 이름 적기 세션메니저 등록.
            SessionManager.Instance.Add(this);
        }

        //protected override void OnRecv(ReadOnlySequence<byte> packet)
        //{
        //    // 받은 패킷 처리 (에코)
        //    string msg = Encoding.UTF8.GetString(packet.ToArray());
        //    Console.WriteLine($"[Packet Received] {msg}");

        //    // 받은 걸 그대로 다시 돌려보내기 (테스트용)
        //    // SendAsync도 나중에는 패킷 조립(헤더 2바이트 포함) 로직을 분리해야 하지만 일단 원본 전송
        //    // _ = SendAsync(packet.ToArray()); 
        //}


        /// <summary>
        /// 문자열을 버리고 정의된 구조체 바이너리 패킷으로 수신한다.
        /// </summary>
        /// <param name="packet"></param>
        protected override void OnRecv(ReadOnlySequence<byte> packet)
        {
            // ReadOnlySequence가 여러 조각으로 나뉘어 있을 수 있으니
            // 안전하게 연속된 Span으로 뽑아냅니다. (보통 단일 패킷은 한 조각에 들어있음)
            ReadOnlySpan<byte> span = packet.IsSingleSegment ? packet.FirstSpan : packet.ToArray().AsSpan();

            // 1. 헤더 파싱 (사이즈 2바이트 + 패킷 ID 2바이트)
            // 사이즈는 이미 파이프라인에서 검증하고 잘라서 넘겨주었으니 건너뛰고, 패킷 ID만 읽습니다.
            ushort packetId = BitConverter.ToUInt16(span.Slice(2, 2));

            // 2. 패킷 종류에 따른 라우팅
            switch ((PacketID)packetId)
            {
                case PacketID.C_Move:
                    // 본문은 앞의 헤더(4바이트: 사이즈 2 + ID 2)를 제외한 부분
                    ReadOnlySpan<byte> payload = span.Slice(4);

                    // 구조체 생성 및 파싱 (Zero-Allocation!)
                    C_MovePacket moveReq = new C_MovePacket();
                    moveReq.Deserialize(payload);

                    //Console.WriteLine($"[C_Move] 클라이언트 이동 요청 -> X: {movePkt.X}, Y: {movePkt.Y}, Z: {movePkt.Z}");
                    Console.WriteLine($"[C_Move] ID({SessionId}) 이동 -> X:{moveReq.X}, Y:{moveReq.Y}, Z:{moveReq.Z}");


                    // 내가 움직였음을 남들에게 알리기.
                    // 📡 1. 남들에게 뿌려줄 S_Move 패킷 만들기
                    S_MovePacket moveRes = new S_MovePacket()
                    {
                        PlayerId = this.SessionId,
                        X = moveReq.X,
                        Y = moveReq.Y,
                        Z = moveReq.Z
                    };

                    // 📡 2. 패킷 조립 (헤더 4바이트 + 본문 16바이트 = 총 20바이트)
                    ushort resSize = 20;
                    ushort resId = (ushort)PacketID.S_Move;
                    byte[] sendBuffer = new byte[resSize];
                    Span<byte> sendSpan = sendBuffer.AsSpan();

                    BitConverter.TryWriteBytes(sendSpan.Slice(0, 2), resSize);
                    BitConverter.TryWriteBytes(sendSpan.Slice(2, 2), resId);
                    moveRes.Serialize(sendSpan.Slice(4)); // 본문 직렬화

                    // 📡 3. 매니저를 통해 접속한 "모든 유저"에게 발사!
                    SessionManager.Instance.Broadcast(sendBuffer);

                    break;

                default:
                    Console.WriteLine($"[Unknown] 알 수 없는 패킷 ID: {packetId}");
                    break;
            }

        }


        protected override void OnDisconnected(EndPoint endPoint)
        {
            Console.WriteLine($"[GameSession] 유저 퇴장: {endPoint}");
        }
    }
}
