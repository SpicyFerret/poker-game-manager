using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Tables;

/// <summary>
/// A manager taking someone back off the table — the mirror of adding them by
/// hand, and bounded by the same thing the whole night's bookkeeping is: once
/// chips have left the case for a player, they belong to the table's books and
/// cannot simply be deleted out of them.
/// </summary>
public sealed class RemovePlayerTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record InviteDto(string Code);

    private sealed record TableDetail(Player[] Players);

    private sealed record Player(Guid TablePlayerId, Guid UserId, string DisplayName, string Status);

    private async Task<(Guid ChampionshipId, Guid TableId, AccessTokens Owner)> SetUpAsync()
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
                lateEntry = "Open",
                smallChipReserve = 0
            });
        Guid tableId = await table.Content.ReadFromJsonAsync<Guid>();

        return (championshipId, tableId, owner);
    }

    private async Task<(Guid UserId, AccessTokens Tokens)> AddPlayerToTableAsync(
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
        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });

        Authenticate(ownerTokens.AccessToken);

        return (userId, tokens);
    }

    private Task<TableDetail?> GetTableAsync(Guid championshipId, Guid tableId) =>
        HttpClient.GetFromJsonAsync<TableDetail>($"championships/{championshipId}/tables/{tableId}");

    private Task<HttpResponseMessage> RemoveAsync(Guid championshipId, Guid tableId, Guid tablePlayerId) =>
        HttpClient.DeleteAsync(
            new Uri($"championships/{championshipId}/tables/{tableId}/players/{tablePlayerId}", UriKind.Relative));

    [Fact]
    public async Task RemovePlayer_Should_TakeAWaitingPlayerOffTheTable()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner) = await SetUpAsync();
        (Guid userId, AccessTokens _) = await AddPlayerToTableAsync(championshipId, tableId, owner);

        TableDetail? before = await GetTableAsync(championshipId, tableId);
        Guid tablePlayerId = before!.Players.Single(p => p.UserId == userId).TablePlayerId;

        HttpResponseMessage response = await RemoveAsync(championshipId, tableId, tablePlayerId);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        TableDetail? after = await GetTableAsync(championshipId, tableId);
        after!.Players.ShouldNotContain(p => p.UserId == userId);
    }

    /// <summary>
    /// The obvious half: that player paid in and chips left the case for them, so
    /// deleting the row would leave those chips belonging to nobody and the night
    /// could never be reconciled.
    /// </summary>
    [Fact]
    public async Task RemovePlayer_Should_RefuseOnceThePlayerHasBeenDealtIn()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner) = await SetUpAsync();
        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });
        await AddPlayerToTableAsync(championshipId, tableId, owner);

        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/start", new { });

        TableDetail? table = await GetTableAsync(championshipId, tableId);
        Guid dealtIn = table!.Players[0].TablePlayerId;

        HttpResponseMessage response = await RemoveAsync(championshipId, tableId, dealtIn);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        TableDetail? after = await GetTableAsync(championshipId, tableId);
        after!.Players.ShouldContain(p => p.TablePlayerId == dealtIn);
    }

    /// <summary>
    /// Only before the night starts. A late entrant sitting in standby at a
    /// running table has received no chips, but taking them off is a decision
    /// about a night in progress rather than a correction to who turned up — the
    /// way out of a running table is to cash out, which leaves a record.
    /// </summary>
    [Fact]
    public async Task RemovePlayer_Should_RefuseOnceTheTableHasStarted()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner) = await SetUpAsync();
        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });
        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/start", new { });

        (Guid lateUserId, AccessTokens _) = await AddPlayerToTableAsync(championshipId, tableId, owner);

        TableDetail? table = await GetTableAsync(championshipId, tableId);
        Player late = table!.Players.Single(p => p.UserId == lateUserId);
        late.Status.ShouldBe("Standby");

        HttpResponseMessage response = await RemoveAsync(championshipId, tableId, late.TablePlayerId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        TableDetail? after = await GetTableAsync(championshipId, tableId);
        after!.Players.ShouldContain(p => p.UserId == lateUserId);
    }

    [Fact]
    public async Task RemovePlayer_Should_RefuseAPlainPlayerRemovingSomeoneElse()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner) = await SetUpAsync();
        (Guid _, AccessTokens playerTokens) = await AddPlayerToTableAsync(championshipId, tableId, owner);
        (Guid otherUserId, AccessTokens _) = await AddPlayerToTableAsync(championshipId, tableId, owner);

        TableDetail? table = await GetTableAsync(championshipId, tableId);
        Guid target = table!.Players.Single(p => p.UserId == otherUserId).TablePlayerId;

        Authenticate(playerTokens.AccessToken);
        HttpResponseMessage response = await RemoveAsync(championshipId, tableId, target);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RemovePlayer_Should_NotFindSomeoneWhoIsNotAtThisTable()
    {
        (Guid championshipId, Guid tableId, AccessTokens _) = await SetUpAsync();

        HttpResponseMessage response = await RemoveAsync(championshipId, tableId, Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
