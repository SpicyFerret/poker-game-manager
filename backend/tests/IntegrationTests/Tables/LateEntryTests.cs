using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Tables;

/// <summary>
/// What happens when someone tries to sit down at a table already in play. A
/// yes/no answer turned out to be the wrong shape: "no" is what a table wants
/// early on and "yes" is what it wants when a friend turns up at ten, and
/// nobody edits a setting at the moment it matters. Asking leaves the decision
/// with whoever is running the night, at the time it comes up.
/// </summary>
public sealed class LateEntryTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record InviteDto(string Code);

    private sealed record TableDetail(Player[] Players);

    private sealed record Player(Guid TablePlayerId, Guid UserId, string DisplayName, string Status);

    /// <summary>A running table with the owner already dealt in, under the given late-entry policy.</summary>
    private async Task<(Guid ChampionshipId, Guid TableId, AccessTokens Owner)> RunningTableAsync(
        string lateEntry)
    {
        (Guid _, AccessTokens owner) = await RegisterAndLoginAsync();
        Authenticate(owner.AccessToken);

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
                    new { faceValue = 5, effectiveValue = 5, quantity = 500, colour = (string?)null },
                    new { faceValue = 100, effectiveValue = 100, quantity = 500, colour = (string?)null }
                }
            });
        Guid chipSetId = await chipSet.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage table = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables",
            new
            {
                name = "Mesa",
                chipSetId,
                buyIn = 50m,
                rebuy = 50m,
                joinPolicy = "AnyMember",
                lateEntry,
                smallChipReserve = 0
            });
        Guid tableId = await table.Content.ReadFromJsonAsync<Guid>();

        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });
        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/start", new { });

        return (championshipId, tableId, owner);
    }

    /// <summary>Registers a newcomer, gets them into the championship, and has them try the table.</summary>
    private async Task<(Guid UserId, HttpResponseMessage Join, AccessTokens Tokens)> LatecomerAsync(
        Guid championshipId,
        Guid tableId,
        AccessTokens ownerTokens)
    {
        HttpResponseMessage invite = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/invites",
            new { role = "Player", expiresAtUtc = (DateTime?)null, maxUses = (int?)null });
        InviteDto? code = await invite.Content.ReadFromJsonAsync<InviteDto>();

        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        await HttpClient.PostAsJsonAsync("championships/join", new { code = code!.Code });

        HttpResponseMessage join = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });

        Authenticate(ownerTokens.AccessToken);

        return (userId, join, tokens);
    }

    private Task<TableDetail?> GetTableAsync(Guid championshipId, Guid tableId) =>
        HttpClient.GetFromJsonAsync<TableDetail>($"championships/{championshipId}/tables/{tableId}");

    private Task<HttpResponseMessage> DecideAsync(
        Guid championshipId,
        Guid tableId,
        Guid tablePlayerId,
        bool approved) =>
        HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/players/{tablePlayerId}/decision",
            new { approved });

    [Fact]
    public async Task Blocked_Should_TurnAwayAnyoneArrivingLate()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner) = await RunningTableAsync("Blocked");

        (Guid _, HttpResponseMessage join, AccessTokens _) =
            await LatecomerAsync(championshipId, tableId, owner);

        join.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        TableDetail? table = await GetTableAsync(championshipId, tableId);
        table!.Players.Length.ShouldBe(1);
    }

    [Fact]
    public async Task Open_Should_SeatAnyoneArrivingLate_WithNobodyToAsk()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner) = await RunningTableAsync("Open");

        (Guid userId, HttpResponseMessage join, AccessTokens _) =
            await LatecomerAsync(championshipId, tableId, owner);

        join.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        TableDetail? table = await GetTableAsync(championshipId, tableId);
        table!.Players.Single(p => p.UserId == userId).Status.ShouldBe("Standby");
    }

    /// <summary>
    /// The new answer. The join succeeds — they have asked — but it parks them
    /// as Requested rather than seating them, and no chips are owed until a
    /// manager says yes.
    /// </summary>
    [Fact]
    public async Task Request_Should_ParkThemAsRequested_UntilAManagerAnswers()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner) = await RunningTableAsync("Request");

        (Guid userId, HttpResponseMessage join, AccessTokens _) =
            await LatecomerAsync(championshipId, tableId, owner);

        join.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        TableDetail? table = await GetTableAsync(championshipId, tableId);
        table!.Players.Single(p => p.UserId == userId).Status.ShouldBe("Requested");
    }

    [Fact]
    public async Task Approving_Should_SeatThemInStandby()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner) = await RunningTableAsync("Request");
        (Guid userId, HttpResponseMessage _, AccessTokens _) =
            await LatecomerAsync(championshipId, tableId, owner);

        TableDetail? pending = await GetTableAsync(championshipId, tableId);
        Guid tablePlayerId = pending!.Players.Single(p => p.UserId == userId).TablePlayerId;

        HttpResponseMessage decision = await DecideAsync(championshipId, tableId, tablePlayerId, true);

        decision.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        TableDetail? after = await GetTableAsync(championshipId, tableId);
        after!.Players.Single(p => p.UserId == userId).Status.ShouldBe("Standby");
    }

    /// <summary>
    /// Turning someone away removes the request outright rather than storing a
    /// "no", so asking again later is a fresh question and not an argument with
    /// a record.
    /// </summary>
    [Fact]
    public async Task Denying_Should_RemoveTheRequestEntirely()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner) = await RunningTableAsync("Request");
        (Guid userId, HttpResponseMessage _, AccessTokens _) =
            await LatecomerAsync(championshipId, tableId, owner);

        TableDetail? pending = await GetTableAsync(championshipId, tableId);
        Guid tablePlayerId = pending!.Players.Single(p => p.UserId == userId).TablePlayerId;

        HttpResponseMessage decision = await DecideAsync(championshipId, tableId, tablePlayerId, false);

        decision.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        TableDetail? after = await GetTableAsync(championshipId, tableId);
        after!.Players.ShouldNotContain(p => p.UserId == userId);
    }

    [Fact]
    public async Task Deciding_Should_RefuseAnyoneWithoutAPendingRequest()
    {
        (Guid championshipId, Guid tableId, AccessTokens _) = await RunningTableAsync("Request");

        TableDetail? table = await GetTableAsync(championshipId, tableId);
        Guid alreadyPlaying = table!.Players[0].TablePlayerId;

        HttpResponseMessage decision = await DecideAsync(championshipId, tableId, alreadyPlaying, true);

        decision.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Deciding_Should_RefuseAPlainPlayer()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner) = await RunningTableAsync("Request");
        (Guid userId, HttpResponseMessage _, AccessTokens askerTokens) =
            await LatecomerAsync(championshipId, tableId, owner);

        TableDetail? pending = await GetTableAsync(championshipId, tableId);
        Guid tablePlayerId = pending!.Players.Single(p => p.UserId == userId).TablePlayerId;

        // Letting yourself in would make the whole policy decorative.
        Authenticate(askerTokens.AccessToken);
        HttpResponseMessage decision = await DecideAsync(championshipId, tableId, tablePlayerId, true);

        decision.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Someone still waiting on an answer never sat down, so the reconciliation
    /// must not expect a count from them — otherwise one unanswered request
    /// would block the night from ever settling.
    /// </summary>
    [Fact]
    public async Task APendingRequest_Should_NotBlockTheCount()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner) = await RunningTableAsync("Request");
        await LatecomerAsync(championshipId, tableId, owner);

        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/counting", new { });

        TableDetail? table = await GetTableAsync(championshipId, tableId);
        Player playing = table!.Players.Single(p => p.Status == "Playing");

        // The one person who actually played reports; nobody else is expected.
        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/counts",
            new
            {
                tablePlayerId = playing.TablePlayerId,
                counts = Array.Empty<object>()
            });

        Reconciliation? reconciliation = await HttpClient.GetFromJsonAsync<Reconciliation>(
            $"championships/{championshipId}/tables/{tableId}/reconciliation");

        reconciliation!.AwaitingCountFrom.ShouldBeEmpty();
        reconciliation.EveryoneHasCounted.ShouldBeTrue();
    }

    private sealed record Reconciliation(
        AwaitingPlayer[] AwaitingCountFrom,
        bool EveryoneHasCounted,
        bool ChipsBalance,
        bool CanSettle);

    private sealed record AwaitingPlayer(Guid TablePlayerId, string DisplayName);
}
