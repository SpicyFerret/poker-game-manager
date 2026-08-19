namespace Domain.Championships;

public sealed class Invite
{
    public Guid Id { get; set; }
    public Guid ChampionshipId { get; set; }

    /// <summary>
    /// Short enough to read out across a table. See <see cref="InviteCode"/> for
    /// the alphabet and why it excludes some characters.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Role the invitee joins as. Restricted the same way role changes are: an
    /// invite can't hand out a role at or above the issuer's own.
    /// </summary>
    public ChampionshipRole Role { get; set; }

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>
    /// Null means unlimited — the usual case for a code pasted into the group's
    /// chat once and used by whoever shows up.
    /// </summary>
    public int? MaxUses { get; set; }
    public int Uses { get; set; }

    public bool IsRevoked { get; set; }

    public bool IsUsable(DateTime utcNow) =>
        !IsRevoked &&
        (ExpiresAtUtc is null || ExpiresAtUtc > utcNow) &&
        (MaxUses is null || Uses < MaxUses);
}
