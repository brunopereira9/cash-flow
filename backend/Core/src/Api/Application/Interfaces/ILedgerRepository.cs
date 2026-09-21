using Core.Api.Domain.Entities;

namespace Core.Api.Application.Interfaces;

public interface ILedgerRepository
{
    Task<LedgerEntry?> FindByIdempotencyAsync(string actor, string key, CancellationToken token);
    Task<LedgerEntry?> FindAsync(Guid id, CancellationToken token);
    Task<IReadOnlyList<LedgerEntry>> ListAsync(DateOnly? date, CancellationToken token);
    Task AddAsync(LedgerEntry entry, string key, string actor, string correlation, CancellationToken token);
    Task UpdateAsync(LedgerEntry entry, string actor, string correlation, CancellationToken token);
    Task DeleteAsync(LedgerEntry entry, string actor, string correlation, CancellationToken token);
}
