namespace Application.Seasons;

public sealed record SeasonResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    public DateOnly StartsOn { get; init; }

    public DateOnly? EndsOn { get; init; }
}
