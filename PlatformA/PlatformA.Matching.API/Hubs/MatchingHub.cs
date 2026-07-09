using Microsoft.AspNetCore.SignalR;

namespace PlatformA.Matching.API.Hubs
{
    // EngineService(주식 주문 매칭 엔진)가 IHubContext<MatchingHub>로 방송에 사용하므로 유지.
    // 게임 플레이어 JWT 검증·그룹 등록 로직은 Game.Lobby 도입 이후 불필요해져 제거.
    public class MatchingHub : Hub
    {
    }
}
