using Summary.Api.Domain.Events;

namespace Summary.Api.Domain.Entities;

public sealed class ProjectedEntry
{
    private ProjectedEntry() { }

    private ProjectedEntry(ProjectedEntryPayload payload) => Apply(payload);

    public Guid Id { get; private set; }
    public decimal Amount { get; private set; }
    public string Type { get; private set; } = "";
    public string Description { get; private set; } = "";
    public DateOnly BusinessDate { get; private set; }
    public int Version { get; private set; }
    public bool Deleted { get; private set; }

    public static ProjectedEntry Create(ProjectedEntryPayload payload) => new(payload);

    public void Apply(ProjectedEntryPayload payload)
    {
        if (payload.Id != Id && Id != Guid.Empty) throw new InvalidOperationException("Projected entry identity cannot change.");
        if (payload.Version <= Version) throw new InvalidOperationException("Projected entry version must advance.");
        Id = payload.Id;
        Amount = payload.Amount;
        Type = payload.Type;
        Description = payload.Description;
        BusinessDate = payload.BusinessDate;
        Version = payload.Version;
        Deleted = payload.Deleted;
    }
}
