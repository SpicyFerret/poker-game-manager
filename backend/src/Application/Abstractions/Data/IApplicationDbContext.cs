using Domain.Championships;
using Domain.ChipSets;
using Domain.Seasons;
using Domain.Tables;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Application.Abstractions.Data;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<Championship> Championships { get; }
    DbSet<ChampionshipMember> ChampionshipMembers { get; }
    DbSet<Invite> Invites { get; }

    DbSet<ChipSet> ChipSets { get; }
    DbSet<ChipDenomination> ChipDenominations { get; }

    DbSet<Season> Seasons { get; }

    DbSet<PokerTable> Tables { get; }
    DbSet<TablePlayer> TablePlayers { get; }
    DbSet<LedgerEntry> LedgerEntries { get; }
    DbSet<LedgerEntryChip> LedgerEntryChips { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
