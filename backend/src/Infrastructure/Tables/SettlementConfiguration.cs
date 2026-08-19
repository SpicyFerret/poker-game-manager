using Domain.ChipSets;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Tables;

internal sealed class FinalCountConfiguration : IEntityTypeConfiguration<FinalCount>
{
    public void Configure(EntityTypeBuilder<FinalCount> builder)
    {
        builder.HasKey(c => c.Id);

        // One count per player per chip. Re-reporting overwrites rather than
        // adding, so a corrected count replaces the wrong one.
        builder.HasIndex(c => new { c.TablePlayerId, c.ChipDenominationId }).IsUnique();

        builder.HasIndex(c => c.TableId);

        builder.HasOne<PokerTable>()
            .WithMany()
            .HasForeignKey(c => c.TableId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<TablePlayer>()
            .WithMany()
            .HasForeignKey(c => c.TablePlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ChipDenomination>()
            .WithMany()
            .HasForeignKey(c => c.ChipDenominationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
    public void Configure(EntityTypeBuilder<Settlement> builder)
    {
        builder.HasKey(s => s.Id);

        // One settlement per table: it is produced once and never recomputed.
        builder.HasIndex(s => s.TableId).IsUnique();

        builder.HasOne<PokerTable>()
            .WithMany()
            .HasForeignKey(s => s.TableId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Transfers)
            .WithOne()
            .HasForeignKey(t => t.SettlementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SettlementTransferConfiguration : IEntityTypeConfiguration<SettlementTransfer>
{
    public void Configure(EntityTypeBuilder<SettlementTransfer> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Amount).HasPrecision(18, 2);

        // Cascade on both ends. A transfer names two people and cannot be nulled
        // the way the ledger's counterparty can, and Restrict would deadlock the
        // cascade that removes a whole table — exactly the trap the ledger's
        // counterparty foreign key already fell into. A player row only ever
        // disappears as part of its table going, and the transfers go with it.
        builder.HasOne<TablePlayer>()
            .WithMany()
            .HasForeignKey(t => t.FromPlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<TablePlayer>()
            .WithMany()
            .HasForeignKey(t => t.ToPlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TableResultConfiguration : IEntityTypeConfiguration<TableResult>
{
    public void Configure(EntityTypeBuilder<TableResult> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Balance).HasPrecision(18, 2);

        builder.HasIndex(r => new { r.TableId, r.TablePlayerId }).IsUnique();

        builder.HasOne<PokerTable>()
            .WithMany()
            .HasForeignKey(r => r.TableId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<TablePlayer>()
            .WithMany()
            .HasForeignKey(r => r.TablePlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
