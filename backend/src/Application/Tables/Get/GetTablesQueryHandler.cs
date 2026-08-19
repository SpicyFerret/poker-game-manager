using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tables.Get;

internal sealed class GetTablesQueryHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : IQueryHandler<GetTablesQuery, IReadOnlyList<TableSummaryResponse>>
{
    public async Task<Result<IReadOnlyList<TableSummaryResponse>>> Handle(
        GetTablesQuery query,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            query.ChampionshipId,
            ChampionshipRole.Player,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<TableSummaryResponse>>(caller.Error);
        }

        List<TableSummaryResponse> tables = await context.Tables
            .Where(t => t.ChampionshipId == query.ChampionshipId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new TableSummaryResponse
            {
                Id = t.Id,
                Name = t.Name,
                Status = t.Status,
                BuyIn = t.BuyIn,
                PlayerCount = context.TablePlayers.Count(p => p.TableId == t.Id),
                CreatedAtUtc = t.CreatedAtUtc,
                StartedAtUtc = t.StartedAtUtc
            })
            .ToListAsync(cancellationToken);

        return tables;
    }
}
