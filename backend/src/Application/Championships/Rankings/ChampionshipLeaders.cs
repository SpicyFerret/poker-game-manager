using Application.Abstractions.Data;
using Domain.Tables;
using Microsoft.EntityFrameworkCore;

namespace Application.Championships.Rankings;

/// <summary>Who is top of the points ranking, and on how many.</summary>
public sealed record ChampionshipLeader(Guid ChampionshipId, string DisplayName, int Points);

/// <summary>
/// The leader shown on a championship card.
///
/// By points rather than by balance: points are the standing the group agreed
/// on, and "who is winning the championship" is the question the card answers.
/// Balance is a click away in the ranking itself.
/// </summary>
internal static class ChampionshipLeaders
{
    /// <summary>
    /// Leaders for several championships in one query. The list screen shows one
    /// card per championship, and a query each would be a query per card.
    /// </summary>
    public static async Task<Dictionary<Guid, ChampionshipLeader>> ForAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> championshipIds,
        CancellationToken cancellationToken)
    {
        if (championshipIds.Count == 0)
        {
            return [];
        }

        var totals = await context.TableResults
            .Join(
                context.Tables.Where(table =>
                    championshipIds.Contains(table.ChampionshipId) &&
                    (table.Status == TableStatus.Settled || table.Status == TableStatus.Closed)),
                result => result.TableId,
                table => table.Id,
                (result, table) => new { table.ChampionshipId, result })
            .Join(
                context.TablePlayers,
                row => row.result.TablePlayerId,
                player => player.Id,
                (row, player) => new
                {
                    row.ChampionshipId,
                    player.UserId,
                    player.User.DisplayName,
                    row.result.Points,
                    row.result.Balance
                })
            .GroupBy(row => new { row.ChampionshipId, row.UserId, row.DisplayName })
            .Select(group => new
            {
                group.Key.ChampionshipId,
                group.Key.DisplayName,
                Points = group.Sum(row => row.Points),
                Balance = group.Sum(row => row.Balance)
            })
            .ToListAsync(cancellationToken);

        // Same tie-break as the full ranking, so the card and the list it links
        // to can never name a different leader.
        return totals
            .GroupBy(row => row.ChampionshipId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(row => row.Points)
                    .ThenByDescending(row => row.Balance)
                    .ThenBy(row => row.DisplayName, StringComparer.Ordinal)
                    .Select(row => new ChampionshipLeader(group.Key, row.DisplayName, row.Points))
                    .First());
    }
}
