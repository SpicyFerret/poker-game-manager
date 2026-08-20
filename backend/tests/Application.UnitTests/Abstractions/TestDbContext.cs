using Application.Abstractions.Data;
using Domain.Championships;
using Domain.ChipSets;
using Domain.Tables;
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


    public DbSet<PokerTable> Tables { get; set; }

    public DbSet<TablePlayer> TablePlayers { get; set; }

    public DbSet<LedgerEntry> LedgerEntries { get; set; }

    public DbSet<LedgerEntryChip> LedgerEntryChips { get; set; }

    public DbSet<FinalCount> FinalCounts { get; set; }

    public DbSet<Settlement> Settlements { get; set; }

    public DbSet<SettlementTransfer> SettlementTransfers { get; set; }

    public DbSet<TableResult> TableResults { get; set; }

    public DbSet<BlindLevel> BlindLevels { get; set; }

    public DbSet<TableClock> TableClocks { get; set; }
}
