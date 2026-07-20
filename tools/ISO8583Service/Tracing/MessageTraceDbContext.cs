using Microsoft.EntityFrameworkCore;

namespace ISO8583Service.Tracing;

/// <summary>
/// EF Core DbContext for the message trace database.
/// Uses PostgreSQL via Npgsql.
/// </summary>
public sealed class MessageTraceDbContext : DbContext
{
    public MessageTraceDbContext(DbContextOptions<MessageTraceDbContext> options)
        : base(options) { }

    public DbSet<TracedMessage> TracedMessages => Set<TracedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TracedMessage>(entity =>
        {
            entity.ToTable("traced_messages");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).UseIdentityColumn();

            entity.Property(e => e.Timestamp).IsRequired();

            entity.Property(e => e.TraceType)
                  .IsRequired()
                  .HasMaxLength(16);

            entity.Property(e => e.Mti).HasMaxLength(4);

            entity.Property(e => e.RawHex).HasMaxLength(500);

            entity.Property(e => e.ParsedHex).HasMaxLength(500);

            entity.Property(e => e.ResponseHex).HasMaxLength(500);

            entity.Property(e => e.HandlerName).HasMaxLength(128);

            entity.Property(e => e.ErrorMessage).HasMaxLength(1024);

            // Composite index for common queries: time range + trace type
            entity.HasIndex(e => new { e.Timestamp, e.TraceType });

            // Index for per-connection queries
            entity.HasIndex(e => e.ConnectionNumber);

            // Index for MTI-based filtering
            entity.HasIndex(e => e.Mti);
        });
    }
}
