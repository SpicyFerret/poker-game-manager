using Application.Abstractions.Messaging;
using Application.Championships.Reorder;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Championships;

internal sealed class Reorder : IEndpoint
{
    public sealed record ReorderRequest(IReadOnlyList<Guid> ChampionshipIds);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("championships/order", async (
            ReorderRequest request,
            ICommandHandler<ReorderChampionshipsCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new ReorderChampionshipsCommand(request.ChampionshipIds),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Championships)
        .RequireAuthorization();
    }
}
