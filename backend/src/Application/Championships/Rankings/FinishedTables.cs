using Application.Abstractions.Data;
using Domain.Tables;

namespace Application.Championships.Rankings;

/// <summary>
/// What every ranking, statement and statistic in this folder counts.
///
/// Shared deliberately: four queries that each decided for themselves which
/// tables were finished would eventually disagree, and a ranking that disagrees
/// with the statement behind it is worse than no ranking.
/// </summary>
internal static class FinishedTables
{
    /// <summary>
    /// A night is finished once it has been settled. `Closed` is included because
    /// it is what `Settled` becomes, and results are written before either.
    /// </summary>
    public static IQueryable<PokerTable> For(IApplicationDbContext context, Guid championshipId) =>
        context.Tables.Where(table =>
            table.ChampionshipId == championshipId &&
            (table.Status == TableStatus.Settled || table.Status == TableStatus.Closed));
}
