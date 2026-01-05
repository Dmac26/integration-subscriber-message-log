using Microsoft.EntityFrameworkCore;
using Wachter.IntegrationSubscriberMessageLog.Models;

public class IntegrationMessageLogDbContext : DbContext
{
    public IntegrationMessageLogDbContext(DbContextOptions<IntegrationMessageLogDbContext> options)
        : base(options)
    {
    }

    public DbSet<MessageLog>   MessageLog { get; set; } = null!;
}