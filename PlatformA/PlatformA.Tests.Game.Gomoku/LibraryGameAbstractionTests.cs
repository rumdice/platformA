using PlatformA.Game.Gomoku.Core;
using PlatformA.Library.Game.Core;
using PlatformA.Library.Game.Network;
using System.Reflection;
using Xunit;

namespace PlatformA.Tests.Game.Gomoku;

public class LibraryGameAbstractionTests
{
    [Fact]
    public void GomokuRoomManager_InheritsGameRoomManagerBase()
    {
        Assert.IsAssignableFrom<GameRoomManagerBase<GomokuRoom, string>>(
            GomokuRoomManager.Instance);
    }

    [Fact]
    public void GameRoomManagerBase_GetOrCreate_CreatesNewRoom()
    {
        string roomId = $"abstraction-test-{Guid.NewGuid():N}";

        GomokuRoom room = GomokuRoomManager.Instance.GetOrCreate(roomId);
        GomokuRoom? found = GomokuRoomManager.Instance.Find(roomId);

        try
        {
            Assert.NotNull(room);
            Assert.NotNull(found);
            Assert.Same(room, found);
        }
        finally
        {
            GomokuRoomManager.Instance.Remove(roomId);
        }
    }

    [Fact]
    public void GameRoomManagerBase_Remove_RemovesRoom()
    {
        string roomId = $"abstraction-remove-{Guid.NewGuid():N}";

        GomokuRoomManager.Instance.GetOrCreate(roomId);
        bool removed = GomokuRoomManager.Instance.Remove(roomId);
        GomokuRoom? found = GomokuRoomManager.Instance.Find(roomId);

        Assert.True(removed);
        Assert.Null(found);
    }

    [Fact]
    public void GomokuRoom_ImplementsIGameRoom()
    {
        var room = new GomokuRoom("iGameRoom-test");

        Assert.IsAssignableFrom<IGameRoom>(room);
    }

    [Fact]
    public void GameRoom_Enter_IsVirtualOverride()
    {
        MethodInfo? enterMethod = typeof(GomokuRoom).GetMethod(
            nameof(GomokuRoom.Enter),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(GameSession)],
            modifiers: null);

        Assert.NotNull(enterMethod);
        // GetBaseDefinition()이 자기 자신과 다르면 override 메서드임
        Assert.NotEqual(enterMethod, enterMethod!.GetBaseDefinition());
    }
}
