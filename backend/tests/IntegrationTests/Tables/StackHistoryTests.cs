using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Tables;

/// <summary>
/// Every buy-in and rebuy a player was actually dealt, chips and all — the
/// screen a player checks against what is physically in front of them.
/// </summary>
public sealed class StackHistoryTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record InviteDto(string Code);

    private sealed record TableDetail(Player[] Players);

    private sealed record Player(Guid TablePlayerId, string DisplayName);

    private sealed record StackEntry(Guid LedgerEntryId, bool IsRebuy, decimal Money, Chip[] Chips);

    private sealed record Chip(Guid DenominationId, int FaceValue, int EffectiveValue, int Quantity);

    private async Task<(Guid ChampionshipId, Guid TableId, AccessTokens Owner, AccessTokens Friend)> StartedTableAsync()
    {
        (Guid _, AccessTokens owner) = await RegisterAndLoginAsync();
        Authenticate(owner.AccessToken);
        await HttpClient.PutAsJsonAsync(
            "users/me/profile",
            new { displayName = "Dono", paymentType = (string?)null, paymentHandle = (string?)null });

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
                    new { faceValue = 5, effectiveValue = 5, quantity = 400, colour = "white" },
                    new { faceValue = 100, effectiveValue = 100, quantity = 200, colour = "black" }
                }
            });
        Guid chipSetId = await chipSet.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage inviteResponse = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/invites",
            new { role = "Player", expiresAtUtc = (DateTime?)null, maxUses = (int?)null });
        InviteDto? invite = await inviteResponse.Content.ReadFromJsonAsync<InviteDto>();

        (Guid _, AccessTokens friend) = await RegisterAndLoginAsync();
        Authenticate(friend.AccessToken);
        await HttpClient.PutAsJsonAsync(
            "users/me/profile",
            new { displayName = "Amigo", paymentType = (string?)null, paymentHandle = (string?)null });
        await HttpClient.PostAsJsonAsync("championships/join", new { code = invite!.Code });

        Authenticate(owner.AccessToken);
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

        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });

        Authenticate(friend.AccessToken);
        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });

        Authenticate(owner.AccessToken);
        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/start", new { });

        return (championshipId, tableId, owner, friend);
    }

    [Fact]
    public async Task History_Should_ListTheOpeningBuyIn()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner, AccessTokens _) =
            await StartedTableAsync();

        Authenticate(owner.AccessToken);
        TableDetail? table = await HttpClient.GetFromJsonAsync<TableDetail>(
            $"championships/{championshipId}/tables/{tableId}");
        Guid myPlayer = table!.Players.Single(p => p.DisplayName == "Dono").TablePlayerId;

        StackEntry[]? history = await HttpClient.GetFromJsonAsync<StackEntry[]>(
            $"championships/{championshipId}/tables/{tableId}/players/{myPlayer}/stacks");

        StackEntry entry = history!.Single();
        entry.IsRebuy.ShouldBeFalse();
        entry.Money.ShouldBe(50m);
        entry.Chips.Sum(c => (long)c.Quantity * c.EffectiveValue).ShouldBe(1000);
    }

    [Fact]
    public async Task History_Should_PutTheLatestRebuyFirst()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner, AccessTokens _) =
            await StartedTableAsync();

        Authenticate(owner.AccessToken);
        TableDetail? table = await HttpClient.GetFromJsonAsync<TableDetail>(
            $"championships/{championshipId}/tables/{tableId}");
        Guid myPlayer = table!.Players.Single(p => p.DisplayName == "Dono").TablePlayerId;

        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/stacks",
            new { tablePlayerId = myPlayer, isRebuy = true });

        StackEntry[]? history = await HttpClient.GetFromJsonAsync<StackEntry[]>(
            $"championships/{championshipId}/tables/{tableId}/players/{myPlayer}/stacks");

        history!.Length.ShouldBe(2);
        history[0].IsRebuy.ShouldBeTrue();
        history[1].IsRebuy.ShouldBeFalse();
    }

    [Fact]
    public async Task History_Should_BeAvailableToThePlayerThemselves()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner, AccessTokens friend) =
            await StartedTableAsync();

        Authenticate(owner.AccessToken);
        TableDetail? table = await HttpClient.GetFromJsonAsync<TableDetail>(
            $"championships/{championshipId}/tables/{tableId}");
        Guid friendPlayer = table!.Players.Single(p => p.DisplayName == "Amigo").TablePlayerId;

        // The friend checking their own history has no manager permission and
        // needs none — this is the same self-service rule as a rebuy.
        Authenticate(friend.AccessToken);
        HttpResponseMessage response = await HttpClient.GetAsync(
            $"championships/{championshipId}/tables/{tableId}/players/{friendPlayer}/stacks");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task History_Should_RefuseSomeoneElsesStacksToAPlainPlayer()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner, AccessTokens friend) =
            await StartedTableAsync();

        Authenticate(owner.AccessToken);
        TableDetail? table = await HttpClient.GetFromJsonAsync<TableDetail>(
            $"championships/{championshipId}/tables/{tableId}");
        Guid ownerPlayer = table!.Players.Single(p => p.DisplayName == "Dono").TablePlayerId;

        Authenticate(friend.AccessToken);
        HttpResponseMessage response = await HttpClient.GetAsync(
            $"championships/{championshipId}/tables/{tableId}/players/{ownerPlayer}/stacks");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
