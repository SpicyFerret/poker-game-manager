using Domain.Championships;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Championships;

internal sealed class ChampionshipConfiguration : IEntityTypeConfiguration<Championship>
{
    public void Configure(EntityTypeBuilder<Championship> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(80).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(500);

        // Real money: two decimal places, and never floating point.
        builder.Property(c => c.DefaultBuyIn).HasPrecision(18, 2);
        builder.Property(c => c.DefaultRebuy).HasPrecision(18, 2);

        // A chip unit can be worth a fraction of a cent when a big stack is played
        // for small money, so this one needs more places than a currency amount.
        builder.Property(c => c.MoneyPerUnit).HasPrecision(18, 6);

        // Postgres integer[] — the points table is read and written whole, never
        // queried by position, so a child table would buy nothing.
        builder.Property(c => c.PointsByPosition).HasColumnType("integer[]");

        builder.HasOne<User>().WithMany().HasForeignKey(c => c.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
