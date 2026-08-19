using Domain.Tables;

namespace Application.UnitTests.Tables;

public sealed class TableClockTests
{
    private static readonly DateTime Start = new(2026, 8, 19, 20, 0, 0, DateTimeKind.Utc);

    private static TableClock Clock(
        DateTime? pausedAt = null,
        int accumulatedPause = 0) => new()
        {
            Id = Guid.NewGuid(),
            TableId = Guid.NewGuid(),
            CurrentLevel = 1,
            LevelStartedAtUtc = Start,
            PausedAtUtc = pausedAt,
            AccumulatedPauseSeconds = accumulatedPause
        };

    [Fact]
    public void Should_CountForwardWhileRunning()
    {
        Clock().ElapsedSeconds(Start.AddMinutes(7)).ShouldBe(420);
    }

    [Fact]
    public void Should_StandStillWhilePaused()
    {
        // Paused at 5 minutes. However long the wall clock runs on, the level has
        // still only had 5 minutes played.
        TableClock clock = Clock(pausedAt: Start.AddMinutes(5));

        clock.ElapsedSeconds(Start.AddMinutes(5)).ShouldBe(300);
        clock.ElapsedSeconds(Start.AddMinutes(30)).ShouldBe(300);
        clock.IsPaused.ShouldBeTrue();
    }

    [Fact]
    public void Should_DiscountTimeAlreadySpentPaused()
    {
        // Ten minutes of wall clock, three of which were a break: seven played.
        TableClock clock = Clock(accumulatedPause: 180);

        clock.ElapsedSeconds(Start.AddMinutes(10)).ShouldBe(420);
        clock.IsPaused.ShouldBeFalse();
    }

    [Fact]
    public void Should_NeverGoNegative()
    {
        // Clock skew between the server and a phone must not produce a negative
        // elapsed time that the UI would then render as a huge countdown.
        Clock().ElapsedSeconds(Start.AddSeconds(-30)).ShouldBe(0);
    }

    [Fact]
    public void Should_NotGoNegative_WhenPauseExceedsWallClock()
    {
        TableClock clock = Clock(accumulatedPause: 600);

        clock.ElapsedSeconds(Start.AddMinutes(1)).ShouldBe(0);
    }

    [Fact]
    public void Should_ReadTheSameForEveryDeviceAtTheSameInstant()
    {
        // The reason this stores timestamps rather than a counting-down number:
        // two phones polling at different moments still agree about the level.
        TableClock clock = Clock();

        DateTime instant = Start.AddMinutes(12).AddSeconds(34);

        clock.ElapsedSeconds(instant).ShouldBe(clock.ElapsedSeconds(instant));
        clock.ElapsedSeconds(instant).ShouldBe(754);
    }
}
