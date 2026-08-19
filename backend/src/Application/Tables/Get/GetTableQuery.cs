using Application.Abstractions.Messaging;

namespace Application.Tables.Get;

public sealed record GetTableQuery(Guid ChampionshipId, Guid TableId) : IQuery<TableDetailResponse>;

public sealed record GetTablesQuery(Guid ChampionshipId) : IQuery<IReadOnlyList<TableSummaryResponse>>;
