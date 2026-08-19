using Domain.Tables.Services;

namespace Application.UnitTests.Tables;

public sealed class TableResultCalculatorTests
{
    private static readonly DateTime Night = new(2026, 8, 19, 20, 0, 0, DateTimeKind.Utc);

    private static PlayerNight Player(
        Guid id,
        decimal chips,
        decimal paidIn,
        int joinedMinutesIn = 0) => new()
        {
            TablePlayerId = id,
            ChipsValue = chips,
            PaidIn = paidIn,
            JoinedAtUtc = Night.AddMinutes(joinedMinutesIn)
        };

    private static readonly int[] DefaultPoints = [10, 7, 5, 3, 2, 1];

    [Fact]
    public void Should_RankByWhatPeopleWalkedAwayWith_NotByChipCount()
    {
        // The distinction that matters: 'grinder' is holding far more chips, but
        // bought them with three rebuys and finished down. 'sortudo' turned one
        // buy-in into a profit and won the night.
        var grinder = Guid.NewGuid();
        var sortudo = Guid.NewGuid();

        IReadOnlyList<PlayerResult> results = TableResultCalculator.Calculate(
            [
                Player(grinder, chips: 150m, paidIn: 200m),
                Player(sortudo, chips: 80m, paidIn: 50m)
            ],
            DefaultPoints);

        results[0].TablePlayerId.ShouldBe(sortudo);
        results[0].Position.ShouldBe(1);
        results[0].Balance.ShouldBe(30m);

        results[1].TablePlayerId.ShouldBe(grinder);
        results[1].Balance.ShouldBe(-50m);
    }

    [Fact]
    public void Should_AwardThePointsForEachPlacing()
    {
        IReadOnlyList<PlayerResult> results = TableResultCalculator.Calculate(
            [
                Player(Guid.NewGuid(), 100m, 50m),
                Player(Guid.NewGuid(), 70m, 50m),
                Player(Guid.NewGuid(), 30m, 50m)
            ],
            DefaultPoints);

        results.Select(r => r.Points).ShouldBe([10, 7, 5]);
    }

    [Fact]
    public void Should_ScoreNothingPastTheEndOfThePointsTable()
    {
        // The list length is how the owner says how deep scoring goes.
        IReadOnlyList<PlayerResult> results = TableResultCalculator.Calculate(
            [
                Player(Guid.NewGuid(), 100m, 50m),
                Player(Guid.NewGuid(), 60m, 50m),
                Player(Guid.NewGuid(), 10m, 50m)
            ],
            [10, 5]);

        results.Select(r => r.Points).ShouldBe([10, 5, 0]);
    }

    [Fact]
    public void Should_BreakATieByWhoRiskedLess()
    {
        // Same profit, but one got there on a single buy-in. That is the better
        // night, and the tie has to break somewhere other than at random.
        var careful = Guid.NewGuid();
        var reckless = Guid.NewGuid();

        IReadOnlyList<PlayerResult> results = TableResultCalculator.Calculate(
            [
                Player(reckless, chips: 200m, paidIn: 150m),
                Player(careful, chips: 100m, paidIn: 50m)
            ],
            DefaultPoints);

        results[0].TablePlayerId.ShouldBe(careful);
        results[1].TablePlayerId.ShouldBe(reckless);
    }

    [Fact]
    public void Should_BreakARemainingTieByWhoSatDownFirst()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        IReadOnlyList<PlayerResult> results = TableResultCalculator.Calculate(
            [
                Player(second, 100m, 50m, joinedMinutesIn: 30),
                Player(first, 100m, 50m, joinedMinutesIn: 0)
            ],
            DefaultPoints);

        results[0].TablePlayerId.ShouldBe(first);
    }

    [Fact]
    public void Should_ProduceBalancesThatSumToZero()
    {
        // The night's books have to close: every real cent won came from someone.
        IReadOnlyList<PlayerResult> results = TableResultCalculator.Calculate(
            [
                Player(Guid.NewGuid(), 220m, 100m),
                Player(Guid.NewGuid(), 30m, 100m),
                Player(Guid.NewGuid(), 50m, 100m)
            ],
            DefaultPoints);

        results.Sum(r => r.Balance).ShouldBe(0m);
    }

    [Fact]
    public void Should_HandleATableWhereNobodyPlayed()
    {
        TableResultCalculator.Calculate([], DefaultPoints).ShouldBeEmpty();
    }

    [Fact]
    public void ChipsValue_Should_UseTheEffectiveValueNotThePrintedOne()
    {
        // The whole point of the override: a chip printed 5, played as 100.
        var five = Guid.NewGuid();

        decimal value = TableResultCalculator.ChipsValue(
            new Dictionary<Guid, int> { [five] = 10 },
            new Dictionary<Guid, int> { [five] = 100 },
            moneyPerUnit: 0.05m);

        // 10 chips x 100 units x R$0.05 = R$50.
        value.ShouldBe(50m);
    }

    [Fact]
    public void ChipsValue_Should_BeZeroForAnEmptyStack()
    {
        TableResultCalculator
            .ChipsValue(new Dictionary<Guid, int>(), new Dictionary<Guid, int>(), 0.05m)
            .ShouldBe(0m);
    }
}
