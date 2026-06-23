using System.Collections.Concurrent;

namespace PlatformA.Game.Lobby.Services
{
    /// <summary>
    /// Lobby에 연결된 유저의 ConnectionId를 추적합니다. (Singleton)
    /// </summary>
    public class LobbyPresenceService
    {
        private readonly ConcurrentDictionary<int, string> _userConnections = new();

        public void Register(int userId, string connectionId)
            => _userConnections[userId] = connectionId;

        public void Unregister(int userId)
            => _userConnections.TryRemove(userId, out _);

        public string? GetConnectionId(int userId)
            => _userConnections.TryGetValue(userId, out var cid) ? cid : null;

        public bool IsOnline(int userId)
            => _userConnections.ContainsKey(userId);

        public int OnlineCount => _userConnections.Count;
    }
}
