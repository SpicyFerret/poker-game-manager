using Application.Abstractions.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Web.Api.Realtime;

internal sealed class ChampionshipActivityNotifier(IHubContext<ChampionshipActivityHub> hub)
    : IChampionshipActivityNotifier
{
    public Task NotifyAsync(Guid championshipId, CancellationToken cancellationToken) =>
        hub.Clients
            .Group($"championship-{championshipId}")
            .SendAsync("changed", championshipId, cancellationToken);
}
