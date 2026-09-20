namespace CashFlow.Core.Domain;

public sealed class LedgerEntry
{
    public LedgerEntry() { }
    public LedgerEntry(Guid id, decimal amount, string type, string description, DateOnly businessDate, int version, bool deleted) => (Id, Amount, Type, Description, BusinessDate, Version, Deleted) = (id, amount, type, description, businessDate, version, deleted);
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public DateOnly BusinessDate { get; set; }
    public int Version { get; set; }
    public bool Deleted { get; set; }
}
