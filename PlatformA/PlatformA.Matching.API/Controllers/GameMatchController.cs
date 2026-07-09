using Microsoft.AspNetCore.Mvc;
using PlatformA.Library.Common;
using PlatformA.Matching.API.Models;
using PlatformA.Matching.API.Services;

namespace PlatformA.Matching.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameMatchController : ControllerBase
    {
        private readonly GameMatchService _matchService;

        public GameMatchController(GameMatchService matchService)
        {
            _matchService = matchService;
        }

        /// <summary>
        /// Lobby 서버 전용: 매칭 요청 및 즉시 게임 서버 접속 정보 반환.
        /// JWT 인증 없이 Lobby 서버 내부에서 호출됩니다.
        /// </summary>
        [HttpPost("request")]
        public async Task<IActionResult> RequestMatchFromLobby([FromBody] MatchRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Message = "잘못된 요청입니다." });

            MatchResultDto? result = await _matchService.TryMatchAsync(dto.UserId, dto.GameType);
            if (result == null)
                return Accepted(new { Message = "매칭 대기 중입니다." });

            return Ok(new { result.Host, result.Port, result.RoomId });
        }

        /// <summary>
        /// Lobby 서버 전용: 매칭 취소. JWT 없음, body에 userId/gameType을 전달합니다.
        /// </summary>
        [HttpPost("cancel")]
        public async Task<IActionResult> CancelMatchInternal([FromBody] CancelMatchRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Message = "잘못된 요청입니다." });

            bool removed = await _matchService.CancelMatchAsync(dto.UserId, dto.GameType);
            return removed
                ? Ok(new { Message = "매칭이 취소되었습니다." })
                : NotFound(new { Message = "대기열에서 찾을 수 없습니다." });
        }

        /// <summary>
        /// Lobby 서버 전용: 대기열 상태 조회. JWT 없음, userId 경로 파라미터와 gameType 쿼리 파라미터를 사용합니다.
        /// </summary>
        [HttpGet("status/{userId:int}")]
        public async Task<IActionResult> GetStatusInternal(int userId, [FromQuery] string gameType = "gomoku")
        {
            if (userId <= 0)
                return BadRequest(new { Message = "유효하지 않은 사용자 ID입니다." });

            (long rank, long total) = await _matchService.GetQueueStatusAsync(userId, gameType);
            if (rank < 0)
                return NotFound(new { Message = "매칭 대기열에 없습니다." });

            return Ok(new { Rank = rank + 1, Total = total });
        }

        /// <summary>
        /// 게임 서버 전용: 두 플레이어가 모두 입장했음을 알리고 Status를 InProgress로 전환합니다.
        /// 인증 불필요 (내부 서비스 간 통신).
        /// </summary>
        [HttpPost("start")]
        public async Task<IActionResult> NotifyMatchStarted([FromBody] MatchStartNotifyDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Message = "잘못된 요청입니다." });

            bool updated = await _matchService.NotifyMatchStartedAsync(dto.RoomId);
            if (!updated)
                return NotFound(new { Message = "해당 방의 매칭 기록을 찾을 수 없습니다." });

            return Ok(new { Message = "게임 시작이 기록되었습니다." });
        }

        /// <summary>
        /// 게임 서버 전용: 게임 종료 후 결과를 보고합니다. 인증 불필요 (내부 서비스 간 통신).
        /// </summary>
        [HttpPost("result")]
        public async Task<IActionResult> ReportMatchResult([FromBody] MatchResultReportDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Message = "잘못된 요청입니다." });

            bool updated = await _matchService.UpdateMatchResultAsync(dto.RoomId, dto.WinnerId, dto.Reason);
            if (!updated)
                return NotFound(new { Message = "해당 방의 매칭 기록을 찾을 수 없습니다." });

            return Ok(new { Message = "결과가 기록되었습니다." });
        }

        /// <summary>
        /// 매칭 이력 조회: 인증된 플레이어의 최근 20건 매칭 이력을 반환합니다.
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            int playerId = ExtractPlayerId();
            if (playerId <= 0)
                return Unauthorized(new { Message = "유효하지 않은 토큰입니다." });

            List<MatchHistoryDto> history = await _matchService.GetMatchHistoryAsync(playerId);
            return Ok(history);
        }

        /// <summary>
        /// ELO 레이팅 조회: 특정 플레이어의 현재 레이팅과 전적을 반환합니다.
        /// 인증 불필요 (공개 정보).
        /// </summary>
        [HttpGet("rating/{userId:int}")]
        public async Task<IActionResult> GetRating(int userId)
        {
            if (userId <= 0)
                return BadRequest(new { Message = "유효하지 않은 사용자 ID입니다." });

            PlayerRatingDto? rating = await _matchService.GetPlayerRatingDtoAsync(userId);
            if (rating == null)
                return Ok(new PlayerRatingDto
                {
                    PlayerId = userId,
                    Rating = Consts.DEFAULT_PLAYER_RATING,
                    WinCount = 0,
                    LoseCount = 0,
                    DrawCount = 0,
                });
            return Ok(rating);
        }

        private int ExtractPlayerId()
        {
            string authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer"))
                return 0;
            string jwtToken = authHeader.Substring(7);
            return TokenManager.ValidateTokenAndGetUserId(jwtToken);
        }
    }

}
