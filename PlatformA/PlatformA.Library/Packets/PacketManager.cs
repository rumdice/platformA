using System.Reflection;
using PlatformA.Library.Network;


namespace PlatformA.Library.Packets
{
    // 1. 패킷 처리 함수들을 담을 델리게이트 (ReadOnlySpan을 받기 위해 Action 대신 커스텀 사용)
    public delegate void PacketHandlerDelegate<T>(T session, ReadOnlySpan<byte> payload) where T : Session;

    // 2. 이 함수가 어떤 패킷을 처리하는지 표시해주는 명찰(Attribute)
    [AttributeUsage(AttributeTargets.Method)]
    public class PacketHandlerAttribute : Attribute
    {
        public ushort PacketId { get; }
        public PacketHandlerAttribute(ushort packetId) { PacketId = packetId; }
    }

    public class PacketManager<T> where T : Session
    {
        // 유일한 인스턴스임을 보장함.
        public static PacketManager<T> Instance { get; } = new PacketManager<T>();

        // 싱글톤이므로 생성자는 private로 막아서 외부에서 생성 불가능하게 
        private PacketManager() { }

        // [패킷 ID -> 처리 함수] 를 매핑해두는 사전 (O(1) 속도)
        private Dictionary<ushort, PacketHandlerDelegate<T>> _handlers = new Dictionary<ushort, PacketHandlerDelegate<T>>();

        // 서버 구동 시 딱 한 번 호출됩니다.
        public void Register()
        {
            _handlers.Clear();

            // 현재 실행 중인 프로그램의 모든 클래스를 스캔합니다.
            Assembly asm = typeof(T).Assembly;
            Type[] types = asm.GetTypes();

            foreach (Type type in types)
            {
                // 각 클래스에서 static 함수들을 가져옵니다.
                MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                foreach (MethodInfo method in methods)
                {
                    // 우리가 만든 [PacketHandler] 명찰이 붙어있는지 확인합니다.
                    var attribute = method.GetCustomAttribute<PacketHandlerAttribute>();
                    if (attribute != null)
                    {
                        // 🚀 핵심: 리플렉션은 느리지만, 이렇게 구동 시점에 한 번만 Delegate로 변환해두면
                        // 런타임에는 일반 함수 호출과 동일한 빛의 속도로 실행됩니다!
                        PacketHandlerDelegate<T> handler = (PacketHandlerDelegate<T>)Delegate.CreateDelegate(typeof(PacketHandlerDelegate<T>), method);

                        // 🚀 주의: Dictionary에 넣을 때 캐스팅 안전하게 확인
                        _handlers.Add(attribute.PacketId, handler);
                        Console.WriteLine($"[PacketManager] 라우팅 등록 완료: {attribute.PacketId} -> {method.Name}()");

                    }
                }
            }
        }

        // 수신부에서 switch문 대신 이 함수를 호출합니다.
        public void HandlePacket(T session, ushort packetId, ReadOnlySpan<byte> payload)
        {
            if (_handlers.TryGetValue(packetId, out PacketHandlerDelegate<T> handler))
            {
                handler.Invoke(session, payload);
            }
            else
            {
                Console.WriteLine($"[Warning] 등록되지 않은 패킷 ID: {packetId}");
            }
        }
    }
}
