using Microsoft.EntityFrameworkCore;
using PlatformA.MySqlDB.Lib.DBWebApp;
using PlatformA.MySqlDB.Lib.DBWebApp.Entities;

namespace PlatformA.Auth.API.Services
{
    public class PlayerService
    {
        private readonly IDbContextFactory<DbWebAppContext> _contextFactory;

        public PlayerService(IDbContextFactory<DbWebAppContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// 로그인 처리.
        /// - 신규 유저: DB에 계정을 생성하고 playerId 반환 (학습용 자동 회원가입)
        /// - 기존 유저: 비밀번호 검증 후 playerId 반환, 실패 시 null
        /// </summary>
        public async Task<int?> LoginAsync(string username, string password)
        {
            await using var db = await _contextFactory.CreateDbContextAsync();

            var player = await db.Players
                .FirstOrDefaultAsync(p => p.Username == username);

            if (player == null)
            {
                // 신규 유저 자동 등록
                var newPlayer = new Player
                {
                    Username = username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    CreatedAt = DateTime.UtcNow
                };
                db.Players.Add(newPlayer);
                await db.SaveChangesAsync();
                return newPlayer.Id;
            }

            // 기존 유저 비밀번호 검증
            if (!BCrypt.Net.BCrypt.Verify(password, player.PasswordHash))
                return null;

            return player.Id;
        }
    }
}
