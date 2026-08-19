using Application.Abstractions.Messaging;

namespace Application.Championships.Members.GetMembers;

public sealed record GetMembersQuery(Guid ChampionshipId) : IQuery<IReadOnlyList<MemberResponse>>;
