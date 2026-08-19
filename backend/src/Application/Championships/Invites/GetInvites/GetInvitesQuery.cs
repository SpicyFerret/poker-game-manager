using Application.Abstractions.Messaging;

namespace Application.Championships.Invites.GetInvites;

public sealed record GetInvitesQuery(Guid ChampionshipId) : IQuery<IReadOnlyList<InviteResponse>>;
