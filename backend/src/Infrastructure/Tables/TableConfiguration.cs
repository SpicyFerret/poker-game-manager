using Domain.Championships;
using Domain.ChipSets;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Tables;

internal sealed class PokerTableConfiguration : IEntityTypeConfiguration<PokerTable>
{
    public void Configure(EntityTypeBuilder<PokerTable> builder)
    {
        builder.ToTable("tables");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).HasMaxLength(80).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.JoinPolicy).HasConversion<string>().HasMaxLength(20);

        builder.Property(t => t.BuyIn).HasPrecision(18, 2);
        builder.Property(t => t.Rebuy).HasPrecision(18, 2);
        builder.Property(t => t.MoneyPerUnit).HasPrecision(18, 6);

        builder.Property(t => t.JoinCode).HasMaxLength(InviteCode.Length);
        builder.HasIndex(t => t.JoinCode).IsUnique().HasFilter("join_code IS NOT NULL");

        // Computed from other columns; nothing to store.
        builder.Ignore(t => t.BuyInUnits);
        builder.Ignore(t => t.RebuyUnits);

        builder.HasIndex(t => new { t.ChampionshipId, t.Status });

        builder.HasOne<Championship>()
            .WithMany()
            .HasForeignKey(t => t.ChampionshipId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade: deleting a chip case must not take the history of
        // every table played with it. The chip set delete fails instead, which is
        // the honest answer.
        builder.HasOne<ChipSet>()
            .WithMany()
            .HasForeignKey(t => t.ChipSetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TablePlayerConfiguration : IEntityTypeConfiguration<TablePlayer>
{
    public void Configure(EntityTypeBuilder<TablePlayer> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(p => new { p.TableId, p.UserId }).IsUnique();

        builder.HasOne<PokerTable>()
            .WithMany()
            .HasForeignKey(p => p.TableId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.MoneyAmount).HasPrecision(18, 2);
        builder.Property(e => e.Note).HasMaxLength(200);

        builder.HasIndex(e => e.TableId);

        builder.HasOne<PokerTable>()
            .WithMany()
            .HasForeignKey(e => e.TableId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<TablePlayer>()
            .WithMany()
            .HasForeignKey(e => e.TablePlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        // No cascade from the counterparty: one player's row disappearing must not
        // silently delete the other side's money.
        builder.HasOne<TablePlayer>()
            .WithMany()
            .HasForeignKey(e => e.CounterpartyPlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Chips)
            .WithOne()
            .HasForeignKey(c => c.LedgerEntryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LedgerEntryChipConfiguration : IEntityTypeConfiguration<LedgerEntryChip>
{
    public void Configure(EntityTypeBuilder<LedgerEntryChip> builder)
    {
        builder.HasKey(c => c.Id);

        builder.HasIndex(c => new { c.LedgerEntryId, c.ChipDenominationId }).IsUnique();

        // Restrict again: a denomination that chips were issued from cannot be
        // deleted out from under the record of issuing them.
        builder.HasOne<ChipDenomination>()
            .WithMany()
            .HasForeignKey(c => c.ChipDenominationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
