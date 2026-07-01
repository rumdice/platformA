namespace PlatformA.Library.Game.Network
{
    public interface IGameSession
    {
        int SessionId { get; set; }
        string? LoginLockValue { get; set; }
        Task SendAsync(byte[] data);
        void Disconnect();
    }
}
