using System.Text.Json;
using Core.Api.Domain.Entities;
using Core.Api.Domain.Events;
using Core.Api.Infrastructure.Persistence;

namespace Core.Api.Application.Events;

public sealed class LedgerMutationFactory
{
    public void Add(
        CoreDbContext db,
        LedgerEntry entry,
        string action,
        string actor,
        string correlation,
        string eventName)
    {
        db.AuditRecords.Add(new AuditRecord
        {
            EntryId = entry.Id,
            Action = action,
            ActorId = actor,
            CorrelationId = correlation
        });

        var eventId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(
            new LedgerEventEnvelope(
                eventId,
                eventName,
                entry.Version,
                new(
                    entry.Id,
                    entry.Amount,
                    entry.Type,
                    entry.Description,
                    entry.BusinessDate,
                    entry.Version,
                    entry.Deleted)));

        db.OutboxEvents.Add(new OutboxEvent
        {
            EventId = eventId,
            Name = eventName,
            Version = entry.Version,
            Payload = payload
        });
    }
}
