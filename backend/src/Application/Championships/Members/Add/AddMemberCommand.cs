using Application.Abstractions.Messaging;
using Domain.Championships;

namespace Application.Championships.Members.Add;

/// <summary>
/// Adds someone who already has an account, by their exact email.
///
/// Exact match rather than a search endpoint on purpose: a search over users
/// would be a directory anyone with an account could enumerate. Adding by an
/// address you already know gives up nothing — you either know it or you don't.
/// </summary>
public sealed record AddMemberCommand(Guid ChampionshipId, string Email, ChampionshipRole Role)
    : ICommand;
