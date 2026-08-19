using Application.Abstractions.Messaging;
using Domain.Tables;

namespace Application.Tables.Create;

public sealed record CreateTableCommand(
    Guid ChampionshipId,
    string Name,
    Guid ChipSetId,
    decimal? BuyIn,
    decimal? Rebuy,
    JoinPolicy JoinPolicy,
    bool AllowLateEntry,
    int SmallChipReserve)
    : ICommand<Guid>;
