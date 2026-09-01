using Application.Abstractions.Realtime;
using Application.ChipSets;
using Application.ChipSets.Update;
using Application.UnitTests.Abstractions;
using Domain.Championships;
using Domain.ChipSets;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.ChipSets;

public sealed class UpdateChipSetCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid ChampionshipId = Guid.NewGuid();
    private static readonly Guid ChipSetId = Guid.NewGuid();
    private static readonly Guid FiveId = Guid.NewGuid();
    private static readonly Guid TwentyFiveId = Guid.NewGuid();

    /// <summary>
    /// Seeds through one context and hands back a fresh one, so the handler sees
    /// the chip set the way a real request does — loaded from storage rather than
    /// already sitting in the change tracker.
    /// </summary>
    private static async Task<TestDbContext> SeedAsync()
    {
        string databaseName = NewDatabaseName();

        await using (TestDbContext seed = CreateDbContext(databaseName))
        {
            seed.ChipSets.Add(BuildChipSet());
            await seed.SaveChangesAsync();
        }

        return CreateDbContext(databaseName);
    }

    private static ChipSet BuildChipSet()
    {
        return new ChipSet
        {
            Id = ChipSetId,
            ChampionshipId = ChampionshipId,
            Name = "Maleta velha",
            CreatedAtUtc = DateTime.UtcNow,
            Denominations =
            [
                new ChipDenomination
                {
                    Id = FiveId, ChipSetId = ChipSetId, FaceValue = 5, EffectiveValue = 5, Quantity = 100
                },
                new ChipDenomination
                {
                    Id = TwentyFiveId, ChipSetId = ChipSetId, FaceValue = 25, EffectiveValue = 25, Quantity = 80
                }
            ]
        };
    }

    private static UpdateChipSetCommandHandler HandlerFor(TestDbContext context, ChampionshipRole role) =>
        new(context, new FakeChampionshipContext(role), Substitute.For<IChampionshipActivityNotifier>());

    [Fact]
    public async Task Handle_Should_Fail_WhenCallerIsOnlyATableManager()
    {
        await using TestDbContext context = await SeedAsync();

        Result result = await HandlerFor(context, ChampionshipRole.TableManager).Handle(
            new UpdateChipSetCommand(ChampionshipId, ChipSetId, "Nova", [new ChipDenominationModel(5, 5, 10, null)]),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ChampionshipErrors.InsufficientRole(ChampionshipRole.Admin));
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenTwoDenominationsShareAFaceValue()
    {
        await using TestDbContext context = await SeedAsync();

        Result result = await HandlerFor(context, ChampionshipRole.Admin).Handle(
            new UpdateChipSetCommand(ChampionshipId, ChipSetId, "Maleta",
            [
                new ChipDenominationModel(5, 5, 100, null),
                new ChipDenominationModel(5, 100, 50, null)
            ]),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ChipSetErrors.DuplicateFaceValue);
    }

    [Fact]
    public async Task Handle_Should_KeepIdsOfDenominationsThatSurviveTheEdit()
    {
        // From Phase 2 the ledger records chips issued per denomination id, so
        // recreating rows on every edit would orphan the history of every table
        // already played with this case.
        await using TestDbContext context = await SeedAsync();

        Result result = await HandlerFor(context, ChampionshipRole.Admin).Handle(
            new UpdateChipSetCommand(ChampionshipId, ChipSetId, "Maleta nova",
            [
                // Same chip, but now counted as 100 in play — the override.
                new ChipDenominationModel(5, 100, 120, "Vermelha"),
                new ChipDenominationModel(100, 100, 40, "Preta")
            ]),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        ChipSet chipSet = await context.ChipSets.Include(s => s.Denominations).SingleAsync();
        chipSet.Name.ShouldBe("Maleta nova");
        chipSet.Denominations.Count.ShouldBe(2);

        ChipDenomination five = chipSet.Denominations.Single(d => d.FaceValue == 5);
        five.Id.ShouldBe(FiveId);
        five.EffectiveValue.ShouldBe(100);
        five.Quantity.ShouldBe(120);
        five.Colour.ShouldBe("Vermelha");

        // The 25s were left out of the new list, so they are gone.
        chipSet.Denominations.ShouldNotContain(d => d.Id == TwentyFiveId);
        chipSet.Denominations.ShouldContain(d => d.FaceValue == 100);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenTheChipSetBelongsToAnotherChampionship()
    {
        await using TestDbContext context = await SeedAsync();

        Result result = await HandlerFor(context, ChampionshipRole.Admin).Handle(
            new UpdateChipSetCommand(Guid.NewGuid(), ChipSetId, "Maleta", [new ChipDenominationModel(5, 5, 10, null)]),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ChipSetErrors.NotFound(ChipSetId));
    }
}
