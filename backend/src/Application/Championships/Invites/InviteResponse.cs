using Domain.Championships;

namespace Application.Championships.Invites;

public sealed record InviteResponse
{
    public Guid Id { get; init; }

    public string Code { get; init; }

    public ChampionshipRole Role { get; init; }

    public DateTime? ExpiresAtUtc { get; init; }

    public int? MaxUses { get; init; }

    public int Uses { get; init; }

    public bool IsRevoked { get; init; }
}
