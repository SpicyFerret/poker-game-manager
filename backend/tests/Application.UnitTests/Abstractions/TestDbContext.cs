using Application.Abstractions.Data;
using Domain.Championships;
using Domain.ChipSets;
using Domain.Seasons;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Abstractions;

/// <summary>
/// A lightweight in-memory <see cref="DbContext"/> that implements <see cref="IApplicationDbContext"/>
/// so Application handlers can be unit tested without referencing the Infrastructure layer.
/// </summary>
public sealed class TestDbContext(DbContextOptions<TestDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users { get; set; }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<Championship> Championships { get; set; }

    public DbSet<ChampionshipMember> ChampionshipMembers { get; set; }

    public DbSet<Invite> Invites { get; set; }

    public DbSet<ChipSet> ChipSets { get; set; }

    public DbSet<ChipDenomination> ChipDenominations { get; set; }

    public DbSet<Season> Seasons { get; set; }
}
