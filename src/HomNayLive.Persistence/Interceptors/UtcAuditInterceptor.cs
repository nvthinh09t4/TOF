using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HomNayLive.Persistence.Interceptors;

public class UtcAuditInterceptor : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider;

    public UtcAuditInterceptor(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            ApplyUtcDates(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            ApplyUtcDates(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void ApplyUtcDates(DbContext context)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                if (entry.Entity is null)
                {
                    continue;
                }

                var props = entry.Properties.Where(p => p.Metadata.Name is "CreatedAtUtc" or "UpdatedAtUtc");
                foreach (var prop in props)
                {
                    if (entry.State == EntityState.Added && prop.Metadata.Name == "CreatedAtUtc" && prop.CurrentValue is DateTime createdAt)
                    {
                        if (createdAt == default)
                        {
                            prop.CurrentValue = now;
                        }
                    }

                    if (prop.Metadata.Name == "UpdatedAtUtc")
                    {
                        prop.CurrentValue = now;
                    }
                }
            }
        }
    }
}
