using Application.Abstractions.Messaging;
using Application.Tables;
using Application.Tables.Blinds;
using Application.Tables.BuyChips;
using Application.Tables.Counting;
using Application.Tables.Create;
using Application.Tables.Delete;
using Application.Tables.Get;
using Application.Tables.IssueStack;
using Application.Tables.Join;
using Application.Tables.Preview;
using Application.Tables.Settle;
using Application.Tables.Start;
using Domain.Tables;
using Microsoft.AspNetCore.Mvc;
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

    public sealed record ReportCountRequest(Guid TablePlayerId, IReadOnlyList<ChipCountEntry> Counts);

    public sealed record SetBlindLevelsRequest(IReadOnlyList<BlindLevelInput> Levels);

    public sealed record ClockRequest(ClockAction Action);

    public sealed record DeleteTableRequest(string ConfirmName);

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

        group.MapPost("{tableId:guid}/counting", async (
            Guid championshipId,
            Guid tableId,
            ICommandHandler<StartCountingCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new StartCountingCommand(championshipId, tableId),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        });

        group.MapPost("{tableId:guid}/counts", async (
            Guid championshipId,
            Guid tableId,
            ReportCountRequest request,
            ICommandHandler<ReportCountCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new ReportCountCommand(championshipId, tableId, request.TablePlayerId, request.Counts),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        });

        group.MapGet("{tableId:guid}/reconciliation", async (
            Guid championshipId,
            Guid tableId,
            IQueryHandler<GetReconciliationQuery, ReconciliationResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<ReconciliationResponse> result = await handler.Handle(
                new GetReconciliationQuery(championshipId, tableId),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        });

        group.MapPost("{tableId:guid}/settlement", async (
            Guid championshipId,
            Guid tableId,
            ICommandHandler<SettleTableCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new SettleTableCommand(championshipId, tableId),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        });

        group.MapGet("{tableId:guid}/settlement", async (
            Guid championshipId,
            Guid tableId,
            IQueryHandler<GetSettlementQuery, SettlementResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<SettlementResponse> result = await handler.Handle(
                new GetSettlementQuery(championshipId, tableId),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        });

        group.MapGet("{tableId:guid}/blinds", async (
            Guid championshipId,
            Guid tableId,
            IQueryHandler<GetBlindsQuery, BlindsResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<BlindsResponse> result = await handler.Handle(
                new GetBlindsQuery(championshipId, tableId),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        });

        group.MapPut("{tableId:guid}/blinds", async (
            Guid championshipId,
            Guid tableId,
            SetBlindLevelsRequest request,
            ICommandHandler<SetBlindLevelsCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new SetBlindLevelsCommand(championshipId, tableId, request.Levels),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        });

        group.MapPost("{tableId:guid}/clock", async (
            Guid championshipId,
            Guid tableId,
            ClockRequest request,
            ICommandHandler<ControlClockCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new ControlClockCommand(championshipId, tableId, request.Action),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        });

        group.MapGet("{tableId:guid}/stack-preview", async (
            Guid championshipId,
            Guid tableId,
            bool isRebuy,
            IQueryHandler<GetStackPreviewQuery, StackPreviewResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<StackPreviewResponse> result = await handler.Handle(
                new GetStackPreviewQuery(championshipId, tableId, isRebuy),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        });

        // The name goes in the body rather than the query string: it is a
        // confirmation, and confirmations do not belong in a URL that gets logged.
        // [FromBody] is required because minimal APIs never infer a body for
        // DELETE. Without it the endpoint fails while routing is being built,
        // which takes down every other route in the app, not just this one.
        group.MapDelete("{tableId:guid}", async (
            Guid championshipId,
            Guid tableId,
            [FromBody] DeleteTableRequest request,
            ICommandHandler<DeleteTableCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new DeleteTableCommand(championshipId, tableId, request.ConfirmName),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        });
    }
}
