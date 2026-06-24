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
        /// 매칭 요청: Bearer JWT로 인증 후 매칭 대기열에 진입합니다.
        /// </summary>
        [HttpPost("RequestMatch")]
        public async Task<IActionResult> RequestMatch()
        {
            int playerId = ExtractPlayerId();
            if (playerId <= 0)
                return Unauthorized(new { Message = "유효하지 않은 토큰입니다." });

            await _matchService.AddPlayerToQueueAsync(playerId);
            return Ok(new { Message = "매칭 대기열에 성공적으로 진입했습니다." });
        }

        /// <summary>
        /// 매칭 취소: 대기열에서 본인을 제거합니다.
        /// </summary>
        [HttpDelete("CancelMatch")]
        public async Task<IActionResult> CancelMatch()
        {
            int playerId = ExtractPlayerId();
            if (playerId <= 0)
                return Unauthorized(new { Message = "유효하지 않은 토큰입니다." });

            bool removed = await _matchService.RemovePlayerFromQueueAsync(playerId);
            return removed
                ? Ok(new { Message = "매칭이 취소되었습니다." })
                : NotFound(new { Message = "대기열에서 찾을 수 없습니다." });
        }

        /// <summary>
        /// 대기열 상태 조회: 본인의 순위와 전체 대기 인원을 반환합니다.
        /// </summary>
        [HttpGet("Status")]
        public async Task<IActionResult> GetStatus()
        {
            int playerId = ExtractPlayerId();
            if (playerId <= 0)
                return Unauthorized(new { Message = "유효하지 않은 토큰입니다." });

            long rank = await _matchService.GetQueueRankAsync(playerId);
            if (rank < 0)
                return NotFound(new { Message = "매칭 대기열에 없습니다." });

            long total = await _matchService.GetQueueLengthAsync();
            return Ok(new { Rank = rank + 1, Total = total });
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
