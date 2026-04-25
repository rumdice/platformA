using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PlatformA.MySqlDB.Lib.DBWebApp;

namespace PlatformA.Auth.API.HealthChecks
{
    /// <summary>
    /// db_WebApp MariaDB 연결 상태를 확인합니다.
    /// IDbContextFactory를 사용해 짧은 수명의 컨텍스트로 연결을 테스트합니다.
    /// </summary>
    public class DbWebAppHealthCheck : IHealthCheck
    {
        private readonly IDbContextFactory<DbWebAppContext> _contextFactory;

        public DbWebAppHealthCheck(IDbContextFactory<DbWebAppContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            // test: claude 를 통해 뭔가작업을 해서 코드가 변경되었다 가정.
            try
            {
                await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
                bool canConnect = await db.Database.CanConnectAsync(cancellationToken);
                return canConnect
                    ? HealthCheckResult.Healthy("MariaDB db_WebApp 연결 정상")
                    : HealthCheckResult.Unhealthy("MariaDB db_WebApp 연결 실패");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("MariaDB db_WebApp 예외 발생", ex);
            }
        }
    }
}
