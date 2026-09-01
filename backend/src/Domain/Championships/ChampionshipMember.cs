using Domain.Users;

namespace Domain.Championships;

public sealed class ChampionshipMember
{
    public Guid Id { get; set; }
    public Guid ChampionshipId { get; set; }
    public Guid UserId { get; set; }
    public ChampionshipRole Role { get; set; }
    public DateTime JoinedAtUtc { get; set; }

    /// <summary>
    /// Where this championship sits in the caller's own list, low to high. It is
    /// the user's personal arrangement, not a property of the championship
    /// itself — the same championship can sit anywhere in a different member's
    /// list.
    /// </summary>
    public int DisplayOrder { get; set; }

    public User User { get; set; }
}
