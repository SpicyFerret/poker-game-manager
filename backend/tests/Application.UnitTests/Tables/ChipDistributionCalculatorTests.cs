using Domain.Tables.Services;

namespace Application.UnitTests.Tables;

public sealed class ChipDistributionCalculatorTests
{
    private static readonly Guid Five = Guid.NewGuid();
    private static readonly Guid TwentyFive = Guid.NewGuid();
    private static readonly Guid Fifty = Guid.NewGuid();
    private static readonly Guid Hundred = Guid.NewGuid();

    /// <summary>A typical case: 5/25/50/100 with plenty of stock.</summary>
    private static List<DenominationStock> Case(
        int fives = 500,
        int twentyFives = 500,
        int fifties = 500,
        int hundreds = 500,
        double[]? profile = null)
    {
        double[] shares = profile ?? [.. ChipDistributionCalculator.DefaultProfile(4)];

        return
        [
            new DenominationStock { DenominationId = Five, EffectiveValue = 5, Available = fives, ProfileShare = shares[0] },
            new DenominationStock { DenominationId = TwentyFive, EffectiveValue = 25, Available = twentyFives, ProfileShare = shares[1] },
            new DenominationStock { DenominationId = Fifty, EffectiveValue = 50, Available = fifties, ProfileShare = shares[2] },
            new DenominationStock { DenominationId = Hundred, EffectiveValue = 100, Available = hundreds, ProfileShare = shares[3] }
        ];
    }

    private static int CountOf(ChipDistribution distribution, Guid denominationId) =>
        distribution.Chips.SingleOrDefault(c => c.DenominationId == denominationId)?.Quantity ?? 0;

    private static long UnitsOf(ChipDistribution distribution, IReadOnlyList<DenominationStock> stock) =>
        distribution.Chips.Sum(c =>
            (long)c.Quantity * stock.Single(d => d.DenominationId == c.DenominationId).EffectiveValue);

    [Fact]
    public void Should_HitTheTargetExactly_WithAmpleStock()
    {
        List<DenominationStock> stock = Case();

        ChipDistribution result = ChipDistributionCalculator.Calculate(1000, stock);

        result.IsComplete.ShouldBeTrue();
        result.AllocatedUnits.ShouldBe(1000);
        UnitsOf(result, stock).ShouldBe(1000);
    }

    [Fact]
    public void Should_ReproduceTheHandDealtExample_WithTheProfileThatDescribesIt()
    {
        // The mix Danilo described for a 1000-unit stack: 10x5, 8x25, 7x50, 4x100.
        // It comes out of a 5/20/35/40 profile, which is what that shape *is*.
        List<DenominationStock> stock = Case(profile: [0.05, 0.20, 0.35, 0.40]);

        ChipDistribution result = ChipDistributionCalculator.Calculate(1000, stock);

        CountOf(result, Five).ShouldBe(10);
        CountOf(result, TwentyFive).ShouldBe(8);
        CountOf(result, Fifty).ShouldBe(7);
        CountOf(result, Hundred).ShouldBe(4);
        result.IsComplete.ShouldBeTrue();
    }

    [Fact]
    public void Should_GiveTheSmallestDenominationTheLeastValue()
    {
        // A stack that is mostly 5s cannot be counted at the table, and a stack of
        // only 100s cannot post a small blind. The ramp is what keeps both true.
        List<DenominationStock> stock = Case();

        ChipDistribution result = ChipDistributionCalculator.Calculate(1000, stock);

        long fivesValue = CountOf(result, Five) * 5L;
        long hundredsValue = CountOf(result, Hundred) * 100L;

        fivesValue.ShouldBeLessThan(hundredsValue);
        CountOf(result, Five).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Should_MoveToBiggerChips_WhenTheSmallOnesAreGone()
    {
        // Late in the night: the 5s are spent. The stack still has to add up.
        List<DenominationStock> stock = Case(fives: 0);

        ChipDistribution result = ChipDistributionCalculator.Calculate(1000, stock);

        CountOf(result, Five).ShouldBe(0);
        result.IsComplete.ShouldBeTrue();
        UnitsOf(result, stock).ShouldBe(1000);
    }

    [Fact]
    public void Should_NeverHandOutMoreThanTheCaseHolds()
    {
        List<DenominationStock> stock = Case(fives: 3, twentyFives: 2, fifties: 1, hundreds: 1);

        ChipDistribution result = ChipDistributionCalculator.Calculate(1000, stock);

        CountOf(result, Five).ShouldBeLessThanOrEqualTo(3);
        CountOf(result, TwentyFive).ShouldBeLessThanOrEqualTo(2);
        CountOf(result, Fifty).ShouldBeLessThanOrEqualTo(1);
        CountOf(result, Hundred).ShouldBeLessThanOrEqualTo(1);
    }

    [Fact]
    public void Should_ReportTheShortfall_RatherThanDealAShortStack()
    {
        // 3x5 + 2x25 + 1x50 + 1x100 = 215 units, against a 1000 target.
        List<DenominationStock> stock = Case(fives: 3, twentyFives: 2, fifties: 1, hundreds: 1);

        ChipDistribution result = ChipDistributionCalculator.Calculate(1000, stock);

        result.IsComplete.ShouldBeFalse();
        result.AllocatedUnits.ShouldBe(215);
        result.ShortfallUnits.ShouldBe(785);

        // Whatever it did allocate still has to be internally consistent.
        UnitsOf(result, stock).ShouldBe(result.AllocatedUnits);
    }

    [Fact]
    public void Should_ReportAShortfall_WhenTheTargetIsNotReachableWithTheseChips()
    {
        // Nothing smaller than a 5 can close a 3-unit gap, however much stock
        // there is. Reported, not rounded away: a stack short by 3 units puts the
        // table's reconciliation out by 3 units at the end of the night.
        List<DenominationStock> stock = Case();

        ChipDistribution result = ChipDistributionCalculator.Calculate(1003, stock);

        result.IsComplete.ShouldBeFalse();
        result.ShortfallUnits.ShouldBe(3);
        result.AllocatedUnits.ShouldBe(1000);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_AllocateNothing_ForANonPositiveTarget(long target)
    {
        ChipDistribution result = ChipDistributionCalculator.Calculate(target, Case());

        result.Chips.ShouldBeEmpty();
        result.AllocatedUnits.ShouldBe(0);
    }

    [Fact]
    public void Should_ReportTheWholeTarget_WhenTheCaseIsEmpty()
    {
        ChipDistribution result = ChipDistributionCalculator.Calculate(1000, Case(0, 0, 0, 0));

        result.ShortfallUnits.ShouldBe(1000);
        result.Chips.ShouldBeEmpty();
    }

    [Fact]
    public void Should_WorkWithASingleDenomination()
    {
        List<DenominationStock> stock =
        [
            new DenominationStock { DenominationId = Hundred, EffectiveValue = 100, Available = 50, ProfileShare = 1 }
        ];

        ChipDistribution result = ChipDistributionCalculator.Calculate(1000, stock);

        CountOf(result, Hundred).ShouldBe(10);
        result.IsComplete.ShouldBeTrue();
    }

    [Theory]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(2000)]
    [InlineData(3000)]
    [InlineData(10000)]
    public void Should_AlwaysBalance_AcrossTypicalStackSizes(long target)
    {
        List<DenominationStock> stock = Case();

        ChipDistribution result = ChipDistributionCalculator.Calculate(target, stock);

        // The invariant the whole night's bookkeeping rests on.
        (UnitsOf(result, stock) + result.ShortfallUnits).ShouldBe(target);
        result.AllocatedUnits.ShouldBe(UnitsOf(result, stock));
    }

    [Fact]
    public void Should_DealSuccessiveStacksUntilTheCaseRunsDry()
    {
        // Walks a real night: hand out stacks from one case, deducting as it goes,
        // and check the case is never overdrawn and every full stack is exact.
        List<DenominationStock> stock = Case(20, 20, 20, 20);
        var remaining = stock.ToDictionary(d => d.DenominationId, d => d.Available);
        int fullStacks = 0;

        for (int i = 0; i < 10; i++)
        {
            List<DenominationStock> current =
            [
                .. stock.Select(d => d with { Available = remaining[d.DenominationId] })
            ];

            ChipDistribution result = ChipDistributionCalculator.Calculate(1000, current);

            foreach (ChipCount chip in result.Chips)
            {
                remaining[chip.DenominationId] -= chip.Quantity;
                remaining[chip.DenominationId].ShouldBeGreaterThanOrEqualTo(0);
            }

            if (result.IsComplete)
            {
                fullStacks++;
            }
        }

        // 20 of each of 5/25/50/100 is 3500 units, so three 1000-unit stacks come
        // out whole and the rest come up short — which is the point: it says so
        // instead of dealing a short stack.
        fullStacks.ShouldBe(3);
    }

    [Fact]
    public void DefaultProfile_Should_SumToOneAndRampUpwards()
    {
        IReadOnlyList<double> profile = ChipDistributionCalculator.DefaultProfile(4);

        profile.Sum().ShouldBe(1.0, 0.0001);
        profile.ShouldBe([0.1, 0.2, 0.3, 0.4], 0.0001);
    }

    [Fact]
    public void DefaultProfile_Should_HandleAnEmptyCase()
    {
        ChipDistributionCalculator.DefaultProfile(0).ShouldBeEmpty();
    }
}
