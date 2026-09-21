using Core.Api.Application.Interfaces;
using Core.Api.Domain.Entities;

namespace Core.Api.Application.Services;

public sealed class AuditService(IAuditRepository repository)
{
    public Task<IReadOnlyList<AuditRecord>> ListAsync(
        string? actor,
        string? operation,
        string? correlation,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken token) => repository.ListAsync(actor, operation, correlation, from, to, token);
}
