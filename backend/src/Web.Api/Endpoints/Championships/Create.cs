using Application.Abstractions.Messaging;
using Application.Championships.Create;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Championships;

internal sealed class Create : IEndpoint
{
    public sealed record Request(
        string Name,
        string? Description,
        decimal DefaultBuyIn,
        decimal DefaultRebuy,
        bool EnforceDefaults,
        decimal MoneyPerUnit,
        IReadOnlyList<int>? PointsByPosition);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("championships", async (
            Request request,
            ICommandHandler<CreateChampionshipCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateChampionshipCommand(
                request.Name,
                request.Description,
                request.DefaultBuyIn,
                request.DefaultRebuy,
                request.EnforceDefaults,
                request.MoneyPerUnit,
                request.PointsByPosition);

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Championships)
        .RequireAuthorization();
    }
}
