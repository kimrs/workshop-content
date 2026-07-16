using Microsoft.EntityFrameworkCore;
using Mt.Persistence.Rows;

namespace Mt.Persistence;

/// <summary>The EF Core context for all workshop tables (§11).</summary>
public sealed class WorkshopDbContext(DbContextOptions<WorkshopDbContext> options) : DbContext(options)
{
    public DbSet<MigrationRow> Migrations => Set<MigrationRow>();

    public DbSet<OutboxRow> Outbox => Set<OutboxRow>();

    public DbSet<InboxRow> Inbox => Set<InboxRow>();

    public DbSet<ScheduledEventRow> ScheduledEvents => Set<ScheduledEventRow>();

    public DbSet<MessageRow> Messages => Set<MessageRow>();

    public DbSet<ExternalIdRow> ExternalIds => Set<ExternalIdRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var migrations = modelBuilder.Entity<MigrationRow>();
        migrations.ToTable("Migrations");
        migrations.HasKey(m => m.Id);
        migrations.Property(m => m.OrganizationNumber).IsRequired();
        // Filtered unique index: an org can be re-migrated once its migration is terminal (§11).
        migrations.HasIndex(m => m.OrganizationNumber)
            .IsUnique()
            .HasFilter($"\"State\" NOT IN ({(int)MigrationState.Completed}, {(int)MigrationState.Cancelled})");

        var outbox = modelBuilder.Entity<OutboxRow>();
        outbox.ToTable("Outbox");
        outbox.HasKey(o => o.Id);
        outbox.HasIndex(o => new { o.ProcessedAt, o.FailedAt });

        var inbox = modelBuilder.Entity<InboxRow>();
        inbox.ToTable("Inbox");
        inbox.HasKey(i => new { i.MigrationId, i.DomainEvent, i.Attempt });

        var scheduled = modelBuilder.Entity<ScheduledEventRow>();
        scheduled.ToTable("ScheduledEvents");
        scheduled.HasKey(s => s.Id);
        scheduled.HasIndex(s => new { s.ProcessedAt, s.ScheduledAt });

        var messages = modelBuilder.Entity<MessageRow>();
        messages.ToTable("Messages");
        messages.HasKey(m => m.Id);
        messages.HasIndex(m => m.ConsumedAt);

        var externalIds = modelBuilder.Entity<ExternalIdRow>();
        externalIds.ToTable("ExternalIds");
        externalIds.HasKey(e => new { e.MigrationId, e.System, e.Name });
        // Filtered unique index: cancelled migrations release their ids for reuse (spec 8).
        externalIds.HasIndex(e => new { e.System, e.Name, e.Value })
            .IsUnique()
            .HasFilter("NOT \"IsCancelled\"");
        externalIds.HasOne<MigrationRow>()
            .WithMany()
            .HasForeignKey(e => e.MigrationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
