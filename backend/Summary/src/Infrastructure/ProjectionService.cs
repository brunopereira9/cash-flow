using Microsoft.EntityFrameworkCore;

public sealed class ProjectionService(SummaryDbContext db)
{
    public async Task<bool> ApplyAsync(ProjectionEvent message, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await db.InboxEvents.AnyAsync(x => x.EventId == message.EventId, token)) { CashFlowSummaryTelemetry.InboxDuplicates.Add(1); return false; }
        db.InboxEvents.Add(new InboxEvent { EventId = message.EventId, Version = message.Version });
        var current = await db.ProjectedEntries.SingleOrDefaultAsync(x => x.Id == message.Entry.Id, token);
        if (current is not null && current.Version >= message.Version) { await db.SaveChangesAsync(token); await transaction.CommitAsync(token); return false; }
        var affected = new HashSet<DateOnly> { message.Entry.BusinessDate };
        if (current is null) db.ProjectedEntries.Add(new ProjectedEntry { Id = message.Entry.Id, Amount = message.Entry.Amount, Type = message.Entry.Type, Description = message.Entry.Description, BusinessDate = message.Entry.BusinessDate, Version = message.Version, Deleted = message.Entry.Deleted });
        else { affected.Add(current.BusinessDate); current.Amount = message.Entry.Amount; current.Type = message.Entry.Type; current.Description = message.Entry.Description; current.BusinessDate = message.Entry.BusinessDate; current.Version = message.Version; current.Deleted = message.Entry.Deleted; }
        await db.SaveChangesAsync(token);
        foreach (var date in affected) await RecalculateAsync(date, token);
        await db.SaveChangesAsync(token); await transaction.CommitAsync(token); CashFlowSummaryTelemetry.ProjectionApplied.Add(1); return true;
    }
    private async Task RecalculateAsync(DateOnly date, CancellationToken token)
    {
        var entries = await db.ProjectedEntries.Where(x => x.BusinessDate == date && !x.Deleted).ToListAsync(token); var credits = entries.Where(x => string.Equals(x.Type, "credit", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount); var debits = entries.Where(x => string.Equals(x.Type, "debit", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
        var summary = await db.DailySummaries.FindAsync([date], token); if (summary is null) db.DailySummaries.Add(new DailySummary { Date = date, Credits = credits, Debits = debits, Balance = credits - debits, AsOf = DateTimeOffset.UtcNow }); else { summary.Credits = credits; summary.Debits = debits; summary.Balance = credits - debits; summary.AsOf = DateTimeOffset.UtcNow; summary.FreshnessStatus = "current"; }
    }
}
