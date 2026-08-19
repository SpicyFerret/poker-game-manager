using Application.Abstractions.Messaging;
using Domain.Championships;

namespace Application.Championships.Invites.Create;

public sealed record CreateInviteCommand(
    Guid ChampionshipId,
    ChampionshipRole Role,
    DateTime? ExpiresAtUtc,
    int? MaxUses)
    : ICommand<InviteResponse>;
