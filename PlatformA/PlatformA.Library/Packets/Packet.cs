namespace PlatformA.Library.Packets
{

    //[AttributeUsage(AttributeTargets.Struct)]
    //public class PacketAttribute : Attribute
    //{
    //}

    // TODO: 현재 이 패킷 구조체는 게임서버의 패킷 넘버링만을 의미함.
    public enum PacketID : ushort
    {
        C_Move      = 1, // Client -> Server 이동 요청
        S_Move      = 2, // Server -> Client 이동 결과 (브로드캐스트용)
        C_Login     = 3, // Client -> Server 로그인 요청
        S_Login     = 4, // Server -> Client 로그인 결과
        C_EnterRoom = 5, // Client -> Server 방 이동 요청 (매칭 성사 후 로비 → 게임방)
        S_EnterRoom = 6, // Server -> Client 방 이동 결과
    }
}
