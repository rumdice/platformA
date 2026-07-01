using PlatformA.Game.Gomoku.Network;

namespace PlatformA.Tests.Game.Gomoku
{
    /// <summary>
    /// TCP 소켓 없이 테스트에서 사용할 수 있는 GomokuSession 대역(Fake).
    /// 생성자에서 SessionId를 설정하고, OnRecv·OnDisconnected는 테스트에서 직접 호출하지 않는다.
    /// </summary>
    internal class FakeGomokuSession : GomokuSession
    {
        public FakeGomokuSession(int playerId)
        {
            SessionId = playerId;
        }
    }
}
