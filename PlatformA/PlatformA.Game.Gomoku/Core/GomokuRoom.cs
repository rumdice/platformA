using System.Buffers.Binary;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using PlatformA.Game.Gomoku.Network;
using PlatformA.Library.Common;
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

        private readonly string _roomId;
        private Board _board = new Board();
        private TurnManager? _turn;

        // 결과 보고용 HttpClient — 인스턴스 재사용으로 소켓 고갈 방지
        private static readonly HttpClient _httpClient = new HttpClient
        {
            BaseAddress = new Uri(Consts.MATCHING_API_BASE_URL),
            Timeout = TimeSpan.FromSeconds(5),
        };

        public GomokuRoom(string roomId)
        {
            _roomId = roomId;
        }

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
            Console.WriteLine($"[GomokuRoom {_roomId}] 게임 시작 — P1={p1} vs P2={p2}");

            // Matching.API에 게임 시작 알림 (fire-and-forget)
            _ = ReportMatchStartAsync();

            // 턴 타임아웃 감시 루프 — 1초 간격으로 Push() 안에서 체크
            _ = Task.Run(async () =>
            {
                while (GameState == GomokuGameState.InProgress)
                {
                    await Task.Delay(1000);
                    Push(() =>
                    {
                        if (GameState != GomokuGameState.InProgress || _turn == null)
                            return;
                        if (_turn.IsTimeout())
                        {
                            int winner = _turn.GetOpponentId(_turn.CurrentTurnPlayerId);
                            FinishGame(winner, GameOverReason.Timeout);
                        }
                    });
                }
            });
        }

        /// <summary>돌 놓기 처리. JobQueue 내부에서만 호출해야 합니다.</summary>
        public void HandlePlaceStone(GomokuSession session, int x, int y)
        {
            if (GameState != GomokuGameState.InProgress || _turn == null)
                return;

            if (!_turn.IsCurrentTurn(session.SessionId))
            {
                Console.WriteLine($"[GomokuRoom {_roomId}] 턴이 아닌 플레이어 요청 무시: {session.SessionId}");
                return;
            }

            StoneColor color = session.SessionId == _turn.Player1Id
                ? StoneColor.StoneBlack
                : StoneColor.StoneWhite;

            if (!_board.PlaceStone(x, y, color))
            {
                Console.WriteLine($"[GomokuRoom {_roomId}] 잘못된 위치: ({x},{y})");
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
            else if (_board.IsFull())
                FinishGame(0, GameOverReason.Draw);  // 0 = 무승부 (특정 승자 없음)
        }

        /// <summary>연결 끊김으로 인한 게임 종료 처리.</summary>
        public void HandleDisconnect(GomokuSession session)
        {
            if (GameState == GomokuGameState.Finished)
                return;

            if (GameState == GomokuGameState.WaitingPlayers)
            {
                // 게임 시작 전 퇴장 — 남은 플레이어에게 취소 알림 후 방 메모리 정리
                Broadcast(BuildPacket(new ProtoPacket
                {
                    SGameOver = new SGameOver
                    {
                        WinnerId = 0,
                        Reason = GameOverReason.Disconnect,
                        LobbyUrl = Consts.LOBBY_HUB_URL,
                    }
                }));
                GomokuRoomManager.Instance.Remove(_roomId);
                return;
            }

            int winnerId = _turn!.GetOpponentId(session.SessionId);
            FinishGame(winnerId, GameOverReason.Disconnect);
        }

        private void FinishGame(int winnerId, GameOverReason reason)
        {
            // 중복 호출 방지 (타임아웃 루프 + 돌 놓기 + 연결 끊김이 동시에 올 수 있음)
            if (GameState != GomokuGameState.InProgress)
                return;

            GameState = GomokuGameState.Finished;
            Broadcast(BuildPacket(new ProtoPacket
            {
                SGameOver = new SGameOver
                {
                    WinnerId = winnerId,
                    Reason = reason,
                    LobbyUrl = Consts.LOBBY_HUB_URL,
                }
            }));
            Console.WriteLine($"[GomokuRoom {_roomId}] 게임 종료 — 승자={winnerId} 이유={reason}");

            // Matching.API에 결과 보고 (fire-and-forget, 실패해도 게임 흐름 방해 없음)
            _ = ReportMatchResultAsync(winnerId, reason);

            // 방 메모리 정리
            GomokuRoomManager.Instance.Remove(_roomId);
        }

        private async Task ReportMatchStartAsync()
        {
            try
            {
                var payload = new { RoomId = _roomId };
                string json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage resp = await _httpClient.PostAsync("/api/gamematch/start", content);
                if (!resp.IsSuccessStatusCode)
                    Console.WriteLine($"[GomokuRoom {_roomId}] 게임 시작 보고 실패: HTTP {(int)resp.StatusCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GomokuRoom {_roomId}] 게임 시작 보고 예외: {ex.Message}");
            }
        }

        private async Task ReportMatchResultAsync(int winnerId, GameOverReason reason)
        {
            try
            {
                var payload = new
                {
                    RoomId = _roomId,
                    WinnerId = winnerId,
                    Reason = reason.ToString(),
                };
                string json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage resp = await _httpClient.PostAsync("/api/gamematch/result", content);
                if (!resp.IsSuccessStatusCode)
                    Console.WriteLine($"[GomokuRoom {_roomId}] 결과 보고 실패: HTTP {(int)resp.StatusCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GomokuRoom {_roomId}] 결과 보고 예외: {ex.Message}");
            }
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
