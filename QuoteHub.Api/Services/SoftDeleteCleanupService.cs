using Microsoft.EntityFrameworkCore;
using QuoteHub.Api.Data;

namespace QuoteHub.Api.Services;

/// <summary>
/// Hosted background worker that periodically purges soft-deleted quotes older than 30 days.
/// Demonstrates creating an IServiceScope inside a Singleton BackgroundService to safely use Scoped DbContext.
/// </summary>
public class SoftDeleteCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<SoftDeleteCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PurgeExpiredQuotesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while purging expired soft-deleted quotes.");
            }
        }
    }

    public async Task<int> PurgeExpiredQuotesAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuoteDbContext>();

        var cutoff = DateTime.UtcNow - RetentionPeriod;
        var expired = await db.Quotes
            .IgnoreQueryFilters()
            .Where(q => q.IsDeleted && q.DeletedAtUtc != null && q.DeletedAtUtc < cutoff)
            .ToListAsync(ct);

        if (expired.Count == 0)
        {
            return 0;
        }

        db.Quotes.RemoveRange(expired);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Purged {Count} soft-deleted quotes older than {CutoffUtc}", expired.Count, cutoff);
        return expired.Count;
    }
}
