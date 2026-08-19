using Application.Abstractions.Messaging;
using Application.Championships.UpdateSettings;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Championships;

internal sealed class UpdateSettings : IEndpoint
{
    public sealed record Request(
        string Name,
        string? Description,
        decimal DefaultBuyIn,
        decimal DefaultRebuy,
        bool EnforceDefaults,
        decimal MoneyPerUnit,
        IReadOnlyList<int> PointsByPosition);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("championships/{championshipId:guid}", async (
            Guid championshipId,
            Request request,
            ICommandHandler<UpdateChampionshipSettingsCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateChampionshipSettingsCommand(
                championshipId,
                request.Name,
                request.Description,
                request.DefaultBuyIn,
                request.DefaultRebuy,
                request.EnforceDefaults,
                request.MoneyPerUnit,
                request.PointsByPosition);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Championships)
        .RequireAuthorization();
    }
}
