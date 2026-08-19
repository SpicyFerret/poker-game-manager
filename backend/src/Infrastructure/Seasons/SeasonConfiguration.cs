using Domain.Championships;
using Domain.Seasons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Seasons;

internal sealed class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(60).IsRequired();

        builder.HasIndex(s => new { s.ChampionshipId, s.StartsOn });

        builder.HasOne<Championship>()
            .WithMany()
            .HasForeignKey(s => s.ChampionshipId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
