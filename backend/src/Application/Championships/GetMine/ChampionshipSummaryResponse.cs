using Domain.Championships;

namespace Application.Championships.GetMine;

public sealed record ChampionshipSummaryResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>
    /// The caller's own role. The list screen uses it to decide which actions to
    /// offer without a follow-up request per championship.
    /// </summary>
    public ChampionshipRole Role { get; init; }

    public int MemberCount { get; init; }
}
