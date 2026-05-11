using System.Reflection;
using PlatformA.Library.Network;

namespace PlatformA.Library.Packets
{
    public delegate void PacketHandlerDelegate<T>(T session, Packet packet) where T : Session;

    [AttributeUsage(AttributeTargets.Method)]
    public class PacketHandlerAttribute : Attribute
    {
        public Packet.PayloadOneofCase OneofCase { get; }

        public PacketHandlerAttribute(Packet.PayloadOneofCase oneofCase)
        {
            OneofCase = oneofCase;
        }
    }

    public class PacketManager<T> where T : Session
    {
        public static PacketManager<T> Instance { get; } = new PacketManager<T>();

        private PacketManager() { }

        private Dictionary<Packet.PayloadOneofCase, PacketHandlerDelegate<T>> _handlers
            = new Dictionary<Packet.PayloadOneofCase, PacketHandlerDelegate<T>>();

        public void Register()
        {
            _handlers.Clear();

            Assembly asm = typeof(T).Assembly;

            foreach (Type type in asm.GetTypes())
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                {
                    var attribute = method.GetCustomAttribute<PacketHandlerAttribute>();
                    if (attribute == null)
                        continue;

                    PacketHandlerDelegate<T> handler = (PacketHandlerDelegate<T>)Delegate.CreateDelegate(
                        typeof(PacketHandlerDelegate<T>), method);

                    _handlers.Add(attribute.OneofCase, handler);
                    Console.WriteLine($"[PacketManager] 라우팅 등록 완료: {attribute.OneofCase} -> {method.Name}()");
                }
            }
        }

        public void HandlePacket(T session, Packet packet)
        {
            if (_handlers.TryGetValue(packet.PayloadCase, out PacketHandlerDelegate<T> handler))
            {
                handler.Invoke(session, packet);
            }
            else
            {
                Console.WriteLine($"[Warning] 등록되지 않은 패킷: {packet.PayloadCase}");
            }
        }
    }
}
