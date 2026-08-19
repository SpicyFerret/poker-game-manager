using Application.Abstractions.Messaging;
using Application.Seasons;
using Application.Seasons.Create;
using Application.Seasons.Get;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Championships;

internal sealed class Seasons : IEndpoint
{
    public sealed record CreateRequest(string Name, DateOnly StartsOn, DateOnly? EndsOn);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app
            .MapGroup("championships/{championshipId:guid}/seasons")
            .WithTags(Tags.Seasons)
            .RequireAuthorization();

        group.MapGet("", async (
            Guid championshipId,
            IQueryHandler<GetSeasonsQuery, IReadOnlyList<SeasonResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<SeasonResponse>> result =
                await handler.Handle(new GetSeasonsQuery(championshipId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        });

        group.MapPost("", async (
            Guid championshipId,
            CreateRequest request,
            ICommandHandler<CreateSeasonCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(
                new CreateSeasonCommand(championshipId, request.Name, request.StartsOn, request.EndsOn),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        });
    }
}
