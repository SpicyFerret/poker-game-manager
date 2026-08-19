using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Tables;

internal sealed class BlindLevelConfiguration : IEntityTypeConfiguration<BlindLevel>
{
    public void Configure(EntityTypeBuilder<BlindLevel> builder)
    {
        builder.HasKey(l => l.Id);

        builder.HasIndex(l => new { l.TableId, l.Order }).IsUnique();

        builder.HasOne<PokerTable>()
            .WithMany()
            .HasForeignKey(l => l.TableId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TableClockConfiguration : IEntityTypeConfiguration<TableClock>
{
    public void Configure(EntityTypeBuilder<TableClock> builder)
    {
        builder.HasKey(c => c.Id);

        // At most one clock per table, and only when the table has blind levels
        // at all — the clock is optional.
        builder.HasIndex(c => c.TableId).IsUnique();

        builder.Ignore(c => c.IsPaused);

        builder.HasOne<PokerTable>()
            .WithMany()
            .HasForeignKey(c => c.TableId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
