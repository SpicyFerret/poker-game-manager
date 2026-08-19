using Application.Abstractions.Messaging;
using Application.Championships.TransferOwnership;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Championships;

internal sealed class TransferOwnership : IEndpoint
{
    public sealed record Request(Guid NewOwnerId);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("championships/{championshipId:guid}/transfer-ownership", async (
            Guid championshipId,
            Request request,
            ICommandHandler<TransferOwnershipCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new TransferOwnershipCommand(championshipId, request.NewOwnerId),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Championships)
        .RequireAuthorization();
    }
}
