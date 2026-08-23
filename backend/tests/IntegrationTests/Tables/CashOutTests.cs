using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Tables;

/// <summary>
/// Someone going home before the night ends. The chips go back into the case
/// and get dealt to somebody else, which is the whole difficulty: the same
/// physical chip must not read as issued twice, or the night can never
/// reconcile.
/// </summary>
public sealed class CashOutTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record Invite(string Code);

    private sealed record TableDetail(string Status, Player[] Players, Stock[] Stock);

    private sealed record Player(Guid TablePlayerId, Guid UserId, string DisplayName, string Status, decimal PaidIn);

    private sealed record Stock(Guid DenominationId, int FaceValue, int EffectiveValue, int Remaining, int Issued);

    private sealed record Reconciliation(
        Awaiting[] AwaitingCountFrom,
        bool EveryoneHasCounted,
        bool ChipsBalance,
        bool CanSettle);

    private sealed record Awaiting(Guid TablePlayerId, string DisplayName);

    private sealed record SettlementDto(Transfer[] Transfers, ResultDto[] Results);

    private sealed record Transfer(string FromDisplayName, string ToDisplayName, decimal Amount);

    private sealed record ResultDto(string DisplayName, int Position, int Points, decimal Balance);

    private static readonly int[] PointsByPosition = [10, 7];

    /// <summary>A two-player table, started, each holding a 1000-unit stack.</summary>
    private async Task<(Guid ChampionshipId, Guid TableId, AccessTokens Owner, AccessTokens Friend)> StartedTableAsync()
    {
        (Guid _, AccessTokens owner) = await RegisterAndLoginAsync();
        Authenticate(owner.AccessToken);
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
            new { displayName = "Amigo", paymentType = "Pix", paymentHandle = "amigo@pix" });
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

    /// <summary>Builds a count of exactly <paramref name="units"/> from the table's chips.</summary>
    private static object[] CountFor(TableDetail table, long units)
    {
        var counts = new List<object>();
        long left = units;

        foreach (Stock chip in table.Stock.OrderByDescending(s => s.EffectiveValue))
        {
            int quantity = (int)(left / chip.EffectiveValue);
            if (quantity > 0)
            {
                counts.Add(new { denominationId = chip.DenominationId, quantity });
                left -= (long)quantity * chip.EffectiveValue;
            }
        }

        return [.. counts];
    }

    private Task<HttpResponseMessage> CashOutAsync(
        Guid championshipId,
        Guid tableId,
        Guid tablePlayerId,
        object[] counts) =>
        HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/cash-outs",
            new { tablePlayerId, counts });

    [Fact]
    public async Task CashOut_Should_MarkThemLeftAndPutTheChipsBackInTheCase()
    {
        (Guid championshipId, Guid tableId, AccessTokens _, AccessTokens _) = await StartedTableAsync();

        TableDetail? before = await GetTableAsync(championshipId, tableId);
        long issuedBefore = before!.Stock.Sum(s => (long)s.Issued * s.EffectiveValue);
        issuedBefore.ShouldBe(2000);

        Player leaving = before.Players[0];

        HttpResponseMessage response = await CashOutAsync(
            championshipId, tableId, leaving.TablePlayerId, CountFor(before, 1200));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        TableDetail? after = await GetTableAsync(championshipId, tableId);
        after!.Players.Single(p => p.TablePlayerId == leaving.TablePlayerId).Status.ShouldBe("Left");

        // 2000 went out, 1200 came back: 800 is still on the table.
        long issuedAfter = after.Stock.Sum(s => (long)s.Issued * s.EffectiveValue);
        issuedAfter.ShouldBe(800);

        // And the case holds those chips again, ready to be dealt to someone else.
        after.Stock.ShouldAllBe(s => s.Remaining >= 0);
    }

    /// <summary>
    /// They took R$60 out for a R$50 buy-in, so they are R$10 up. Their final
    /// count is nothing, and without the cash-out crediting them that would read
    /// as having lost the lot.
    /// </summary>
    [Fact]
    public async Task CashOut_Should_CreditWhatTheyTookAgainstWhatTheyPaidIn()
    {
        (Guid championshipId, Guid tableId, AccessTokens _, AccessTokens _) = await StartedTableAsync();

        TableDetail? before = await GetTableAsync(championshipId, tableId);
        Player leaving = before!.Players[0];
        leaving.PaidIn.ShouldBe(50m);

        await CashOutAsync(championshipId, tableId, leaving.TablePlayerId, CountFor(before, 1200));

        TableDetail? after = await GetTableAsync(championshipId, tableId);

        // 1200 units at 0.05 is R$60 taken back off a R$50 stake.
        after!.Players.Single(p => p.TablePlayerId == leaving.TablePlayerId).PaidIn.ShouldBe(-10m);
    }

    /// <summary>
    /// The whole point of the exercise. One player leaves early, the other plays
    /// on and counts at the end, and the night still balances and settles — with
    /// the person who left in the settlement, so they know who owes them.
    /// </summary>
    [Fact]
    public async Task CashOut_Should_LeaveTheNightAbleToReconcileAndSettle()
    {
        (Guid championshipId, Guid tableId, AccessTokens _, AccessTokens _) = await StartedTableAsync();

        TableDetail? before = await GetTableAsync(championshipId, tableId);
        Player leaving = before!.Players.Single(p => p.DisplayName == "Amigo");
        Player staying = before.Players.Single(p => p.DisplayName == "Dono");

        // Amigo goes home 1200 units up; 800 units are left on the table.
        await CashOutAsync(championshipId, tableId, leaving.TablePlayerId, CountFor(before, 1200));

        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/counting", new { });

        // Dono is the only one left, so he is holding every chip still out —
        // built from what the table says is issued rather than from a tidy
        // number, because after a cash-out the chips left in play are whichever
        // ones the person leaving did not hand back.
        TableDetail? counting = await GetTableAsync(championshipId, tableId);
        object[] whatIsLeft =
        [
            .. counting!.Stock
                .Where(s => s.Issued > 0)
                .Select(s => new { denominationId = s.DenominationId, quantity = s.Issued })
        ];

        counting.Stock.Sum(s => (long)s.Issued * s.EffectiveValue).ShouldBe(800);

        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/counts",
            new { tablePlayerId = staying.TablePlayerId, counts = whatIsLeft });

        Reconciliation? reconciliation = await HttpClient.GetFromJsonAsync<Reconciliation>(
            $"championships/{championshipId}/tables/{tableId}/reconciliation");

        // Nobody is waiting on the person who already went home.
        reconciliation!.AwaitingCountFrom.ShouldBeEmpty();
        reconciliation.EveryoneHasCounted.ShouldBeTrue();
        reconciliation.ChipsBalance.ShouldBeTrue();
        reconciliation.CanSettle.ShouldBeTrue();

        HttpResponseMessage settled = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/settlement", new { });
        settled.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        SettlementDto? settlement = await HttpClient.GetFromJsonAsync<SettlementDto>(
            $"championships/{championshipId}/tables/{tableId}/settlement");

        // Amigo is R$10 up, Dono R$10 down, and the two sum to nothing.
        settlement!.Results.Single(r => r.DisplayName == "Amigo").Balance.ShouldBe(10m);
        settlement.Results.Single(r => r.DisplayName == "Dono").Balance.ShouldBe(-10m);
        settlement.Results.Sum(r => r.Balance).ShouldBe(0m);

        // And it says who pays whom, which is what the person who left came back for.
        Transfer transfer = settlement.Transfers.Single();
        transfer.FromDisplayName.ShouldBe("Dono");
        transfer.ToDisplayName.ShouldBe("Amigo");
        transfer.Amount.ShouldBe(10m);
    }

    /// <summary>
    /// Chips handed back must be dealable again — otherwise "they go back in the
    /// case" is only true on paper.
    /// </summary>
    [Fact]
    public async Task CashedOutChips_Should_BeAvailableToDealAgain()
    {
        (Guid championshipId, Guid tableId, AccessTokens _, AccessTokens _) = await StartedTableAsync();

        TableDetail? before = await GetTableAsync(championshipId, tableId);
        Player leaving = before!.Players[0];
        Player staying = before.Players[1];

        await CashOutAsync(championshipId, tableId, leaving.TablePlayerId, CountFor(before, 1000));

        TableDetail? afterCashOut = await GetTableAsync(championshipId, tableId);
        long issuedAfterCashOut = afterCashOut!.Stock.Sum(s => (long)s.Issued * s.EffectiveValue);

        HttpResponseMessage rebuy = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/stacks",
            new { tablePlayerId = staying.TablePlayerId, isRebuy = true });

        rebuy.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        TableDetail? afterRebuy = await GetTableAsync(championshipId, tableId);
        long issuedAfterRebuy = afterRebuy!.Stock.Sum(s => (long)s.Issued * s.EffectiveValue);

        (issuedAfterRebuy - issuedAfterCashOut).ShouldBe(1000);
    }

    [Fact]
    public async Task CashOut_Should_RefuseAPlainPlayerCashingOutSomeoneElse()
    {
        (Guid championshipId, Guid tableId, AccessTokens _, AccessTokens friend) = await StartedTableAsync();

        TableDetail? before = await GetTableAsync(championshipId, tableId);
        Player theOwner = before!.Players.Single(p => p.DisplayName == "Dono");

        Authenticate(friend.AccessToken);
        HttpResponseMessage response = await CashOutAsync(
            championshipId, tableId, theOwner.TablePlayerId, CountFor(before, 100));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>Going home is your own decision, the same way a rebuy is.</summary>
    [Fact]
    public async Task CashOut_Should_LetAPlayerCashThemselvesOut()
    {
        (Guid championshipId, Guid tableId, AccessTokens _, AccessTokens friend) = await StartedTableAsync();

        TableDetail? before = await GetTableAsync(championshipId, tableId);
        Player amigo = before!.Players.Single(p => p.DisplayName == "Amigo");

        Authenticate(friend.AccessToken);
        HttpResponseMessage response = await CashOutAsync(
            championshipId, tableId, amigo.TablePlayerId, CountFor(before, 1000));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Handing back more than the table ever had would drive the issued total
    /// negative and quietly break the reconciliation for everyone still playing.
    /// </summary>
    [Fact]
    public async Task CashOut_Should_RefuseMoreChipsThanAreInPlay()
    {
        (Guid championshipId, Guid tableId, AccessTokens _, AccessTokens _) = await StartedTableAsync();

        TableDetail? before = await GetTableAsync(championshipId, tableId);
        Player leaving = before!.Players[0];

        Stock hundred = before.Stock.Single(s => s.FaceValue == 100);
        object[] tooMany = [new { denominationId = hundred.DenominationId, quantity = hundred.Issued + 5 }];

        HttpResponseMessage response = await CashOutAsync(
            championshipId, tableId, leaving.TablePlayerId, tooMany);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CashOut_Should_RefuseSomeoneWhoAlreadyWentHome()
    {
        (Guid championshipId, Guid tableId, AccessTokens _, AccessTokens _) = await StartedTableAsync();

        TableDetail? before = await GetTableAsync(championshipId, tableId);
        Player leaving = before!.Players[0];

        await CashOutAsync(championshipId, tableId, leaving.TablePlayerId, CountFor(before, 500));

        HttpResponseMessage again = await CashOutAsync(
            championshipId, tableId, leaving.TablePlayerId, CountFor(before, 100));

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
