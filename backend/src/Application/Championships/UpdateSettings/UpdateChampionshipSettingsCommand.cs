using Application.Abstractions.Messaging;

namespace Application.Championships.UpdateSettings;

public sealed record UpdateChampionshipSettingsCommand(
    Guid ChampionshipId,
    string Name,
    string? Description,
    decimal DefaultBuyIn,
    decimal DefaultRebuy,
    bool EnforceDefaults,
    decimal MoneyPerUnit,
    IReadOnlyList<int> PointsByPosition)
    : ICommand;
