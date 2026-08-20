using Application.Abstractions.Messaging;
using Application.Championships.Rankings;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Championships;

/// <summary>
/// Everything read back after the nights are over: the two rankings, the history,
/// one player's own statement, and the championship's numbers. All reads, all
/// open to any member — a ranking nobody may look at is not a ranking.
/// </summary>
internal sealed class Rankings : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app
            .MapGroup("championships/{championshipId:guid}")
            .WithTags(Tags.Rankings)
            .RequireAuthorization();

        group.MapGet("rankings", async (
            Guid championshipId,
            IQueryHandler<GetRankingsQuery, RankingsResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<RankingsResponse> result =
                await handler.Handle(new GetRankingsQuery(championshipId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        });

        group.MapGet("history", async (
            Guid championshipId,
            IQueryHandler<GetHistoryQuery, IReadOnlyList<HistoryRow>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<HistoryRow>> result =
                await handler.Handle(new GetHistoryQuery(championshipId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        });

        group.MapGet("statement", async (
            Guid championshipId,
            IQueryHandler<GetStatementQuery, StatementResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<StatementResponse> result =
                await handler.Handle(new GetStatementQuery(championshipId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        });

        group.MapGet("statistics", async (
            Guid championshipId,
            IQueryHandler<GetStatisticsQuery, StatisticsResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<StatisticsResponse> result =
                await handler.Handle(new GetStatisticsQuery(championshipId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        });
    }
}
