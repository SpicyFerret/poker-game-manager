using Application.Seasons.Create;
using Application.UnitTests.Abstractions;
using Domain.Championships;
using Domain.Seasons;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Seasons;

public sealed class CreateSeasonCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid ChampionshipId = Guid.NewGuid();

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private static async Task<TestDbContext> SeedAsync(DateOnly startsOn, DateOnly? endsOn)
    {
        TestDbContext context = CreateDbContext();

        context.Seasons.Add(new Season
        {
            Id = Guid.NewGuid(),
            ChampionshipId = ChampionshipId,
            Name = "Existing",
            StartsOn = startsOn,
            EndsOn = endsOn
        });

        await context.SaveChangesAsync();

        return context;
    }

    private static CreateSeasonCommandHandler HandlerFor(TestDbContext context) =>
        new(context, new FakeChampionshipContext(ChampionshipRole.Admin));

    [Fact]
    public async Task Handle_Should_Fail_WhenCallerIsOnlyAPlayer()
    {
        await using TestDbContext context = CreateDbContext();
        var handler = new CreateSeasonCommandHandler(context, new FakeChampionshipContext(ChampionshipRole.Player));

        Result<Guid> result = await handler.Handle(
            new CreateSeasonCommand(ChampionshipId, "2027", D(2027, 1, 1), null),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ChampionshipErrors.InsufficientRole(ChampionshipRole.Admin));
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenItEndsBeforeItStarts()
    {
        await using TestDbContext context = CreateDbContext();

        Result<Guid> result = await HandlerFor(context).Handle(
            new CreateSeasonCommand(ChampionshipId, "Backwards", D(2026, 6, 1), D(2026, 5, 1)),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SeasonErrors.EndsBeforeItStarts);
    }

    public static TheoryData<DateOnly, DateOnly?> Overlapping => new()
    {
        // Straddles the start, straddles the end, sits inside, and swallows it.
        { new DateOnly(2025, 12, 1), new DateOnly(2026, 1, 15) },
        { new DateOnly(2026, 12, 1), new DateOnly(2027, 1, 15) },
        { new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30) },
        { new DateOnly(2025, 1, 1), new DateOnly(2028, 1, 1) },
        // Shares a single day at each boundary.
        { new DateOnly(2026, 12, 31), null },
        { new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 1) },
    };

    [Theory]
    [MemberData(nameof(Overlapping))]
    public async Task Handle_Should_Fail_WhenTheRangeOverlapsAnExistingSeason(
        DateOnly startsOn,
        DateOnly? endsOn)
    {
        // Overlap would let one table's result count towards two rankings, so the
        // season totals would stop adding up.
        await using TestDbContext context = await SeedAsync(D(2026, 1, 1), D(2026, 12, 31));

        Result<Guid> result = await HandlerFor(context).Handle(
            new CreateSeasonCommand(ChampionshipId, "Clash", startsOn, endsOn),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SeasonErrors.Overlaps);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenAnOpenEndedSeasonIsAlreadyRunning()
    {
        // An open-ended season runs forever, so anything starting after it clashes.
        await using TestDbContext context = await SeedAsync(D(2026, 1, 1), null);

        Result<Guid> result = await HandlerFor(context).Handle(
            new CreateSeasonCommand(ChampionshipId, "Later", D(2030, 1, 1), null),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SeasonErrors.Overlaps);
    }

    [Theory]
    [InlineData(2027, 1, 1)]
    [InlineData(2025, 1, 1)]
    public async Task Handle_Should_Create_WhenTheRangeIsClear(int year, int month, int day)
    {
        await using TestDbContext context = await SeedAsync(D(2026, 1, 1), D(2026, 12, 31));

        Result<Guid> result = await HandlerFor(context).Handle(
            new CreateSeasonCommand(ChampionshipId, "Clear", D(year, month, day), D(year, 6, 30)),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await context.Seasons.CountAsync()).ShouldBe(2);
    }
}
