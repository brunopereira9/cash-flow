namespace Summary.Api.Domain.Entities;

public sealed class DailySummary
{
    private DailySummary() { }

    public DateOnly Date { get; private set; }
    public decimal Credits { get; private set; }
    public decimal Debits { get; private set; }
    public decimal Balance { get; private set; }
    public DateTimeOffset AsOf { get; private set; }
    public string FreshnessStatus { get; private set; } = "current";

    public static DailySummary Create(DateOnly date, decimal credits, decimal debits, DateTimeOffset asOf)
    {
        var summary = new DailySummary { Date = date };
        return summary.Refresh(credits, debits, asOf);
    }

    public DailySummary Refresh(decimal credits, decimal debits, DateTimeOffset asOf)
    {
        Credits = credits;
        Debits = debits;
        Balance = credits - debits;
        AsOf = asOf;
        FreshnessStatus = "current";
        return this;
    }

    public void MarkFreshness(string status) => FreshnessStatus = status;
}
