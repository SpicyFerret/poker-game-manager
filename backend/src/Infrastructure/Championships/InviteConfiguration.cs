using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Championships;

internal sealed class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Code).HasMaxLength(InviteCode.Length).IsRequired();

        // Unique across every championship: a person redeeming a code types only
        // the code, so it has to identify the championship on its own.
        builder.HasIndex(i => i.Code).IsUnique();

        builder.Property(i => i.Role).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Championship>()
            .WithMany()
            .HasForeignKey(i => i.ChampionshipId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
