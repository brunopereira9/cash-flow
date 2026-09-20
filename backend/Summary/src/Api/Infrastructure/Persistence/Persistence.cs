namespace CashFlow.Summary.Infrastructure;

using Microsoft.EntityFrameworkCore;

public sealed class SummaryDbContext(DbContextOptions<SummaryDbContext> options) : DbContext(options)
{
    public DbSet<InboxEvent> InboxEvents => Set<InboxEvent>(); public DbSet<ProjectedEntry> ProjectedEntries => Set<ProjectedEntry>(); public DbSet<DailySummary> DailySummaries => Set<DailySummary>();
    protected override void OnModelCreating(ModelBuilder model) { model.Entity<InboxEvent>().HasKey(x => x.EventId); model.Entity<ProjectedEntry>().HasKey(x => x.Id); model.Entity<ProjectedEntry>().Property(x => x.Amount).HasPrecision(18, 2); model.Entity<DailySummary>().HasKey(x => x.Date); model.Entity<DailySummary>().Property(x => x.Credits).HasPrecision(18, 2); model.Entity<DailySummary>().Property(x => x.Debits).HasPrecision(18, 2); model.Entity<DailySummary>().Property(x => x.Balance).HasPrecision(18, 2); }
}
