using Application.Tables;
using Domain.ChipSets;
using Domain.Tables;
using Domain.Tables.Services;

namespace Application.UnitTests.Tables;

public sealed class ChipStockTests
{
    private static readonly Guid Five = Guid.NewGuid();
    private static readonly Guid Hundred = Guid.NewGuid();

    private static List<ChipDenomination> Denominations(int fives = 100, int hundreds = 40) =>
    [
        new ChipDenomination { Id = Five, EffectiveValue = 5, FaceValue = 5, Quantity = fives },
        new ChipDenomination { Id = Hundred, EffectiveValue = 100, FaceValue = 100, Quantity = hundreds }
    ];

    private static int AvailableOf(IReadOnlyList<DenominationStock> stock, Guid id) =>
        stock.Single(s => s.DenominationId == id).Available;

    [Fact]
    public void Available_Should_SubtractWhatHasBeenIssued()
    {
        IReadOnlyList<DenominationStock> stock = ChipStock.Available(
            Denominations(),
            new Dictionary<Guid, int> { [Five] = 30, [Hundred] = 10 });

        AvailableOf(stock, Five).ShouldBe(70);
        AvailableOf(stock, Hundred).ShouldBe(30);
    }

    [Fact]
    public void Available_Should_HoldBackTheReserve_FromTheSmallestDenominationOnly()
    {
        // The chips that run out first are the ones the game needs most, so the
        // reserve exists to keep the first rebuys bettable.
        IReadOnlyList<DenominationStock> stock = ChipStock.Available(
            Denominations(),
            new Dictionary<Guid, int>(),
            smallChipReserve: 25);

        AvailableOf(stock, Five).ShouldBe(75);
        AvailableOf(stock, Hundred).ShouldBe(40);
    }

    [Fact]
    public void Available_Should_NeverGoNegative()
    {
        // A reserve larger than the case, or an over-issue, must clamp rather than
        // hand the calculator a negative stock to reason about.
        IReadOnlyList<DenominationStock> stock = ChipStock.Available(
            Denominations(fives: 10),
            new Dictionary<Guid, int> { [Five] = 8 },
            smallChipReserve: 50);

        AvailableOf(stock, Five).ShouldBe(0);
    }

    [Fact]
    public void IssuedByDenomination_Should_IgnoreEntriesThatMovedNoChips()
    {
        // This is the whole reason a chip trade between players reconciles: it has
        // money on it and no chip rows, so the case is untouched by it.
        var entries = new List<LedgerEntry>
        {
            new()
            {
                Type = LedgerEntryType.BuyIn,
                MoneyAmount = 50,
                Chips = [new LedgerEntryChip { ChipDenominationId = Five, Quantity = 10 }]
            },
            new() { Type = LedgerEntryType.ChipPurchaseFromPlayer, MoneyAmount = 50 },
            new() { Type = LedgerEntryType.ChipSaleToPlayer, MoneyAmount = 50 }
        };

        Dictionary<Guid, int> issued = ChipStock.IssuedByDenomination(entries);

        issued[Five].ShouldBe(10);
        issued.ContainsKey(Hundred).ShouldBeFalse();
    }

    [Fact]
    public void IssuedByDenomination_Should_AddUpAcrossEntries()
    {
        var entries = new List<LedgerEntry>
        {
            new() { Chips = [new LedgerEntryChip { ChipDenominationId = Five, Quantity = 10 }] },
            new() { Chips = [new LedgerEntryChip { ChipDenominationId = Five, Quantity = 4 }] }
        };

        ChipStock.IssuedByDenomination(entries)[Five].ShouldBe(14);
    }
}
