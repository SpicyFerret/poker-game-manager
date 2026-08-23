using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Tables;

public sealed class BlindClockTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record Blinds(Level[] Levels, Clock? Clock);

    private sealed record Level(int Order, int SmallBlind, int BigBlind, int Ante, int DurationSeconds);

    private sealed record Clock(int CurrentLevel, bool IsPaused, int ElapsedSeconds, DateTime ServerTimeUtc);

    private static readonly object[] Ladder =
    [
        new { smallBlind = 5, bigBlind = 10, ante = 0, durationSeconds = 900 },
        new { smallBlind = 10, bigBlind = 20, ante = 0, durationSeconds = 900 },
        new { smallBlind = 25, bigBlind = 50, ante = 5, durationSeconds = 600 }
    ];

    private async Task<(Guid ChampionshipId, Guid TableId)> TableAsync()
    {
        (Guid _, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        HttpResponseMessage created = await HttpClient.PostAsJsonAsync("championships", new
        {
            name = "Quinta",
            defaultBuyIn = 50m,
            defaultRebuy = 50m,
            enforceDefaults = false,
            moneyPerUnit = 0.05m
        });
        Guid championshipId = await created.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage chipSet = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/chip-sets",
            new
            {
                name = "Maleta",
                denominations = new[]
                {
                    new { faceValue = 5, effectiveValue = 5, quantity = 200, colour = (string?)null }
                }
            });
        Guid chipSetId = await chipSet.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage table = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables",
            new
            {
                name = "Mesa",
                chipSetId,
                buyIn = 5m,
                rebuy = 5m,
                joinPolicy = "AnyMember",
                lateEntry = "Open",
                smallChipReserve = 0
            });

        return (championshipId, await table.Content.ReadFromJsonAsync<Guid>());
    }

    private Task<Blinds?> GetBlindsAsync(Guid championshipId, Guid tableId) =>
        HttpClient.GetFromJsonAsync<Blinds>($"championships/{championshipId}/tables/{tableId}/blinds");

    [Fact]
    public async Task ATableWithoutLevels_Should_HaveNoClockAtAll()
    {
        // The clock is optional, and most casual nights never turn it on.
        (Guid championshipId, Guid tableId) = await TableAsync();

        Blinds? blinds = await GetBlindsAsync(championshipId, tableId);

        blinds!.Levels.ShouldBeEmpty();
        blinds.Clock.ShouldBeNull();
    }

    [Fact]
    public async Task Clock_Should_BeRefused_WhenThereAreNoLevels()
    {
        (Guid championshipId, Guid tableId) = await TableAsync();

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/clock",
            new { action = "Start" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Levels_Should_RoundTripInOrder()
    {
        (Guid championshipId, Guid tableId) = await TableAsync();

        HttpResponseMessage set = await HttpClient.PutAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/blinds",
            new { levels = Ladder });
        set.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        Blinds? blinds = await GetBlindsAsync(championshipId, tableId);

        blinds!.Levels.Select(l => l.Order).ShouldBe([1, 2, 3]);
        blinds.Levels[2].BigBlind.ShouldBe(50);
        blinds.Levels[2].Ante.ShouldBe(5);

        // Setting levels alone does not start anything.
        blinds.Clock.ShouldBeNull();
    }

    [Fact]
    public async Task Clock_Should_StartPauseAndAdvance()
    {
        (Guid championshipId, Guid tableId) = await TableAsync();

        await HttpClient.PutAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/blinds", new { levels = Ladder });

        string clockUrl = $"championships/{championshipId}/tables/{tableId}/clock";

        await HttpClient.PostAsJsonAsync(clockUrl, new { action = "Start" });

        Blinds? running = await GetBlindsAsync(championshipId, tableId);
        running!.Clock.ShouldNotBeNull();
        running.Clock.CurrentLevel.ShouldBe(1);
        running.Clock.IsPaused.ShouldBeFalse();

        // Sent so each phone can keep ticking between polls without drifting.
        running.Clock.ServerTimeUtc.Kind.ShouldBe(DateTimeKind.Utc);

        await HttpClient.PostAsJsonAsync(clockUrl, new { action = "Pause" });
        Blinds? paused = await GetBlindsAsync(championshipId, tableId);
        paused!.Clock!.IsPaused.ShouldBeTrue();

        await HttpClient.PostAsJsonAsync(clockUrl, new { action = "NextLevel" });
        Blinds? advanced = await GetBlindsAsync(championshipId, tableId);
        advanced!.Clock!.CurrentLevel.ShouldBe(2);

        // A level change restarts the timer.
        advanced.Clock.ElapsedSeconds.ShouldBe(0);
    }

    [Fact]
    public async Task Clock_Should_NotRunPastTheLastLevelOrBeforeTheFirst()
    {
        (Guid championshipId, Guid tableId) = await TableAsync();

        await HttpClient.PutAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/blinds", new { levels = Ladder });

        string clockUrl = $"championships/{championshipId}/tables/{tableId}/clock";
        await HttpClient.PostAsJsonAsync(clockUrl, new { action = "Start" });

        for (int i = 0; i < 5; i++)
        {
            await HttpClient.PostAsJsonAsync(clockUrl, new { action = "NextLevel" });
        }

        Blinds? atTop = await GetBlindsAsync(championshipId, tableId);
        atTop!.Clock!.CurrentLevel.ShouldBe(3);

        for (int i = 0; i < 5; i++)
        {
            await HttpClient.PostAsJsonAsync(clockUrl, new { action = "PreviousLevel" });
        }

        Blinds? atBottom = await GetBlindsAsync(championshipId, tableId);
        atBottom!.Clock!.CurrentLevel.ShouldBe(1);
    }

    [Fact]
    public async Task ClearingTheLevels_Should_TakeTheClockWithThem()
    {
        // Otherwise the clock would be left counting against a ladder that no
        // longer exists.
        (Guid championshipId, Guid tableId) = await TableAsync();

        await HttpClient.PutAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/blinds", new { levels = Ladder });
        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/clock", new { action = "Start" });

        (await GetBlindsAsync(championshipId, tableId))!.Clock.ShouldNotBeNull();

        await HttpClient.PutAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/blinds", new { levels = Array.Empty<object>() });

        Blinds? after = await GetBlindsAsync(championshipId, tableId);
        after!.Levels.ShouldBeEmpty();
        after.Clock.ShouldBeNull();
    }

    [Fact]
    public async Task Levels_Should_BeRefused_WhenTheBlindsMakeNoSense()
    {
        (Guid championshipId, Guid tableId) = await TableAsync();

        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/blinds",
            new { levels = new[] { new { smallBlind = 0, bigBlind = 10, ante = 0, durationSeconds = 900 } } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
