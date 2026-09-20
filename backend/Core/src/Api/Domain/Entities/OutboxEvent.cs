namespace Core.Api.Domain.Entities;

public sealed class OutboxEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Payload { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
}
