using Application.Abstractions.Messaging;
using Application.Championships.Members.Add;
using Application.Championships.Members.ChangeRole;
using Application.Championships.Members.GetMembers;
using Application.Championships.Members.Remove;
using Domain.Championships;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Championships;

/// <summary>
/// The four member operations share one route prefix and one mental model, so
/// they are kept together rather than split across four near-identical files.
/// </summary>
internal sealed class Members : IEndpoint
{
    public sealed record AddRequest(string Email, ChampionshipRole Role);

    public sealed record ChangeRoleRequest(ChampionshipRole Role);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app
            .MapGroup("championships/{championshipId:guid}/members")
            .WithTags(Tags.Members)
            .RequireAuthorization();

        group.MapGet("", async (
            Guid championshipId,
            IQueryHandler<GetMembersQuery, IReadOnlyList<MemberResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<MemberResponse>> result =
                await handler.Handle(new GetMembersQuery(championshipId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        });

        group.MapPost("", async (
            Guid championshipId,
            AddRequest request,
            ICommandHandler<AddMemberCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new AddMemberCommand(championshipId, request.Email, request.Role),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        });

        group.MapPut("{userId:guid}/role", async (
            Guid championshipId,
            Guid userId,
            ChangeRoleRequest request,
            ICommandHandler<ChangeMemberRoleCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new ChangeMemberRoleCommand(championshipId, userId, request.Role),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        });

        group.MapDelete("{userId:guid}", async (
            Guid championshipId,
            Guid userId,
            ICommandHandler<RemoveMemberCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new RemoveMemberCommand(championshipId, userId),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        });
    }
}
