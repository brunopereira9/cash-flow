using Core.Api.Domain.Entities;

namespace Core.Api.Application.Interfaces;

public interface IAuditRepository
{
    Task<IReadOnlyList<AuditRecord>> ListAsync(
        string? actor,
        string? operation,
        string? correlation,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken token);
}
