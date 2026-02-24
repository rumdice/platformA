using PlatformA.Game.Server.Core;
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

        protected override void OnRecv(ReadOnlySequence<byte> packet)
        {
            // 받은 패킷 처리 (에코)
            string msg = Encoding.UTF8.GetString(packet.ToArray());
            Console.WriteLine($"[Packet Received] {msg}");

            // 받은 걸 그대로 다시 돌려보내기 (테스트용)
            // SendAsync도 나중에는 패킷 조립(헤더 2바이트 포함) 로직을 분리해야 하지만 일단 원본 전송
            // _ = SendAsync(packet.ToArray()); 
        }

        protected override void OnDisconnected(EndPoint endPoint)
        {
            Console.WriteLine($"[GameSession] 유저 퇴장: {endPoint}");
        }
    }
}
