namespace CashFlow.Summary.Domain;

public sealed class InboxEvent
{
    private InboxEvent() { }

    private InboxEvent(Guid eventId, int version) => (EventId, Version) = (eventId, version);

    public Guid EventId { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; } = DateTimeOffset.UtcNow;

    public static InboxEvent Record(Guid eventId, int version) => new(eventId, version);
}
