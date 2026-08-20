using System.Net.Http.Json;

namespace IntegrationTests.Championships;

/// <summary>
/// The four read-back screens, checked against a night that was actually played
/// rather than against rows written straight into the database — the point of
/// these endpoints is that they agree with what happened at the table.
/// </summary>
public sealed class RankingsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record InviteDto(string Code);

    private sealed record TableDetail(Player[] Players, Stock[] Stock);

    private sealed record Player(Guid TablePlayerId, string DisplayName);

    private sealed record Stock(Guid DenominationId, int EffectiveValue, int Issued);

    private sealed record RankingRow(
        string DisplayName,
        int Position,
        int Points,
        decimal Balance,
        int TablesPlayed,
        int Wins,
        int BestPosition);

    private sealed record RankingsDto(RankingRow[] ByPoints, RankingRow[] ByBalance, int TablesCounted);

    private sealed record HistoryRow(
        string Name,
        int PlayerCount,
        string? WinnerDisplayName,
        decimal WinnerBalance,
        decimal MoneyIn,
        string? ChipLeaderDisplayName,
        decimal ChipLeaderChips);

    private sealed record StatementRow(
        string TableName,
        int Position,
        int Points,
        decimal Balance,
        decimal PaidIn,
        int Rebuys);

    private sealed record StatementDto(
        StatementRow[] Rows,
        decimal TotalBalance,
        decimal TotalPaidIn,
        int TotalPoints,
        int Wins);

    private sealed record NightRecord(string DisplayName, string TableName, decimal Balance);

    private sealed record StatisticsDto(
        int TablesPlayed,
        int DistinctPlayers,
        decimal MoneyIn,
        int Rebuys,
        decimal AverageMoneyPerTable,
        NightRecord? BiggestWin,
        NightRecord? BiggestLoss);

    private sealed record ChampionshipDto(string Name, string? LeaderDisplayName, int LeaderPoints);

    private static readonly int[] PointsByPosition = [10, 7];

    /// <summary>
    /// Plays one whole night to a settlement: two players, one stack each, and
    /// the friend takes the lot.
    /// </summary>
    private async Task<(Guid ChampionshipId, AccessTokens Owner, AccessTokens Friend)> PlayANightAsync(
        string tableName)
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
                    new { faceValue = 25, effectiveValue = 25, quantity = 200, colour = (string?)null },
                    new { faceValue = 50, effectiveValue = 50, quantity = 200, colour = (string?)null },
                    new { faceValue = 100, effectiveValue = 100, quantity = 200, colour = (string?)null }
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
                name = tableName,
                chipSetId,
                buyIn = 50m,
                rebuy = 50m,
                joinPolicy = "AnyMember",
                allowLateEntry = true,
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
        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/counting", new { });

        TableDetail? detail = await HttpClient.GetFromJsonAsync<TableDetail>(
            $"championships/{championshipId}/tables/{tableId}");

        Guid ownerPlayer = detail!.Players.Single(p => p.DisplayName == "Dono").TablePlayerId;
        Guid friendPlayer = detail.Players.Single(p => p.DisplayName == "Amigo").TablePlayerId;

        await ReportAsync(championshipId, tableId, ownerPlayer,
            detail.Stock.ToDictionary(s => s.DenominationId, _ => 0));

        await ReportAsync(championshipId, tableId, friendPlayer,
            detail.Stock.Where(s => s.Issued > 0).ToDictionary(s => s.DenominationId, s => s.Issued));

        HttpResponseMessage settled = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/settlement", new { });
        settled.EnsureSuccessStatusCode();

        return (championshipId, owner, friend);
    }

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
    public async Task Rankings_Should_RankByPointsAndByBalance()
    {
        (Guid championshipId, AccessTokens owner, AccessTokens _) = await PlayANightAsync("Mesa");
        Authenticate(owner.AccessToken);

        RankingsDto? rankings = await HttpClient.GetFromJsonAsync<RankingsDto>(
            $"championships/{championshipId}/rankings");

        rankings!.TablesCounted.ShouldBe(1);

        RankingRow first = rankings.ByPoints[0];
        first.DisplayName.ShouldBe("Amigo");
        first.Position.ShouldBe(1);
        first.Points.ShouldBe(10);
        first.Balance.ShouldBe(50m);
        first.Wins.ShouldBe(1);
        first.BestPosition.ShouldBe(1);
        first.TablesPlayed.ShouldBe(1);

        RankingRow second = rankings.ByPoints[1];
        second.DisplayName.ShouldBe("Dono");
        second.Position.ShouldBe(2);
        second.Points.ShouldBe(7);
        second.Balance.ShouldBe(-50m);
        second.Wins.ShouldBe(0);

        // The same two people, ranked again by money. Here they happen to agree;
        // the point is that each list carries its own positions.
        rankings.ByBalance[0].DisplayName.ShouldBe("Amigo");
        rankings.ByBalance[0].Position.ShouldBe(1);
        rankings.ByBalance[1].DisplayName.ShouldBe("Dono");
        rankings.ByBalance[1].Position.ShouldBe(2);

        // Nobody can win money out of nowhere: a table is zero sum.
        rankings.ByBalance.Sum(row => row.Balance).ShouldBe(0m);
    }

    [Fact]
    public async Task History_Should_NameTheWinnerAndTheMoneyPlayed()
    {
        (Guid championshipId, AccessTokens owner, AccessTokens _) = await PlayANightAsync("Sexta");
        Authenticate(owner.AccessToken);

        HistoryRow[]? history = await HttpClient.GetFromJsonAsync<HistoryRow[]>(
            $"championships/{championshipId}/history");

        HistoryRow row = history!.Single();
        row.Name.ShouldBe("Sexta");
        row.PlayerCount.ShouldBe(2);
        row.WinnerDisplayName.ShouldBe("Amigo");
        row.WinnerBalance.ShouldBe(50m);
        row.MoneyIn.ShouldBe(100m);

        // Nobody rebought, so the biggest stack and the best night are the same
        // person, and the chips they hold are the whole table's money.
        row.ChipLeaderDisplayName.ShouldBe("Amigo");
        row.ChipLeaderChips.ShouldBe(100m);
    }

    /// <summary>
    /// The case the card exists for: someone who rebought can finish with the
    /// biggest pile in front of them and still be down on the night, because
    /// balance takes off what they paid to get those chips.
    /// </summary>
    [Fact]
    public async Task History_Should_NameAChipLeaderWhoIsNotTheWinner()
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
                    new { faceValue = 5, effectiveValue = 5, quantity = 400, colour = (string?)null },
                    new { faceValue = 25, effectiveValue = 25, quantity = 200, colour = (string?)null },
                    new { faceValue = 50, effectiveValue = 50, quantity = 200, colour = (string?)null },
                    new { faceValue = 100, effectiveValue = 100, quantity = 200, colour = (string?)null }
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
                allowLateEntry = true,
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

        TableDetail? started = await HttpClient.GetFromJsonAsync<TableDetail>(
            $"championships/{championshipId}/tables/{tableId}");
        Guid ownerPlayer = started!.Players.Single(p => p.DisplayName == "Dono").TablePlayerId;
        Guid friendPlayer = started.Players.Single(p => p.DisplayName == "Amigo").TablePlayerId;

        // The owner rebuys, so they have paid 100 against the friend's 50.
        HttpResponseMessage rebuy = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/stacks",
            new { tablePlayerId = ownerPlayer, isRebuy = true });
        rebuy.EnsureSuccessStatusCode();

        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/counting", new { });

        TableDetail? counting = await HttpClient.GetFromJsonAsync<TableDetail>(
            $"championships/{championshipId}/tables/{tableId}");

        // 3000 units are in play. Split 1600 to the owner and 1400 to the friend:
        // the owner holds more chips but has paid 100 for them, so they finish
        // 20 down while the friend finishes 20 up.
        Dictionary<Guid, int> ownerCounts = Take(counting!.Stock, 1600);
        Dictionary<Guid, int> friendCounts = counting.Stock.ToDictionary(
            stock => stock.DenominationId,
            stock => stock.Issued - ownerCounts.GetValueOrDefault(stock.DenominationId));

        await ReportAsync(championshipId, tableId, ownerPlayer, ownerCounts);
        await ReportAsync(championshipId, tableId, friendPlayer, friendCounts);

        HttpResponseMessage settled = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/settlement", new { });
        settled.EnsureSuccessStatusCode();

        HistoryRow[]? history = await HttpClient.GetFromJsonAsync<HistoryRow[]>(
            $"championships/{championshipId}/history");

        HistoryRow row = history!.Single();
        row.MoneyIn.ShouldBe(150m);

        // The two differ, which is the whole point of showing both.
        row.ChipLeaderDisplayName.ShouldBe("Dono");
        row.ChipLeaderChips.ShouldBe(80m);
        row.WinnerDisplayName.ShouldBe("Amigo");
        row.WinnerBalance.ShouldBe(20m);
    }

    /// <summary>
    /// Takes exactly <paramref name="units"/> worth of the chips issued, biggest
    /// first. The 5s at the end make any multiple of 5 reachable.
    /// </summary>
    private static Dictionary<Guid, int> Take(Stock[] stock, int units)
    {
        Dictionary<Guid, int> taken = [];
        int left = units;

        foreach (Stock chip in stock.OrderByDescending(s => s.EffectiveValue))
        {
            int wanted = Math.Min(left / chip.EffectiveValue, chip.Issued);

            taken[chip.DenominationId] = wanted;
            left -= wanted * chip.EffectiveValue;
        }

        left.ShouldBe(0, "the chips issued cannot make this split exactly");

        return taken;
    }

    [Fact]
    public async Task Statement_Should_ShowOnlyTheCallersOwnNights()
    {
        (Guid championshipId, AccessTokens owner, AccessTokens friend) = await PlayANightAsync("Mesa");

        Authenticate(owner.AccessToken);
        StatementDto? mine = await HttpClient.GetFromJsonAsync<StatementDto>(
            $"championships/{championshipId}/statement");

        StatementRow row = mine!.Rows.Single();
        row.TableName.ShouldBe("Mesa");
        row.Position.ShouldBe(2);
        row.Points.ShouldBe(7);
        row.Balance.ShouldBe(-50m);
        row.PaidIn.ShouldBe(50m);
        row.Rebuys.ShouldBe(0);

        mine.TotalBalance.ShouldBe(-50m);
        mine.TotalPaidIn.ShouldBe(50m);
        mine.TotalPoints.ShouldBe(7);
        mine.Wins.ShouldBe(0);

        // The same endpoint, the other player: their own night, not the owner's.
        Authenticate(friend.AccessToken);
        StatementDto? theirs = await HttpClient.GetFromJsonAsync<StatementDto>(
            $"championships/{championshipId}/statement");

        theirs!.Rows.Single().Balance.ShouldBe(50m);
        theirs.Wins.ShouldBe(1);
    }

    [Fact]
    public async Task Statistics_Should_SummariseTheChampionship()
    {
        (Guid championshipId, AccessTokens owner, AccessTokens _) = await PlayANightAsync("Mesa");
        Authenticate(owner.AccessToken);

        StatisticsDto? statistics = await HttpClient.GetFromJsonAsync<StatisticsDto>(
            $"championships/{championshipId}/statistics");

        statistics!.TablesPlayed.ShouldBe(1);
        statistics.DistinctPlayers.ShouldBe(2);
        statistics.MoneyIn.ShouldBe(100m);
        statistics.Rebuys.ShouldBe(0);
        statistics.AverageMoneyPerTable.ShouldBe(100m);

        statistics.BiggestWin!.DisplayName.ShouldBe("Amigo");
        statistics.BiggestWin.Balance.ShouldBe(50m);
        statistics.BiggestLoss!.DisplayName.ShouldBe("Dono");
        statistics.BiggestLoss.Balance.ShouldBe(-50m);
    }

    [Fact]
    public async Task ChampionshipCard_Should_NameTheLeaderOnPoints()
    {
        (Guid championshipId, AccessTokens owner, AccessTokens _) = await PlayANightAsync("Mesa");
        Authenticate(owner.AccessToken);

        ChampionshipDto? detail = await HttpClient.GetFromJsonAsync<ChampionshipDto>(
            $"championships/{championshipId}");

        detail!.LeaderDisplayName.ShouldBe("Amigo");
        detail.LeaderPoints.ShouldBe(10);

        // The same leader on the list card, so the two screens cannot disagree.
        ChampionshipDto[]? mine = await HttpClient.GetFromJsonAsync<ChampionshipDto[]>("championships");

        ChampionshipDto summary = mine!.Single(c => c.Name == "Quinta");
        summary.LeaderDisplayName.ShouldBe("Amigo");
        summary.LeaderPoints.ShouldBe(10);
    }

    [Fact]
    public async Task Rankings_Should_BeEmptyBeforeAnythingIsSettled()
    {
        (Guid _, AccessTokens owner) = await RegisterAndLoginAsync();
        Authenticate(owner.AccessToken);

        HttpResponseMessage created = await HttpClient.PostAsJsonAsync("championships", new
        {
            name = "Nova",
            defaultBuyIn = 50m,
            defaultRebuy = 50m,
            enforceDefaults = false,
            moneyPerUnit = 0.05m,
            pointsByPosition = PointsByPosition
        });
        Guid championshipId = await created.Content.ReadFromJsonAsync<Guid>();

        RankingsDto? rankings = await HttpClient.GetFromJsonAsync<RankingsDto>(
            $"championships/{championshipId}/rankings");

        rankings!.ByPoints.ShouldBeEmpty();
        rankings.ByBalance.ShouldBeEmpty();
        rankings.TablesCounted.ShouldBe(0);

        // No division by zero, and no invented record-holder.
        StatisticsDto? statistics = await HttpClient.GetFromJsonAsync<StatisticsDto>(
            $"championships/{championshipId}/statistics");

        statistics!.AverageMoneyPerTable.ShouldBe(0m);
        statistics.BiggestWin.ShouldBeNull();
        statistics.BiggestLoss.ShouldBeNull();

        // And no leader invented before a single hand has been played.
        ChampionshipDto? card = await HttpClient.GetFromJsonAsync<ChampionshipDto>(
            $"championships/{championshipId}");

        card!.LeaderDisplayName.ShouldBeNull();
        card.LeaderPoints.ShouldBe(0);
    }
}
