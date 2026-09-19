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
public sealed class LedgerEntry { public LedgerEntry() { } public LedgerEntry(Guid id, decimal amount, string type, string description, DateOnly businessDate, int version, bool deleted) => (Id, Amount, Type, Description, BusinessDate, Version, Deleted) = (id, amount, type, description, businessDate, version, deleted); public Guid Id { get; set; } public decimal Amount { get; set; } public string Type { get; set; } = ""; public string Description { get; set; } = ""; public DateOnly BusinessDate { get; set; } public int Version { get; set; } public bool Deleted { get; set; } }
public sealed class AuditRecord { public Guid Id { get; set; } = Guid.NewGuid(); public Guid EntryId { get; set; } public string Action { get; set; } = ""; public string ActorId { get; set; } = ""; public string CorrelationId { get; set; } = ""; public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow; }
public sealed class CreationAttempt { public Guid Id { get; set; } = Guid.NewGuid(); public string ActorId { get; set; } = ""; public string Key { get; set; } = ""; public string Intent { get; set; } = ""; public Guid EntryId { get; set; } public LedgerEntry? Entry { get; set; } }
public sealed class OutboxEvent { public Guid EventId { get; set; } = Guid.NewGuid(); public string Name { get; set; } = ""; public int Version { get; set; } public string Payload { get; set; } = ""; public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow; public DateTimeOffset? PublishedAt { get; set; } }
public sealed record LedgerEventEnvelope(Guid EventId, string Name, int Version, LedgerEventEntry Entry);
public sealed record LedgerEventEntry(Guid Id, decimal Amount, string Type, string Description, DateOnly BusinessDate, int Version, bool Deleted);
