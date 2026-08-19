using Application.Abstractions.Messaging;

namespace Application.Championships.Create;

public sealed record CreateChampionshipCommand(
    string Name,
    string? Description,
    decimal DefaultBuyIn,
    decimal DefaultRebuy,
    bool EnforceDefaults,
    decimal MoneyPerUnit,
    IReadOnlyList<int>? PointsByPosition)
    : ICommand<Guid>;
