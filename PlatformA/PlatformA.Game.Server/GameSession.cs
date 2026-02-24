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
        protected override void OnConnected(EndPoint endPoint)
        {
            Console.WriteLine($"[GameSession] 유저 입장: {endPoint}");
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
                    C_MovePacket movePkt = new C_MovePacket();
                    movePkt.Deserialize(payload);

                    Console.WriteLine($"[C_Move] 클라이언트 이동 요청 -> X: {movePkt.X}, Y: {movePkt.Y}, Z: {movePkt.Z}");
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
