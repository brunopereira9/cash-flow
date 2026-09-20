namespace CashFlow.Core.Infrastructure;

using Microsoft.EntityFrameworkCore;

public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options) : DbContext(options)
{
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();
    public DbSet<CreationAttempt> CreationAttempts => Set<CreationAttempt>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<LedgerEntry>().HasKey(x => x.Id); model.Entity<LedgerEntry>().Property(x => x.Amount).HasPrecision(18, 2);
        model.Entity<AuditRecord>().HasKey(x => x.Id); model.Entity<CreationAttempt>().HasKey(x => x.Id);
        model.Entity<CreationAttempt>().HasIndex(x => new { x.ActorId, x.Key }).IsUnique();
        model.Entity<OutboxEvent>().HasKey(x => x.EventId); model.Entity<OutboxEvent>().HasIndex(x => x.PublishedAt);
    }
}
