using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlatformA.Library.Common
{
    // 🚀 Redis Pub/Sub으로 주고받을 매칭 성공 이벤트 데이터 (DTO)
    public class MatchSuccessEvent
    {
        public int RoomId { get; set; } // 게임 서버가 생성해야 할 방 번호
        public List<int> MatchedUserIds { get; set; } // 이 방에 들어올 권한이 있는 유저들

        public MatchSuccessEvent()
        {
            MatchedUserIds = new List<int>();
        }
    }
}
