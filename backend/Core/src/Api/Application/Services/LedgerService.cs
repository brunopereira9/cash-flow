using Core.Api.Application.Interfaces;
using Core.Api.Application.Models;
using Core.Api.Application.Validation;
using Core.Api.Domain.Entities;

namespace Core.Api.Application.Services;

public sealed class LedgerService(ILedgerRepository repository)
{
    public IReadOnlyDictionary<string, string[]> Validate(CreateEntryRequest request) => EntryValidator.Validate(request);

    public IReadOnlyDictionary<string, string[]> Validate(UpdateEntryRequest request) => EntryValidator.Validate(request);

    public Task<CreationAttempt?> FindByIdempotencyAsync(string actor, string key, CancellationToken token) =>
        repository.FindByIdempotencyAsync(actor, key, token);

    public Task<LedgerEntry?> FindAsync(Guid id, CancellationToken token) => repository.FindAsync(id, token);

    public Task<IReadOnlyList<LedgerEntry>> ListAsync(DateOnly? date, CancellationToken token) => repository.ListAsync(date, token);

    public Task AddAsync(LedgerEntry entry, string key, string actor, string correlation, CancellationToken token) =>
        repository.AddAsync(entry, key, actor, correlation, token);

    public Task UpdateAsync(LedgerEntry entry, string actor, string correlation, CancellationToken token) =>
        repository.UpdateAsync(entry, actor, correlation, token);

    public Task DeleteAsync(LedgerEntry entry, string actor, string correlation, CancellationToken token) =>
        repository.DeleteAsync(entry, actor, correlation, token);
}
