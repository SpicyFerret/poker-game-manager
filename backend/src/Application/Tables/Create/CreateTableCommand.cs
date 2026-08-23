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
    LateEntryPolicy LateEntry,
    int SmallChipReserve)
    : ICommand<Guid>;
