using Domain.Users;

namespace Domain.Championships;

public sealed class ChampionshipMember
{
    public Guid Id { get; set; }
    public Guid ChampionshipId { get; set; }
    public Guid UserId { get; set; }
    public ChampionshipRole Role { get; set; }
    public DateTime JoinedAtUtc { get; set; }

    public User User { get; set; }
}
