using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Tables;

public sealed class SettlementTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record Invite(string Code);

    private sealed record TableDetail(string Status, Player[] Players, Stock[] Stock);

    private sealed record Player(Guid TablePlayerId, string DisplayName, decimal PaidIn);

    private sealed record Stock(Guid DenominationId, int FaceValue, int EffectiveValue, int Issued);

    private sealed record Reconciliation(
        Line[] Lines,
        Awaiting[] AwaitingCountFrom,
        bool EveryoneHasCounted,
        bool ChipsBalance,
        bool CanSettle);

    private sealed record Line(Guid DenominationId, int FaceValue, int Issued, int Counted, int Difference);

    private sealed record Awaiting(Guid TablePlayerId, string DisplayName);

    private sealed record SettlementDto(Transfer[] Transfers, ResultDto[] Results);

    private sealed record Transfer(
        string FromDisplayName,
        string ToDisplayName,
        decimal Amount,
        string? ToPaymentType,
        string? ToPaymentHandle);

    private sealed record ResultDto(string DisplayName, int Position, int Points, decimal Balance);

    /// <summary>Hoisted so it is not rebuilt for every table this file sets up.</summary>
    private static readonly int[] PointsByPosition = [10, 7];

    /// <summary>
    /// A two-player table, started, with both holding their opening stacks.
    /// Returns the ids needed to drive the rest of the night.
    /// </summary>
    private async Task<(Guid ChampionshipId, Guid TableId, AccessTokens Owner, AccessTokens Friend)> StartedTableAsync()
    {
        (Guid _, AccessTokens owner) = await RegisterAndLoginAsync();
        Authenticate(owner.AccessToken);

        // The owner gets a payment handle; the friend does not, so the settlement
        // has to cope with both.
        await HttpClient.PutAsJsonAsync(
            "users/me/profile",
            new { displayName = "Dono", paymentType = "Pix", paymentHandle = "dono@pix" });

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
                    new { faceValue = 5, effectiveValue = 5, quantity = 200, colour = (string?)null },
                    new { faceValue = 25, effectiveValue = 25, quantity = 200, colour = (string?)null },
                    new { faceValue = 50, effectiveValue = 50, quantity = 200, colour = (string?)null },
                    new { faceValue = 100, effectiveValue = 100, quantity = 200, colour = (string?)null }
                }
            });
        Guid chipSetId = await chipSet.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage inviteResponse = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/invites",
            new { role = "Player", expiresAtUtc = (DateTime?)null, maxUses = (int?)null });
        Invite? invite = await inviteResponse.Content.ReadFromJsonAsync<Invite>();

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

    private Task<Reconciliation?> GetReconciliationAsync(Guid championshipId, Guid tableId) =>
        HttpClient.GetFromJsonAsync<Reconciliation>(
            $"championships/{championshipId}/tables/{tableId}/reconciliation");

    /// <summary>Reports a stack as a flat number of units, made of the chips issued.</summary>
    private async Task ReportAsync(
        Guid championshipId,
        Guid tableId,
        Guid playerId,
        Dictionary<Guid, int> counts)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/counts",
            new
            {
                tablePlayerId = playerId,
                counts = counts.Select(c => new { denominationId = c.Key, quantity = c.Value }).ToArray()
            });

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task WholeNight_Should_ReconcileAndProduceTheSettlement()
    {
        // Arrange — two players, one stack each, then the owner loses their whole
        // stack to the friend.
        (Guid championshipId, Guid tableId, AccessTokens owner, AccessTokens friend) = await StartedTableAsync();

        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/counting", new { });

        TableDetail? table = await GetTableAsync(championshipId, tableId);
        table!.Status.ShouldBe("Counting");

        Guid ownerPlayer = table.Players.Single(p => p.DisplayName == "Dono").TablePlayerId;
        Guid friendPlayer = table.Players.Single(p => p.DisplayName == "Amigo").TablePlayerId;

        // Everything issued ends up with the friend; the owner counts nothing.
        var everything = table.Stock
            .Where(s => s.Issued > 0)
            .ToDictionary(s => s.DenominationId, s => s.Issued);

        // Act — the owner reports an empty stack, the friend reports the lot.
        await ReportAsync(championshipId, tableId, ownerPlayer,
            table.Stock.ToDictionary(s => s.DenominationId, _ => 0));

        Reconciliation? midway = await GetReconciliationAsync(championshipId, tableId);
        midway!.EveryoneHasCounted.ShouldBeFalse();
        midway.CanSettle.ShouldBeFalse();
        midway.AwaitingCountFrom.Single().DisplayName.ShouldBe("Amigo");

        await ReportAsync(championshipId, tableId, friendPlayer, everything);

        // Assert — the table balances and can be settled.
        Reconciliation? ready = await GetReconciliationAsync(championshipId, tableId);
        ready!.EveryoneHasCounted.ShouldBeTrue();
        ready.ChipsBalance.ShouldBeTrue();
        ready.CanSettle.ShouldBeTrue();
        ready.Lines.ShouldAllBe(l => l.Difference == 0);

        HttpResponseMessage settled = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/settlement", new { });
        settled.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        SettlementDto? settlement = await HttpClient.GetFromJsonAsync<SettlementDto>(
            $"championships/{championshipId}/tables/{tableId}/settlement");

        // One payment: the owner owes the friend their whole buy-in.
        Transfer transfer = settlement!.Transfers.Single();
        transfer.FromDisplayName.ShouldBe("Dono");
        transfer.ToDisplayName.ShouldBe("Amigo");
        transfer.Amount.ShouldBe(50m);

        // The friend never set a payment handle, so there is nowhere to point to.
        transfer.ToPaymentHandle.ShouldBeNull();

        settlement.Results.Single(r => r.DisplayName == "Amigo").Position.ShouldBe(1);
        settlement.Results.Single(r => r.DisplayName == "Amigo").Points.ShouldBe(10);
        settlement.Results.Single(r => r.DisplayName == "Amigo").Balance.ShouldBe(50m);
        settlement.Results.Single(r => r.DisplayName == "Dono").Balance.ShouldBe(-50m);

        // The books close: every cent won came from someone.
        settlement.Results.Sum(r => r.Balance).ShouldBe(0m);

        owner.AccessToken.ShouldNotBeNullOrEmpty();
        friend.AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Settle_Should_Refuse_WhileAnyoneStillOwesACount()
    {
        (Guid championshipId, Guid tableId, AccessTokens _, AccessTokens _) = await StartedTableAsync();

        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/counting", new { });

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/settlement", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Settle_Should_Refuse_WhenTheChipsDoNotAddUp()
    {
        // Someone counts short — a chip on the floor. Settling here would make
        // somebody pay for chips nobody found.
        (Guid championshipId, Guid tableId, AccessTokens _, AccessTokens _) = await StartedTableAsync();

        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/counting", new { });

        TableDetail? table = await GetTableAsync(championshipId, tableId);
        Guid ownerPlayer = table!.Players[0].TablePlayerId;
        Guid friendPlayer = table.Players[1].TablePlayerId;

        var short1 = table.Stock
            .Where(s => s.Issued > 0)
            .ToDictionary(s => s.DenominationId, s => s.Issued);

        // Drop a single chip of the smallest denomination.
        Guid smallest = table.Stock.OrderBy(s => s.EffectiveValue).First(s => s.Issued > 0).DenominationId;
        short1[smallest] -= 1;

        await ReportAsync(championshipId, tableId, ownerPlayer,
            table.Stock.ToDictionary(s => s.DenominationId, _ => 0));
        await ReportAsync(championshipId, tableId, friendPlayer, short1);

        Reconciliation? reconciliation = await GetReconciliationAsync(championshipId, tableId);
        reconciliation!.EveryoneHasCounted.ShouldBeTrue();
        reconciliation.ChipsBalance.ShouldBeFalse();
        reconciliation.CanSettle.ShouldBeFalse();

        // The panel points at the exact chip, so the table knows what to look for.
        reconciliation.Lines.Single(l => l.DenominationId == smallest).Difference.ShouldBe(-1);

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/settlement", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Count_Should_BeReplaceableUntilItBalances()
    {
        // Correcting a miscount overwrites it rather than adding to it.
        (Guid championshipId, Guid tableId, AccessTokens _, AccessTokens _) = await StartedTableAsync();

        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/counting", new { });

        TableDetail? table = await GetTableAsync(championshipId, tableId);
        Guid friendPlayer = table!.Players[1].TablePlayerId;

        var wrong = table.Stock
            .Where(s => s.Issued > 0)
            .ToDictionary(s => s.DenominationId, s => s.Issued + 5);

        await ReportAsync(championshipId, tableId, friendPlayer, wrong);

        Reconciliation? afterWrong = await GetReconciliationAsync(championshipId, tableId);
        afterWrong!.ChipsBalance.ShouldBeFalse();

        var right = table.Stock
            .Where(s => s.Issued > 0)
            .ToDictionary(s => s.DenominationId, s => s.Issued);

        await ReportAsync(championshipId, tableId, friendPlayer, right);

        Reconciliation? afterFix = await GetReconciliationAsync(championshipId, tableId);
        afterFix!.Lines.Where(l => l.Issued > 0).ShouldAllBe(l => l.Counted == l.Issued);
    }

    [Fact]
    public async Task Count_Should_BeRefused_ForAnotherPlayersStack()
    {
        (Guid championshipId, Guid tableId, AccessTokens owner, AccessTokens friend) = await StartedTableAsync();

        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/counting", new { });

        TableDetail? table = await GetTableAsync(championshipId, tableId);
        Guid ownerPlayer = table!.Players.Single(p => p.DisplayName == "Dono").TablePlayerId;

        // The friend is a plain Player, so they may only report their own stack.
        Authenticate(friend.AccessToken);

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/counts",
            new
            {
                tablePlayerId = ownerPlayer,
                counts = new[] { new { denominationId = table.Stock[0].DenominationId, quantity = 1 } }
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        owner.AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Settle_Should_Refuse_ASecondTime()
    {
        (Guid championshipId, Guid tableId, AccessTokens _, AccessTokens _) = await StartedTableAsync();

        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/counting", new { });

        TableDetail? table = await GetTableAsync(championshipId, tableId);

        await ReportAsync(championshipId, tableId, table!.Players[0].TablePlayerId,
            table.Stock.ToDictionary(s => s.DenominationId, _ => 0));
        await ReportAsync(championshipId, tableId, table.Players[1].TablePlayerId,
            table.Stock.Where(s => s.Issued > 0).ToDictionary(s => s.DenominationId, s => s.Issued));

        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/settlement", new { });

        // People start sending money the moment it appears, so recomputing later
        // would contradict payments already made.
        HttpResponseMessage again = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/settlement", new { });

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
