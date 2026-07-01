using System.Collections.Concurrent;

namespace PlatformA.Library.Game.Core
{
    public abstract class GameRoomManagerBase<TRoom, TKey>
        where TRoom : GameRoom
        where TKey : notnull
    {
        protected readonly ConcurrentDictionary<TKey, TRoom> _rooms = new();

        public TRoom GetOrCreate(TKey key, Func<TKey, TRoom> factory)
        {
            return _rooms.GetOrAdd(key, factory);
        }

        public TRoom? Find(TKey key)
        {
            _rooms.TryGetValue(key, out TRoom? room);
            return room;
        }

        public bool Remove(TKey key)
        {
            if (_rooms.TryRemove(key, out _))
            {
                Console.WriteLine($"[{GetType().Name}] 방 소멸: {key}");
                return true;
            }
            return false;
        }

        public int Count => _rooms.Count;
    }
}
