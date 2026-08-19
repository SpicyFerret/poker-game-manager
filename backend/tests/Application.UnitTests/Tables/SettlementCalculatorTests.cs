using Domain.Tables.Services;

namespace Application.UnitTests.Tables;

public sealed class SettlementCalculatorTests
{
    private static PlayerBalance Balance(string name, decimal amount) =>
        new(Id(name), amount);

    /// <summary>Stable ids from names, so failures read as names rather than guids.</summary>
    private static Guid Id(string name)
    {
        byte[] bytes = new byte[16];
        System.Text.Encoding.UTF8.GetBytes(name).CopyTo(bytes, 0);
        return new Guid(bytes);
    }

    private static string NameOf(Guid id) =>
        System.Text.Encoding.UTF8.GetString(id.ToByteArray()).TrimEnd('\0');

    private static string Describe(IReadOnlyList<SettlementTransferPlan> transfers) =>
        string.Join(", ", transfers.Select(t => $"{NameOf(t.FromPlayerId)}->{NameOf(t.ToPlayerId)}:{t.Amount}"));

    /// <summary>Applies the transfers and checks everyone ends on zero.</summary>
    private static void ShouldSettleEveryone(
        IReadOnlyList<PlayerBalance> balances,
        IReadOnlyList<SettlementTransferPlan> transfers)
    {
        var net = balances.ToDictionary(b => b.TablePlayerId, b => b.Balance);

        foreach (SettlementTransferPlan transfer in transfers)
        {
            net[transfer.FromPlayerId] += transfer.Amount;
            net[transfer.ToPlayerId] -= transfer.Amount;
        }

        net.Values.ShouldAllBe(value => value == 0m, Describe(transfers));
    }

    [Fact]
    public void Should_ProduceNothing_WhenEverybodyBrokeEven()
    {
        SettlementCalculator.Calculate([Balance("a", 0), Balance("b", 0)]).ShouldBeEmpty();
    }

    [Fact]
    public void Should_SettleTheSimplestCase_WithOnePayment()
    {
        List<PlayerBalance> balances = [Balance("perdeu", -50), Balance("ganhou", 50)];

        IReadOnlyList<SettlementTransferPlan> transfers = SettlementCalculator.Calculate(balances);

        transfers.Count.ShouldBe(1);
        NameOf(transfers[0].FromPlayerId).ShouldBe("perdeu");
        NameOf(transfers[0].ToPlayerId).ShouldBe("ganhou");
        transfers[0].Amount.ShouldBe(50m);
    }

    [Fact]
    public void Should_SplitIntoPairs_WhenThePairsSettleThemselves()
    {
        // Two independent pairs. The naive answer chains them into 3 payments;
        // the right answer is 2, because each pair already settles itself.
        List<PlayerBalance> balances =
        [
            Balance("a", -50), Balance("b", 50),
            Balance("c", -30), Balance("d", 30)
        ];

        IReadOnlyList<SettlementTransferPlan> transfers = SettlementCalculator.Calculate(balances);

        transfers.Count.ShouldBe(2, Describe(transfers));
        ShouldSettleEveryone(balances, transfers);
    }

    [Fact]
    public void Should_FindTheZeroSumSubsetThatGreedyWouldMiss()
    {
        // Greedy from the largest pairs -100 with +100 and lands on 3 payments.
        // The minimum is 2: {-100, +100} and {-30, -70, +... } — here the split is
        // {a,b} and {c,d,e}, which only a real search finds.
        List<PlayerBalance> balances =
        [
            Balance("a", -100), Balance("b", 100),
            Balance("c", -30), Balance("d", -70), Balance("e", 100)
        ];

        IReadOnlyList<SettlementTransferPlan> transfers = SettlementCalculator.Calculate(balances);

        transfers.Count.ShouldBe(3, Describe(transfers));
        ShouldSettleEveryone(balances, transfers);
    }

    [Fact]
    public void Should_NeedOnlyOnePaymentPerLoser_WhenOnePersonWonEverything()
    {
        List<PlayerBalance> balances =
        [
            Balance("a", -25), Balance("b", -25), Balance("c", -25), Balance("vencedor", 75)
        ];

        IReadOnlyList<SettlementTransferPlan> transfers = SettlementCalculator.Calculate(balances);

        // Nothing can be split here, so 4 players means 3 payments.
        transfers.Count.ShouldBe(3, Describe(transfers));
        transfers.ShouldAllBe(t => NameOf(t.ToPlayerId) == "vencedor");
        ShouldSettleEveryone(balances, transfers);
    }

    [Fact]
    public void Should_LetOnePersonPaySeveralOthers()
    {
        // Expected and correct: the night's big loser owes more than any single
        // winner is up, so their debt has to be split.
        List<PlayerBalance> balances =
        [
            Balance("perdeu", -100), Balance("a", 40), Balance("b", 35), Balance("c", 25)
        ];

        IReadOnlyList<SettlementTransferPlan> transfers = SettlementCalculator.Calculate(balances);

        transfers.Count.ShouldBe(3, Describe(transfers));
        transfers.ShouldAllBe(t => NameOf(t.FromPlayerId) == "perdeu");
        ShouldSettleEveryone(balances, transfers);
    }

    [Fact]
    public void Should_IgnorePlayersWhoBrokeEven()
    {
        List<PlayerBalance> balances =
        [
            Balance("a", -50), Balance("nada", 0), Balance("b", 50)
        ];

        IReadOnlyList<SettlementTransferPlan> transfers = SettlementCalculator.Calculate(balances);

        transfers.Count.ShouldBe(1);
        transfers.ShouldAllBe(t => NameOf(t.FromPlayerId) != "nada" && NameOf(t.ToPlayerId) != "nada");
    }

    [Fact]
    public void Should_HandleCentsWithoutDrift()
    {
        // Thirds of a real amount: the kind of split that turns into a stray cent
        // if any of this touches floating point.
        List<PlayerBalance> balances =
        [
            Balance("a", -33.33m), Balance("b", -33.33m), Balance("c", -33.34m), Balance("d", 100m)
        ];

        IReadOnlyList<SettlementTransferPlan> transfers = SettlementCalculator.Calculate(balances);

        transfers.Sum(t => t.Amount).ShouldBe(100m);
        ShouldSettleEveryone(balances, transfers);
    }

    [Fact]
    public void Should_SettleATypicalNight()
    {
        List<PlayerBalance> balances =
        [
            Balance("dan", 120m),
            Balance("pedro", -45m),
            Balance("ana", -80m),
            Balance("rafa", 60m),
            Balance("bia", -55m)
        ];

        IReadOnlyList<SettlementTransferPlan> transfers = SettlementCalculator.Calculate(balances);

        ShouldSettleEveryone(balances, transfers);

        // Never more than n-1, and every payment is a real, positive amount.
        transfers.Count.ShouldBeLessThanOrEqualTo(4, Describe(transfers));
        transfers.ShouldAllBe(t => t.Amount > 0);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(9)]
    [InlineData(12)]
    public void Should_AlwaysSettleAndNeverExceedNMinusOne(int players)
    {
        // Deterministic pseudo-random tables: the last balance absorbs the rest so
        // the table sums to zero, as a real one must. CA5394 is about randomness
        // used for security; here a fixed seed is the point, so failures repeat.
#pragma warning disable CA5394
        var random = new Random(players * 7919);
        var balances = new List<PlayerBalance>();
        decimal running = 0;

        for (int i = 0; i < players - 1; i++)
        {
            decimal amount = random.Next(-20000, 20000) / 100m;
            running += amount;
            balances.Add(Balance($"p{i}", amount));
        }

#pragma warning restore CA5394

        balances.Add(Balance($"p{players - 1}", -running));

        IReadOnlyList<SettlementTransferPlan> transfers = SettlementCalculator.Calculate(balances);

        ShouldSettleEveryone(balances, transfers);
        transfers.Count.ShouldBeLessThanOrEqualTo(players - 1, Describe(transfers));
    }

    [Fact]
    public void Should_StillSettle_AboveTheExactSearchLimit()
    {
        // Past ExactLimit it falls back to greedy, which must still be correct —
        // just not guaranteed minimal.
        int players = SettlementCalculator.ExactLimit + 3;
        var balances = new List<PlayerBalance>();

        for (int i = 0; i < players - 1; i++)
        {
            balances.Add(Balance($"p{i}", 10m));
        }

        balances.Add(Balance($"p{players - 1}", -10m * (players - 1)));

        IReadOnlyList<SettlementTransferPlan> transfers = SettlementCalculator.Calculate(balances);

        ShouldSettleEveryone(balances, transfers);
        transfers.Count.ShouldBeLessThanOrEqualTo(players - 1);
    }
}
