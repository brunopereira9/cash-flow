namespace CashFlow.Summary.Infrastructure;

using Microsoft.EntityFrameworkCore;

public sealed class ProjectionService(SummaryDbContext db)
{
    public async Task<bool> ApplyAsync(ProjectionEvent message, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (await db.InboxEvents.AnyAsync(x => x.EventId == message.EventId, token)) { CashFlowSummaryTelemetry.InboxDuplicates.Add(1); return false; }
        db.InboxEvents.Add(InboxEvent.Record(message.EventId, message.Version));
        var current = await db.ProjectedEntries.SingleOrDefaultAsync(x => x.Id == message.Entry.Id, token);
        if (current is not null && current.Version >= message.Version) { await db.SaveChangesAsync(token); await transaction.CommitAsync(token); return false; }
        var affected = new HashSet<DateOnly> { message.Entry.BusinessDate };
        if (current is null) db.ProjectedEntries.Add(ProjectedEntry.Create(message.Entry));
        else { affected.Add(current.BusinessDate); current.Apply(message.Entry); }
        await db.SaveChangesAsync(token);
        foreach (var date in affected) await RecalculateAsync(date, token);
        await db.SaveChangesAsync(token); await transaction.CommitAsync(token); CashFlowSummaryTelemetry.ProjectionApplied.Add(1); return true;
    }
    private async Task RecalculateAsync(DateOnly date, CancellationToken token)
    {
        var entries = await db.ProjectedEntries.Where(x => x.BusinessDate == date && !x.Deleted).ToListAsync(token); var credits = entries.Where(x => string.Equals(x.Type, "credit", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount); var debits = entries.Where(x => string.Equals(x.Type, "debit", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
        var summary = await db.DailySummaries.FindAsync([date], token); if (summary is null) db.DailySummaries.Add(DailySummary.Create(date, credits, debits, DateTimeOffset.UtcNow)); else summary.Refresh(credits, debits, DateTimeOffset.UtcNow);
    }
}
