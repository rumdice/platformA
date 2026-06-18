using System.Buffers.Binary;
using Google.Protobuf;
using PlatformA.Game.Gomoku.Network;
using PlatformA.Library.Game.Core;
using PlatformA.Library.Packets;
using ProtoPacket = PlatformA.Library.Packets.Packet;

namespace PlatformA.Game.Gomoku.Core
{
    public enum GomokuGameState { WaitingPlayers, InProgress, Finished }

    /// <summary>
    /// 오목 게임 방. 최대 2명, 매칭 완료 후 자동으로 게임을 시작합니다.
    /// 모든 상태 변경은 Push()를 통해 직렬화됩니다.
    /// </summary>
    public class GomokuRoom : GameRoom
    {
        public GomokuGameState GameState { get; private set; } = GomokuGameState.WaitingPlayers;

        private Board _board = new Board();
        private TurnManager? _turn;

        /// <summary>플레이어 입장. 2명이 모이면 게임을 자동 시작합니다.</summary>
        public new void Enter(GomokuSession session)
        {
            base.Enter(session);
            if (Sessions.Count == 2)
                StartGame();
        }

        private void StartGame()
        {
            var sessions = Sessions;
            int p1 = sessions[0].SessionId;
            int p2 = sessions[1].SessionId;

            _turn = new TurnManager(p1, p2);
            GameState = GomokuGameState.InProgress;

            Broadcast(BuildPacket(new ProtoPacket
            {
                SGameStart = new SGameStart
                {
                    Player1Id = p1,
                    Player2Id = p2,
                    FirstTurnPlayerId = _turn.CurrentTurnPlayerId,
                }
            }));
            Console.WriteLine($"[GomokuRoom {RoomId}] 게임 시작 — P1={p1} vs P2={p2}");
        }

        /// <summary>돌 놓기 처리. JobQueue 내부에서만 호출해야 합니다.</summary>
        public void HandlePlaceStone(GomokuSession session, int x, int y)
        {
            if (GameState != GomokuGameState.InProgress || _turn == null) return;

            if (!_turn.IsCurrentTurn(session.SessionId))
            {
                Console.WriteLine($"[GomokuRoom {RoomId}] 턴이 아닌 플레이어 요청 무시: {session.SessionId}");
                return;
            }

            StoneColor color = session.SessionId == _turn.Player1Id
                ? StoneColor.StoneBlack
                : StoneColor.StoneWhite;

            if (!_board.PlaceStone(x, y, color))
            {
                Console.WriteLine($"[GomokuRoom {RoomId}] 잘못된 위치: ({x},{y})");
                return;
            }

            _turn.NextTurn();

            Broadcast(BuildPacket(new ProtoPacket
            {
                SBoardUpdate = new SBoardUpdate
                {
                    X = x,
                    Y = y,
                    Color = color,
                    NextTurnPlayerId = _turn.CurrentTurnPlayerId,
                }
            }));

            if (WinChecker.CheckWin(_board, x, y, color))
                FinishGame(session.SessionId, GameOverReason.FiveInRow);
        }

        /// <summary>연결 끊김으로 인한 게임 종료 처리.</summary>
        public void HandleDisconnect(GomokuSession session)
        {
            if (GameState != GomokuGameState.InProgress || _turn == null) return;
            int winnerId = _turn.GetOpponentId(session.SessionId);
            FinishGame(winnerId, GameOverReason.Disconnect);
        }

        private void FinishGame(int winnerId, GameOverReason reason)
        {
            GameState = GomokuGameState.Finished;
            Broadcast(BuildPacket(new ProtoPacket
            {
                SGameOver = new SGameOver { WinnerId = winnerId, Reason = reason }
            }));
            Console.WriteLine($"[GomokuRoom {RoomId}] 게임 종료 — 승자={winnerId} 이유={reason}");
        }

        private static byte[] BuildPacket(ProtoPacket envelope)
        {
            byte[] body = envelope.ToByteArray();
            ushort size = (ushort)(2 + body.Length);
            byte[] buf = new byte[size];
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), size);
            body.CopyTo(buf, 2);
            return buf;
        }
    }
}
