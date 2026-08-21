using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Tables;

/// <summary>
/// A manager seating someone else, rather than that person joining themselves —
/// the only way onto an <c>InviteOnly</c> table, and available on any table as
/// an alternative to a join code.
/// </summary>
public sealed class AddPlayerTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record InviteDto(string Code);

    private sealed record TableDetail(Player[] Players);

    private sealed record Player(Guid TablePlayerId, Guid UserId, string DisplayName, string Status);

    /// <summary>A championship with a table, owned by the caller, with nobody at it yet.</summary>
    private async Task<(Guid ChampionshipId, Guid TableId, AccessTokens Owner)> SetUpAsync(
        string joinPolicy = "InviteOnly")
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
                buyIn = 50m,
                rebuy = 50m,
                joinPolicy,
                allowLateEntry = false,
                smallChipReserve = 0
            });
        Guid tableId = await table.Content.ReadFromJsonAsync<Guid>();

        return (championshipId, tableId, owner);
    }

    /// <summary>Registers a second person and gets them into the championship, but not the table.</summary>
    private async Task<(Guid UserId, AccessTokens Tokens)> AddChampionshipMemberAsync(
        Guid championshipId,
        AccessTokens ownerTokens)
    {
        HttpResponseMessage invite = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/invites",
            new { role = "Player", expiresAtUtc = (DateTime?)null, maxUses = (int?)null });
        InviteDto? code = await invite.Content.ReadFromJsonAsync<InviteDto>();

        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        await HttpClient.PostAsJsonAsync("championships/join", new { code = code!.Code });

        Authenticate(ownerTokens.AccessToken);

        return (userId, tokens);
    }

    private Task<TableDetail?> GetTableAsync(Guid championshipId, Guid tableId) =>
        HttpClient.GetFromJsonAsync<TableDetail>($"championships/{championshipId}/tables/{tableId}");

    [Fact]
    public async Task AddPlayer_Should_SeatAChampionshipMemberInStandby()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner) = await SetUpAsync();
        (Guid userId, AccessTokens _) = await AddChampionshipMemberAsync(championshipId, owner);

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/players",
            new { userId });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        TableDetail? table = await GetTableAsync(championshipId, tableId);
        Player seated = table!.Players.Single();
        seated.UserId.ShouldBe(userId);
        seated.Status.ShouldBe("Standby");
    }

    /// <summary>
    /// The whole point of the feature: an InviteOnly table has no join code and
    /// nobody can self-serve, so this is the only door in.
    /// </summary>
    [Fact]
    public async Task AddPlayer_Should_BeTheOnlyWayOntoAnInviteOnlyTable()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner) = await SetUpAsync(joinPolicy: "InviteOnly");
        (Guid _, AccessTokens playerTokens) = await AddChampionshipMemberAsync(championshipId, owner);

        Authenticate(playerTokens.AccessToken);
        HttpResponseMessage selfJoin = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });
        selfJoin.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        TableDetail? empty = await GetTableAsync(championshipId, tableId);
        empty!.Players.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddPlayer_Should_RefuseAPlainPlayerAddingSomeoneElse()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner) = await SetUpAsync();
        (Guid _, AccessTokens playerTokens) = await AddChampionshipMemberAsync(championshipId, owner);
        (Guid thirdUserId, AccessTokens _) = await AddChampionshipMemberAsync(championshipId, owner);

        Authenticate(playerTokens.AccessToken);
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/players",
            new { userId = thirdUserId });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddPlayer_Should_RefuseSomeoneWhoIsNotAChampionshipMember()
    {
        (Guid championshipId, Guid tableId, AccessTokens _) = await SetUpAsync();
        (Guid outsiderId, AccessTokens _) = await RegisterAndLoginAsync();

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/players",
            new { userId = outsiderId });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddPlayer_Should_RefuseAddingTheSamePersonTwice()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner) = await SetUpAsync();
        (Guid userId, AccessTokens _) = await AddChampionshipMemberAsync(championshipId, owner);

        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/players", new { userId });

        HttpResponseMessage again = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/players", new { userId });

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddPlayer_Should_RefuseOnceTheTableHasMovedPastOpen()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner) = await SetUpAsync(joinPolicy: "AnyMember");
        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });
        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/start", new { });

        (Guid userId, AccessTokens _) = await AddChampionshipMemberAsync(championshipId, owner);

        // The table was created with allowLateEntry: false, so Running refuses too.
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/players", new { userId });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
