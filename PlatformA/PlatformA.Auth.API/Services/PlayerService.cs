using Microsoft.EntityFrameworkCore;
using PlatformA.MySqlDB.Lib.DBWebApp;
using PlatformA.MySqlDB.Lib.DBWebApp.Entities;

namespace PlatformA.Auth.API.Services
{
    public class PlayerService
    {
        private readonly IDbContextFactory<DbWebAppContext> _contextFactory;
        private readonly ILogger<PlayerService> _logger;

        public PlayerService(
            IDbContextFactory<DbWebAppContext> contextFactory,
            ILogger<PlayerService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <summary>
        /// 로그인 처리.
        /// - 신규 유저: DB에 계정을 생성하고 playerId 반환 (학습용 자동 회원가입)
        /// - 기존 유저: 비밀번호 검증 후 playerId 반환, 실패 시 null
        /// </summary>
        public async Task<int?> LoginAsync(string username, string password)
        {
            try
            {
                await using var db = await _contextFactory.CreateDbContextAsync();


                var player = await db.Players
                    .FirstOrDefaultAsync(p => p.Username == username);


                if (player == null)
                {
                    Console.WriteLine("[PlayerService] 신규 플레이어 등록");
                    var newPlayer = new Player
                    {
                        Username = username,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                        CreatedAt = DateTime.UtcNow
                    };
                    db.Players.Add(newPlayer);
                    await db.SaveChangesAsync();

                    _logger.LogInformation("[PlayerService] 신규 플레이어 등록. Username: {Username}, PlayerId: {PlayerId}",
                        username, newPlayer.Id);
                    return newPlayer.Id;
                }

                if (!BCrypt.Net.BCrypt.Verify(password, player.PasswordHash))
                {
                    _logger.LogWarning("[PlayerService] 비밀번호 불일치. Username: {Username}", username);
                    return null;
                }

                _logger.LogInformation("[PlayerService] 기존 플레이어 인증 성공. Username: {Username}, PlayerId: {PlayerId}",
                    username, player.Id);
                return player.Id;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlayerService] 로그인 처리 중 오류: {ex.Message}");
            }
            return 0;
            
        }
    }
}
