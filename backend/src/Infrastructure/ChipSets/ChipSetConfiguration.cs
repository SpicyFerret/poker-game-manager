using Domain.Championships;
using Domain.ChipSets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.ChipSets;

internal sealed class ChipSetConfiguration : IEntityTypeConfiguration<ChipSet>
{
    public void Configure(EntityTypeBuilder<ChipSet> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(80).IsRequired();

        builder.HasOne<Championship>()
            .WithMany()
            .HasForeignKey(s => s.ChampionshipId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Denominations)
            .WithOne()
            .HasForeignKey(d => d.ChipSetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ChipDenominationConfiguration : IEntityTypeConfiguration<ChipDenomination>
{
    public void Configure(EntityTypeBuilder<ChipDenomination> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Colour).HasMaxLength(30);

        // Face value is how a player identifies the chip in their hand ("give me
        // the 25s"), so two chips in one case can't share it.
        builder.HasIndex(d => new { d.ChipSetId, d.FaceValue }).IsUnique();
    }
}
