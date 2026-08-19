using Application.Abstractions.Messaging;
using Application.Championships.GetById;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Championships;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("championships/{championshipId:guid}", async (
            Guid championshipId,
            IQueryHandler<GetChampionshipByIdQuery, ChampionshipResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<ChampionshipResponse> result =
                await handler.Handle(new GetChampionshipByIdQuery(championshipId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Championships)
        .RequireAuthorization();
    }
}
