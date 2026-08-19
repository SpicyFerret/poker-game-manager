using Application.Abstractions.Messaging;
using Application.Tables;
using Application.Tables.BuyChips;
using Application.Tables.Create;
using Application.Tables.Get;
using Application.Tables.IssueStack;
using Application.Tables.Join;
using Application.Tables.Start;
using Domain.Tables;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Championships;

internal sealed class Tables : IEndpoint
{
    public sealed record CreateRequest(
        string Name,
        Guid ChipSetId,
        decimal? BuyIn,
        decimal? Rebuy,
        JoinPolicy JoinPolicy,
        bool AllowLateEntry,
        int SmallChipReserve);

    public sealed record JoinRequest(string? Code);

    public sealed record IssueStackRequest(Guid TablePlayerId, bool IsRebuy);

    public sealed record BuyChipsRequest(Guid BuyerPlayerId, Guid SellerPlayerId, decimal Amount);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app
            .MapGroup("championships/{championshipId:guid}/tables")
            .WithTags(Tags.Tables)
            .RequireAuthorization();

        group.MapGet("", async (
            Guid championshipId,
            IQueryHandler<GetTablesQuery, IReadOnlyList<TableSummaryResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<TableSummaryResponse>> result =
                await handler.Handle(new GetTablesQuery(championshipId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        });

        group.MapGet("{tableId:guid}", async (
            Guid championshipId,
            Guid tableId,
            IQueryHandler<GetTableQuery, TableDetailResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<TableDetailResponse> result =
                await handler.Handle(new GetTableQuery(championshipId, tableId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        });

        group.MapPost("", async (
            Guid championshipId,
            CreateRequest request,
            ICommandHandler<CreateTableCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateTableCommand(
                championshipId,
                request.Name,
                request.ChipSetId,
                request.BuyIn,
                request.Rebuy,
                request.JoinPolicy,
                request.AllowLateEntry,
                request.SmallChipReserve);

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        });

        group.MapPost("{tableId:guid}/join", async (
            Guid championshipId,
            Guid tableId,
            JoinRequest request,
            ICommandHandler<JoinTableCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new JoinTableCommand(championshipId, tableId, request.Code),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        });

        group.MapPost("{tableId:guid}/start", async (
            Guid championshipId,
            Guid tableId,
            ICommandHandler<StartTableCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new StartTableCommand(championshipId, tableId),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        });

        group.MapPost("{tableId:guid}/stacks", async (
            Guid championshipId,
            Guid tableId,
            IssueStackRequest request,
            ICommandHandler<IssueStackCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new IssueStackCommand(championshipId, tableId, request.TablePlayerId, request.IsRebuy),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        });

        group.MapPost("{tableId:guid}/chip-trades", async (
            Guid championshipId,
            Guid tableId,
            BuyChipsRequest request,
            ICommandHandler<BuyChipsFromPlayerCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new BuyChipsFromPlayerCommand(
                championshipId,
                tableId,
                request.BuyerPlayerId,
                request.SellerPlayerId,
                request.Amount);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        });
    }
}
