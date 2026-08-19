using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Championships;

internal sealed class ChampionshipMemberConfiguration : IEntityTypeConfiguration<ChampionshipMember>
{
    public void Configure(EntityTypeBuilder<ChampionshipMember> builder)
    {
        builder.HasKey(m => m.Id);

        // One membership per person per championship. This is the index every
        // authorization check hits, and the guarantee that a role is unambiguous.
        builder.HasIndex(m => new { m.ChampionshipId, m.UserId }).IsUnique();

        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Championship>()
            .WithMany()
            .HasForeignKey(m => m.ChampionshipId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
