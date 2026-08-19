using Application.Abstractions.Messaging;
using Domain.Championships;

namespace Application.Championships.Members.ChangeRole;

public sealed record ChangeMemberRoleCommand(
    Guid ChampionshipId,
    Guid UserId,
    ChampionshipRole Role)
    : ICommand;
