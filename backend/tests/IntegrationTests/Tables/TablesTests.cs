using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Tables;

public sealed class TablesTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record Invite(Guid Id, string Code);

    private sealed record TableDetail(
        Guid Id,
        string Name,
        string Status,
        decimal BuyIn,
        long BuyInUnits,
        string? JoinCode,
        Player[] Players,
        Stock[] Stock,
        decimal TotalPaidIn,
        bool CanManage,
        Guid? MyPlayerId);

    private sealed record Player(
        Guid TablePlayerId,
        Guid UserId,
        string DisplayName,
        string Status,
        decimal PaidIn,
        int RebuyCount);

    private sealed record Stock(Guid DenominationId, int FaceValue, int EffectiveValue, int Remaining, int Issued);

    /// <summary>A championship with a case big enough for a few stacks, owned by the caller.</summary>
    private async Task<(Guid ChampionshipId, Guid ChipSetId)> SetUpChampionshipAsync(
        int fives = 200,
        int twentyFives = 200,
        int fifties = 200,
        int hundreds = 200)
    {
        HttpResponseMessage created = await HttpClient.PostAsJsonAsync("championships", new
        {
            name = "Quinta-feira",
            defaultBuyIn = 50m,
            defaultRebuy = 50m,
            enforceDefaults = false,
            moneyPerUnit = 0.05m
        });
        created.EnsureSuccessStatusCode();
        Guid championshipId = await created.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage chipSet = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/chip-sets",
            new
            {
                name = "Maleta",
                denominations = new[]
                {
                    new { faceValue = 5, effectiveValue = 5, quantity = fives, colour = (string?)null },
                    new { faceValue = 25, effectiveValue = 25, quantity = twentyFives, colour = (string?)null },
                    new { faceValue = 50, effectiveValue = 50, quantity = fifties, colour = (string?)null },
                    new { faceValue = 100, effectiveValue = 100, quantity = hundreds, colour = (string?)null }
                }
            });
        chipSet.EnsureSuccessStatusCode();

        return (championshipId, await chipSet.Content.ReadFromJsonAsync<Guid>());
    }

    private async Task<Guid> CreateTableAsync(Guid championshipId, Guid chipSetId, string joinPolicy = "AnyMember")
    {
        HttpResponseMessage created = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables",
            new
            {
                name = "Mesa 1",
                chipSetId,
                buyIn = 50m,
                rebuy = 50m,
                joinPolicy,
                allowLateEntry = true,
                smallChipReserve = 0
            });

        created.EnsureSuccessStatusCode();

        return await created.Content.ReadFromJsonAsync<Guid>();
    }

    private Task<TableDetail?> GetTableAsync(Guid championshipId, Guid tableId) =>
        HttpClient.GetFromJsonAsync<TableDetail>($"championships/{championshipId}/tables/{tableId}");

    /// <summary>Registers a second person and gets them into the championship.</summary>
    private async Task<AccessTokens> AddSecondPlayerAsync(Guid championshipId, AccessTokens ownerTokens)
    {
        HttpResponseMessage invite = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/invites",
            new { role = "Player", expiresAtUtc = (DateTime?)null, maxUses = (int?)null });
        Invite? code = await invite.Content.ReadFromJsonAsync<Invite>();

        (Guid _, AccessTokens playerTokens) = await RegisterAndLoginAsync();
        Authenticate(playerTokens.AccessToken);
        await HttpClient.PostAsJsonAsync("championships/join", new { code = code!.Code });

        Authenticate(ownerTokens.AccessToken);

        return playerTokens;
    }

    [Fact]
    public async Task Start_Should_DealAStackToEveryPlayerAndDeductFromTheCase()
    {
        // Arrange
        (Guid _, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        (Guid championshipId, Guid chipSetId) = await SetUpChampionshipAsync();
        Guid tableId = await CreateTableAsync(championshipId, chipSetId);

        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });

        AccessTokens playerTokens = await AddSecondPlayerAsync(championshipId, tokens);
        Authenticate(playerTokens.AccessToken);
        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage started = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/start", new { });

        // Assert
        started.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        TableDetail? table = await GetTableAsync(championshipId, tableId);
        table!.Status.ShouldBe("Running");
        table.Players.Length.ShouldBe(2);
        table.Players.ShouldAllBe(p => p.Status == "Playing");
        table.Players.ShouldAllBe(p => p.PaidIn == 50m);
        table.TotalPaidIn.ShouldBe(100m);

        // R$50 at 0.05 per unit is a 1000-unit stack, twice over.
        table.BuyInUnits.ShouldBe(1000);
        long issuedUnits = table.Stock.Sum(s => (long)s.Issued * s.EffectiveValue);
        issuedUnits.ShouldBe(2000);
    }

    [Fact]
    public async Task Start_Should_Refuse_WhenNobodyIsAtTheTable()
    {
        // Arrange
        (Guid _, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        (Guid championshipId, Guid chipSetId) = await SetUpChampionshipAsync();
        Guid tableId = await CreateTableAsync(championshipId, chipSetId);

        // Act
        HttpResponseMessage started = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/start", new { });

        // Assert
        started.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Start_Should_Refuse_WhenTheCaseCannotCoverEveryStack()
    {
        // Arrange — a case worth 300 units against a 1000-unit stack.
        (Guid _, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        (Guid championshipId, Guid chipSetId) = await SetUpChampionshipAsync(fives: 20, twentyFives: 4, fifties: 2, hundreds: 1);
        Guid tableId = await CreateTableAsync(championshipId, chipSetId);

        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });

        // Act
        HttpResponseMessage started = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/start", new { });

        // Assert — refused whole, with the gap named, rather than dealing short.
        started.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        string body = await started.Content.ReadAsStringAsync();
        body.ShouldContain("units short");

        TableDetail? table = await GetTableAsync(championshipId, tableId);
        table!.Status.ShouldBe("Open");
        table.Stock.ShouldAllBe(s => s.Issued == 0);
    }

    [Fact]
    public async Task Rebuy_Should_TakeMoreChipsAndRaiseWhatThePlayerIsDown()
    {
        // Arrange
        (Guid _, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        (Guid championshipId, Guid chipSetId) = await SetUpChampionshipAsync();
        Guid tableId = await CreateTableAsync(championshipId, chipSetId);

        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });
        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/start", new { });

        TableDetail? before = await GetTableAsync(championshipId, tableId);
        Guid playerId = before!.Players.Single().TablePlayerId;

        // Act
        HttpResponseMessage rebuy = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/stacks",
            new { tablePlayerId = playerId, isRebuy = true });

        // Assert
        rebuy.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        TableDetail? after = await GetTableAsync(championshipId, tableId);
        Player player = after!.Players.Single();
        player.PaidIn.ShouldBe(100m);
        player.RebuyCount.ShouldBe(1);

        after.Stock.Sum(s => (long)s.Issued * s.EffectiveValue).ShouldBe(2000);
    }

    [Fact]
    public async Task ChipTrade_Should_MoveMoneyWithoutTouchingTheCase()
    {
        // The heart of the "case ran out" workaround: the buyer pays, the seller is
        // credited the same, no chips leave the case, and the totals still balance.
        (Guid _, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        (Guid championshipId, Guid chipSetId) = await SetUpChampionshipAsync();
        Guid tableId = await CreateTableAsync(championshipId, chipSetId);

        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });

        AccessTokens playerTokens = await AddSecondPlayerAsync(championshipId, tokens);
        Authenticate(playerTokens.AccessToken);
        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });
        Authenticate(tokens.AccessToken);

        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/start", new { });

        TableDetail? before = await GetTableAsync(championshipId, tableId);
        long issuedBefore = before!.Stock.Sum(s => (long)s.Issued * s.EffectiveValue);
        Guid buyer = before.Players[0].TablePlayerId;
        Guid seller = before.Players[1].TablePlayerId;

        // Act
        HttpResponseMessage trade = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/chip-trades",
            new { buyerPlayerId = buyer, sellerPlayerId = seller, amount = 50m });

        // Assert
        trade.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        TableDetail? after = await GetTableAsync(championshipId, tableId);

        after!.Stock.Sum(s => (long)s.Issued * s.EffectiveValue).ShouldBe(issuedBefore);

        after.Players.Single(p => p.TablePlayerId == buyer).PaidIn.ShouldBe(100m);
        // The seller is credited, so bailing the table out costs them nothing.
        after.Players.Single(p => p.TablePlayerId == seller).PaidIn.ShouldBe(0m);

        // Money on the table is unchanged: it only moved between two people.
        after.TotalPaidIn.ShouldBe(before.TotalPaidIn);
    }

    [Fact]
    public async Task ChipTrade_Should_RefuseAPlayerTradingWithThemselves()
    {
        (Guid _, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        (Guid championshipId, Guid chipSetId) = await SetUpChampionshipAsync();
        Guid tableId = await CreateTableAsync(championshipId, chipSetId);

        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });
        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/start", new { });

        TableDetail? table = await GetTableAsync(championshipId, tableId);
        Guid playerId = table!.Players.Single().TablePlayerId;

        HttpResponseMessage trade = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/chip-trades",
            new { buyerPlayerId = playerId, sellerPlayerId = playerId, amount = 50m });

        trade.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Join_Should_NeedTheCode_OnACodedTable()
    {
        // Arrange
        (Guid _, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        (Guid championshipId, Guid chipSetId) = await SetUpChampionshipAsync();
        Guid tableId = await CreateTableAsync(championshipId, chipSetId, joinPolicy: "Code");

        TableDetail? asManager = await GetTableAsync(championshipId, tableId);
        string code = asManager!.JoinCode.ShouldNotBeNull();

        AccessTokens playerTokens = await AddSecondPlayerAsync(championshipId, ownerTokens);
        Authenticate(playerTokens.AccessToken);

        // A plain player must not be able to read the code back off the table.
        TableDetail? asPlayer = await GetTableAsync(championshipId, tableId);
        asPlayer!.JoinCode.ShouldBeNull();
        asPlayer.CanManage.ShouldBeFalse();

        // Act + Assert
        HttpResponseMessage wrong = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = "ZZZZZZ" });
        wrong.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // CA1308 guards against lowercasing to normalise; here lowercase is the
        // input under test, since someone reading a code aloud gets typed back in
        // whatever case the phone felt like.
#pragma warning disable CA1308
        string asTyped = code.ToLowerInvariant();
#pragma warning restore CA1308

        HttpResponseMessage right = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join",
            new { code = asTyped });
        right.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Tables_Should_BeInvisibleToNonMembers()
    {
        (Guid _, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        (Guid championshipId, Guid chipSetId) = await SetUpChampionshipAsync();
        Guid tableId = await CreateTableAsync(championshipId, chipSetId);

        (Guid _, AccessTokens strangerTokens) = await RegisterAndLoginAsync();
        Authenticate(strangerTokens.AccessToken);

        HttpResponseMessage response = await HttpClient.GetAsync(
            $"championships/{championshipId}/tables/{tableId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Should_BeForbiddenForAPlainPlayer()
    {
        (Guid _, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        (Guid championshipId, Guid chipSetId) = await SetUpChampionshipAsync();

        AccessTokens playerTokens = await AddSecondPlayerAsync(championshipId, ownerTokens);
        Authenticate(playerTokens.AccessToken);

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables",
            new
            {
                name = "Minha mesa",
                chipSetId,
                buyIn = 50m,
                rebuy = 50m,
                joinPolicy = "AnyMember",
                allowLateEntry = true,
                smallChipReserve = 0
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
