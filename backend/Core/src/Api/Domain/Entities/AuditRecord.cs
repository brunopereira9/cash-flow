namespace CashFlow.Core.Domain;

public sealed class AuditRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EntryId { get; set; }
    public string Action { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
