using Core.Api.Application.Events;
using Core.Api.Application.Interfaces;
using Core.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Infrastructure.Persistence;

public sealed class LedgerRepository(CoreDbContext db, LedgerMutationFactory mutations) : ILedgerRepository
{
    public async Task<LedgerEntry?> FindByIdempotencyAsync(string actor, string key, CancellationToken token) =>
        (await db.CreationAttempts.Include(x => x.Entry).SingleOrDefaultAsync(x => x.ActorId == actor && x.Key == key, token))?.Entry;

    public Task<LedgerEntry?> FindAsync(Guid id, CancellationToken token) =>
        db.LedgerEntries.SingleOrDefaultAsync(entry => entry.Id == id, token);

    public async Task<IReadOnlyList<LedgerEntry>> ListAsync(DateOnly? date, CancellationToken token)
    {
        var query = db.LedgerEntries.Where(entry => !entry.Deleted);
        if (date.HasValue) query = query.Where(entry => entry.BusinessDate == date.Value);
        return await query.OrderBy(entry => entry.BusinessDate).ThenBy(entry => entry.Id).ToListAsync(token);
    }

    public async Task AddAsync(LedgerEntry entry, string key, string actor, string correlation, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        db.LedgerEntries.Add(entry);
        db.CreationAttempts.Add(new CreationAttempt { ActorId = actor, Key = key, Intent = Intent(entry), Entry = entry });
        mutations.Add(db, entry, "created", actor, correlation, "LedgerEntryCreated.v1");
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    public async Task UpdateAsync(LedgerEntry entry, string actor, string correlation, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        mutations.Add(db, entry, "updated", actor, correlation, "LedgerEntryUpdated.v1");
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    public async Task DeleteAsync(LedgerEntry entry, string actor, string correlation, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        mutations.Add(db, entry, "deleted", actor, correlation, "LedgerEntryDeleted.v1");
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    private static string Intent(LedgerEntry entry) =>
        $"{entry.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{entry.Type}|{entry.Description}|{entry.BusinessDate:yyyy-MM-dd}";
}
