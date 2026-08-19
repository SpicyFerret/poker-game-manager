using Application.Abstractions.Messaging;
using Application.Championships.Invites;
using Application.Championships.Invites.Create;
using Application.Championships.Invites.GetInvites;
using Application.Championships.Invites.Revoke;
using Application.Championships.Join;
using Domain.Championships;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Championships;

internal sealed class Invites : IEndpoint
{
    public sealed record CreateRequest(
        ChampionshipRole Role,
        DateTime? ExpiresAtUtc,
        int? MaxUses);

    public sealed record JoinRequest(string Code);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app
            .MapGroup("championships/{championshipId:guid}/invites")
            .WithTags(Tags.Invites)
            .RequireAuthorization();

        group.MapPost("", async (
            Guid championshipId,
            CreateRequest request,
            ICommandHandler<CreateInviteCommand, InviteResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<InviteResponse> result = await handler.Handle(
                new CreateInviteCommand(championshipId, request.Role, request.ExpiresAtUtc, request.MaxUses),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        });

        group.MapGet("", async (
            Guid championshipId,
            IQueryHandler<GetInvitesQuery, IReadOnlyList<InviteResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<InviteResponse>> result =
                await handler.Handle(new GetInvitesQuery(championshipId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        });

        group.MapDelete("{inviteId:guid}", async (
            Guid championshipId,
            Guid inviteId,
            ICommandHandler<RevokeInviteCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new RevokeInviteCommand(championshipId, inviteId),
                cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        });

        // Sits outside the group: redeeming a code is how you get into a
        // championship, so it cannot be nested under one you're not in yet.
        app.MapPost("championships/join", async (
            JoinRequest request,
            ICommandHandler<JoinByCodeCommand, JoinByCodeResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<JoinByCodeResponse> result =
                await handler.Handle(new JoinByCodeCommand(request.Code), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Invites)
        .RequireAuthorization();
    }
}
