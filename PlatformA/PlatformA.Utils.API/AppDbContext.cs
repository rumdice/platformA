using Microsoft.EntityFrameworkCore;

namespace PlatformA.Utils.API
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<Models.DB.ShortUrl> ShortUrls { get; set; }
    }
}
