using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Tables;

/// <summary>
/// The notice a player gets when chips are handed to them: what they should be
/// holding, and their confirmation that they are.
/// </summary>
public sealed class StackNoticeTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record InviteDto(string Code);

    private sealed record TableDetail(Player[] Players, PendingStack[] PendingStacks);

    private sealed record Player(Guid TablePlayerId, string DisplayName);

    private sealed record PendingStack(Guid LedgerEntryId, bool IsRebuy, decimal Money, Chip[] Chips);

    private sealed record Chip(Guid DenominationId, int FaceValue, int EffectiveValue, int Quantity);

    private static readonly int[] PointsByPosition = [10, 7];

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
            moneyPerUnit = 0.05m,
            pointsByPosition = PointsByPosition
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
                    new { faceValue = 25, effectiveValue = 25, quantity = 200, colour = "red" },
                    new { faceValue = 50, effectiveValue = 50, quantity = 200, colour = "green" },
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

    private Task<TableDetail?> GetTableAsync(Guid championshipId, Guid tableId) =>
        HttpClient.GetFromJsonAsync<TableDetail>($"championships/{championshipId}/tables/{tableId}");

    [Fact]
    public async Task Starting_Should_LeaveEveryPlayerAStackToConfirm()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner, AccessTokens friend) =
            await StartedTableAsync();

        // Each player sees their own stack, and only their own.
        Authenticate(owner.AccessToken);
        TableDetail? mine = await GetTableAsync(championshipId, tableId);

        PendingStack notice = mine!.PendingStacks.Single();
        notice.IsRebuy.ShouldBeFalse();
        notice.Money.ShouldBe(50m);
        notice.Chips.ShouldNotBeEmpty();

        // 50 reais at 0.05 per unit is a 1000-unit stack, whatever chips it is
        // made of.
        notice.Chips.Sum(chip => chip.Quantity * chip.EffectiveValue).ShouldBe(1000);

        // Biggest first: that is how anyone stacks chips to count them.
        notice.Chips
            .Select(chip => chip.EffectiveValue)
            .ShouldBe(notice.Chips.Select(chip => chip.EffectiveValue).OrderByDescending(v => v));

        Authenticate(friend.AccessToken);
        TableDetail? theirs = await GetTableAsync(championshipId, tableId);

        theirs!.PendingStacks.Single().LedgerEntryId.ShouldNotBe(notice.LedgerEntryId);
    }

    [Fact]
    public async Task Acknowledging_Should_ClearOnlyThatNotice()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner, AccessTokens friend) =
            await StartedTableAsync();

        Authenticate(owner.AccessToken);
        TableDetail? before = await GetTableAsync(championshipId, tableId);
        Guid entryId = before!.PendingStacks.Single().LedgerEntryId;

        HttpResponseMessage acknowledged = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/stacks/{entryId}/acknowledge",
            new { });
        acknowledged.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        TableDetail? after = await GetTableAsync(championshipId, tableId);
        after!.PendingStacks.ShouldBeEmpty();

        // Confirming again is what a double tap looks like, and must not fail.
        HttpResponseMessage again = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/stacks/{entryId}/acknowledge",
            new { });
        again.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The other player's notice is untouched.
        Authenticate(friend.AccessToken);
        TableDetail? theirs = await GetTableAsync(championshipId, tableId);
        theirs!.PendingStacks.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Rebuying_Should_QueueASecondNotice()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner, AccessTokens _) =
            await StartedTableAsync();

        Authenticate(owner.AccessToken);
        TableDetail? started = await GetTableAsync(championshipId, tableId);
        Guid ownerPlayer = started!.Players.Single(p => p.DisplayName == "Dono").TablePlayerId;

        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/stacks",
            new { tablePlayerId = ownerPlayer, isRebuy = true });

        TableDetail? queued = await GetTableAsync(championshipId, tableId);

        // Both waiting, buy-in first: they are a queue, oldest first, not a
        // single latest notice.
        queued!.PendingStacks.Length.ShouldBe(2);
        queued.PendingStacks[0].IsRebuy.ShouldBeFalse();
        queued.PendingStacks[1].IsRebuy.ShouldBeTrue();
    }

    [Fact]
    public async Task Acknowledging_Should_BeRefusedForSomeoneElsesStack()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner, AccessTokens friend) =
            await StartedTableAsync();

        Authenticate(owner.AccessToken);
        TableDetail? mine = await GetTableAsync(championshipId, tableId);
        Guid myEntry = mine!.PendingStacks.Single().LedgerEntryId;

        // The friend is a player at this table, but these are not their chips —
        // and the owner runs the table, so this is not about permissions.
        Authenticate(friend.AccessToken);
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/stacks/{myEntry}/acknowledge",
            new { });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        Authenticate(owner.AccessToken);
        TableDetail? after = await GetTableAsync(championshipId, tableId);
        after!.PendingStacks.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Counting_Should_StopShowingNotices()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner, AccessTokens _) =
            await StartedTableAsync();

        Authenticate(owner.AccessToken);
        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/counting", new { });

        TableDetail? counting = await GetTableAsync(championshipId, tableId);

        // Nobody needs telling what they were dealt once the night is being
        // counted up; the reconciliation is the check by then.
        counting!.PendingStacks.ShouldBeEmpty();
    }
}
