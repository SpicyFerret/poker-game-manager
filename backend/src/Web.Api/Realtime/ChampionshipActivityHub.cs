using Application.Abstractions.Authorization;
using Domain.Championships;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Web.Api.Realtime;

/// <summary>
/// One group per championship. Joining it is the only thing a client asks
/// for — the hub never sends anything back except "something changed",
/// leaving every screen to refetch through the same service call it already
/// uses when it loads the first time.
/// </summary>
[Authorize]
public sealed class ChampionshipActivityHub(IChampionshipContext championshipContext) : Hub
{
    private static string GroupName(Guid championshipId) => $"championship-{championshipId}";

    /// <summary>
    /// Refuses silently rather than throwing: a non-member asking to watch a
    /// championship they cannot see should learn nothing from the answer,
    /// same as every other membership check in the app.
    /// </summary>
    public async Task JoinChampionship(Guid championshipId)
    {
        ChampionshipRole? role = await championshipContext.GetRoleAsync(championshipId, Context.ConnectionAborted);

        if (role is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(championshipId), Context.ConnectionAborted);
        }
    }
}
