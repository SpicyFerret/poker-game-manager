using Application.Abstractions.Messaging;
using Application.Championships.GetMine;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Championships;

internal sealed class GetMine : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("championships", async (
            IQueryHandler<GetMyChampionshipsQuery, IReadOnlyList<ChampionshipSummaryResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<ChampionshipSummaryResponse>> result =
                await handler.Handle(new GetMyChampionshipsQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Championships)
        .RequireAuthorization();
    }
}
