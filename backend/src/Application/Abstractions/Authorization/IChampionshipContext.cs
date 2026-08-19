using Domain.Championships;
using SharedKernel;

namespace Application.Abstractions.Authorization;

/// <summary>
/// Resolves what the caller is allowed to do inside one championship. Roles here
/// are per championship — the same person is Owner in one and Player in another —
/// which is why the template's global permission model cannot express them.
///
/// Deliberately not cached. It is one indexed lookup on (championship_id,
/// user_id), and caching it would buy a saved query at the price of a demoted
/// admin keeping their powers until the entry expires. Authorization is the last
/// place worth trading correctness for speed.
/// </summary>
public interface IChampionshipContext
{
    /// <summary>
    /// The caller's role in the championship, or null if they are not a member —
    /// which is also the answer when the championship does not exist, so callers
    /// cannot use this to probe for ids.
    /// </summary>
    Task<ChampionshipRole?> GetRoleAsync(Guid championshipId, CancellationToken cancellationToken);

    /// <summary>
    /// The caller's role, provided it is at least <paramref name="minimum"/>.
    /// </summary>
    Task<Result<ChampionshipRole>> RequireRoleAsync(
        Guid championshipId,
        ChampionshipRole minimum,
        CancellationToken cancellationToken);
}
