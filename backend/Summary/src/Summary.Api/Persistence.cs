using Microsoft.EntityFrameworkCore;

public sealed class SummaryDbContext(DbContextOptions<SummaryDbContext> options) : DbContext(options)
{
    public DbSet<InboxEvent> InboxEvents => Set<InboxEvent>(); public DbSet<ProjectedEntry> ProjectedEntries => Set<ProjectedEntry>(); public DbSet<DailySummary> DailySummaries => Set<DailySummary>();
    protected override void OnModelCreating(ModelBuilder model) { model.Entity<InboxEvent>().HasKey(x => x.EventId); model.Entity<ProjectedEntry>().HasKey(x => x.Id); model.Entity<ProjectedEntry>().Property(x => x.Amount).HasPrecision(18, 2); model.Entity<DailySummary>().HasKey(x => x.Date); model.Entity<DailySummary>().Property(x => x.Credits).HasPrecision(18, 2); model.Entity<DailySummary>().Property(x => x.Debits).HasPrecision(18, 2); model.Entity<DailySummary>().Property(x => x.Balance).HasPrecision(18, 2); }
}
public sealed class InboxEvent { public Guid EventId { get; set; } public int Version { get; set; } public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow; }
public sealed class ProjectedEntry { public Guid Id { get; set; } public decimal Amount { get; set; } public string Type { get; set; } = ""; public string Description { get; set; } = ""; public DateOnly BusinessDate { get; set; } public int Version { get; set; } public bool Deleted { get; set; } }
public sealed class DailySummary { public DateOnly Date { get; set; } public decimal Credits { get; set; } public decimal Debits { get; set; } public decimal Balance { get; set; } public DateTimeOffset AsOf { get; set; } public string FreshnessStatus { get; set; } = "current"; }
public sealed record ProjectionEvent(Guid EventId, string Name, int Version, ProjectedEntryPayload Entry);
public sealed record ProjectedEntryPayload(Guid Id, decimal Amount, string Type, string Description, DateOnly BusinessDate, int Version, bool Deleted);
