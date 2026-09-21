using Core.Api.Application.Interfaces;
using Core.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Infrastructure.Persistence;

public sealed class AuditRepository(CoreDbContext db) : IAuditRepository
{
    public async Task<IReadOnlyList<AuditRecord>> ListAsync(string? actor, string? operation, string? correlation, DateTimeOffset? from, DateTimeOffset? to, CancellationToken token)
    {
        var query = db.AuditRecords.AsQueryable();
        if (!string.IsNullOrWhiteSpace(actor)) query = query.Where(record => record.ActorId == actor);
        if (!string.IsNullOrWhiteSpace(operation)) query = query.Where(record => record.Action == operation);
        if (!string.IsNullOrWhiteSpace(correlation)) query = query.Where(record => record.CorrelationId == correlation);
        if (from is not null) query = query.Where(record => record.At >= from);
        if (to is not null) query = query.Where(record => record.At <= to);
        return await query.OrderByDescending(record => record.At).ToListAsync(token);
    }
}
