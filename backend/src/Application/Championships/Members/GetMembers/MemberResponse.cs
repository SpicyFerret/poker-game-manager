using Domain.Championships;

namespace Application.Championships.Members.GetMembers;

public sealed record MemberResponse
{
    public Guid UserId { get; init; }

    public string DisplayName { get; init; }

    public ChampionshipRole Role { get; init; }

    public DateTime JoinedAtUtc { get; init; }

    /// <summary>
    /// Whether this member has somewhere to be paid. The value itself is not
    /// exposed here — the member list is visible to every player, and a payment
    /// handle only belongs in the settlement of a table they actually played.
    /// </summary>
    public bool HasPaymentHandle { get; init; }
}
