using Application.Abstractions.Messaging;
using Application.ChipSets;
using Application.ChipSets.Create;
using Application.ChipSets.Delete;
using Application.ChipSets.Get;
using Application.ChipSets.Update;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Championships;

internal sealed class ChipSets : IEndpoint
{
    public sealed record WriteRequest(string Name, IReadOnlyList<ChipDenominationModel> Denominations);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app
            .MapGroup("championships/{championshipId:guid}/chip-sets")
            .WithTags(Tags.ChipSets)
            .RequireAuthorization();

        group.MapGet("", async (
            Guid championshipId,
            IQueryHandler<GetChipSetsQuery, IReadOnlyList<ChipSetResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<ChipSetResponse>> result =
                await handler.Handle(new GetChipSetsQuery(championshipId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        });

        group.MapPost("", async (
            Guid championshipId,
            WriteRequest request,
            ICommandHandler<CreateChipSetCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(
                new CreateChipSetCommand(championshipId, request.Name, request.Denominations),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        });

        group.MapPut("{chipSetId:guid}", async (
            Guid championshipId,
            Guid chipSetId,
            WriteRequest request,
            ICommandHandler<UpdateChipSetCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new UpdateChipSetCommand(championshipId, chipSetId, request.Name, request.Denominations),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        });

        group.MapDelete("{chipSetId:guid}", async (
            Guid championshipId,
            Guid chipSetId,
            ICommandHandler<DeleteChipSetCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new DeleteChipSetCommand(championshipId, chipSetId),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        });
    }
}
