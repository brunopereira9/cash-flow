namespace CashFlow.Core.Domain;

public sealed class CreationAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ActorId { get; set; } = "";
    public string Key { get; set; } = "";
    public string Intent { get; set; } = "";
    public Guid EntryId { get; set; }
    public LedgerEntry? Entry { get; set; }
}
