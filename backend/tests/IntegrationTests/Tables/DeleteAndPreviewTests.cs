using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Tables;

public sealed class DeleteAndPreviewTests(IntegrationTestWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    private sealed record Preview(
        PreviewChip[] Chips,
        decimal Money,
        long Units,
        long ShortfallUnits,
        bool IsPossible);

    private sealed record PreviewChip(
        Guid DenominationId,
        int FaceValue,
        int EffectiveValue,
        string? Colour,
        int Quantity);

    private sealed record Summary(Guid Id, string Name);

    private async Task<(Guid ChampionshipId, Guid TableId)> SetUpAsync(int fives = 200)
    {
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
                    new { faceValue = 5, effectiveValue = 5, quantity = fives, colour = "red" },
                    new { faceValue = 100, effectiveValue = 100, quantity = 200, colour = "black" }
                }
            });
        Guid chipSetId = await chipSet.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage table = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables",
            new
            {
                name = "Mesa 1",
                chipSetId,
                buyIn = 50m,
                rebuy = 50m,
                joinPolicy = "AnyMember",
                allowLateEntry = true,
                smallChipReserve = 0
            });

        return (championshipId, await table.Content.ReadFromJsonAsync<Guid>());
    }

    [Fact]
    public async Task Preview_Should_SayExactlyWhichChipsToCountOut()
    {
        // Arrange
        (Guid _, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        (Guid championshipId, Guid tableId) = await SetUpAsync();

        // Act
        Preview? preview = await HttpClient.GetFromJsonAsync<Preview>(
            $"championships/{championshipId}/tables/{tableId}/stack-preview?isRebuy=false");

        // Assert
        preview!.IsPossible.ShouldBeTrue();
        preview.Units.ShouldBe(1000);
        preview.Money.ShouldBe(50m);

        // The chips actually add up to the stack being promised.
        preview.Chips.Sum(c => (long)c.Quantity * c.EffectiveValue).ShouldBe(1000);

        // Biggest first, which is the order someone counts a stack out of a case.
        preview.Chips.Select(c => c.EffectiveValue).ShouldBe([100, 5]);

        // The colour comes along, because that is what people look for.
        preview.Chips.Single(c => c.FaceValue == 5).Colour.ShouldBe("red");
    }

    /// <summary>
    /// A case worth 1,500 units against a 1,000-unit stack, which it still cannot
    /// make: every chip in it is worth 3, and no number of 3s is 1,000.
    ///
    /// This is exactly what the preview is for. "Plenty of chips" and "can deal
    /// this stack" are different questions, and only the second one matters when
    /// someone is about to tap the button.
    /// </summary>
    [Fact]
    public async Task Preview_Should_SayWhenTheStackCannotBeMadeExactly_BeforeAnythingIsTapped()
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
                name = "Maleta de 3",
                denominations = new[]
                {
                    new { faceValue = 3, effectiveValue = 3, quantity = 500, colour = "red" }
                }
            });
        Guid chipSetId = await chipSet.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage table = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables",
            new
            {
                name = "Mesa 1",
                chipSetId,
                buyIn = 50m,
                rebuy = 50m,
                joinPolicy = "AnyMember",
                allowLateEntry = true,
                smallChipReserve = 0
            });
        Guid tableId = await table.Content.ReadFromJsonAsync<Guid>();

        Preview? preview = await HttpClient.GetFromJsonAsync<Preview>(
            $"championships/{championshipId}/tables/{tableId}/stack-preview?isRebuy=false");

        preview!.Units.ShouldBe(1000);
        preview.IsPossible.ShouldBeFalse();
        preview.ShortfallUnits.ShouldBeGreaterThan(0);

        // What it can allocate is still coherent, so the screen can show how
        // close the case got rather than only how short it fell.
        preview.Chips.Sum(c => (long)c.Quantity * c.EffectiveValue)
            .ShouldBe(preview.Units - preview.ShortfallUnits);
    }

    /// <summary>
    /// The counterpart, and the reason the one above had to be rebuilt: with two
    /// 5s and two hundred 100s the profile's own first guess falls 90 units
    /// short, but ten 100s make the stack exactly. "The profile could not place
    /// it" is not the same as "the case cannot make it", and only the second is
    /// worth refusing a buy-in over.
    /// </summary>
    [Fact]
    public async Task Preview_Should_FindAStackTheProfilesFirstGuessMisses()
    {
        (Guid _, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        (Guid championshipId, Guid tableId) = await SetUpAsync(fives: 2);

        Preview? preview = await HttpClient.GetFromJsonAsync<Preview>(
            $"championships/{championshipId}/tables/{tableId}/stack-preview?isRebuy=false");

        preview!.IsPossible.ShouldBeTrue();
        preview.ShortfallUnits.ShouldBe(0);
        preview.Chips.Sum(c => (long)c.Quantity * c.EffectiveValue).ShouldBe(1000);

        // Ten 100s and nothing else, as it happens: with only two 5s in the case
        // there is no other exact combination, so the search leaves them alone
        // rather than forcing them in and missing the target.
        (preview.Chips.SingleOrDefault(c => c.FaceValue == 5)?.Quantity ?? 0)
            .ShouldBeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task Preview_Should_MatchWhatTheRebuyActuallyHandsOver()
    {
        // The preview promising a mix the real deal would not produce is the one
        // way this feature could actively mislead someone counting chips.
        (Guid _, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        (Guid championshipId, Guid tableId) = await SetUpAsync();

        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });
        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/start", new { });

        Preview? preview = await HttpClient.GetFromJsonAsync<Preview>(
            $"championships/{championshipId}/tables/{tableId}/stack-preview?isRebuy=true");

        TableSnapshot? before = await HttpClient.GetFromJsonAsync<TableSnapshot>(
            $"championships/{championshipId}/tables/{tableId}");

        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/stacks",
            new { tablePlayerId = before!.Players[0].TablePlayerId, isRebuy = true });

        TableSnapshot? after = await HttpClient.GetFromJsonAsync<TableSnapshot>(
            $"championships/{championshipId}/tables/{tableId}");

        foreach (PreviewChip chip in preview!.Chips)
        {
            int issuedBefore = before.Stock.Single(s => s.DenominationId == chip.DenominationId).Issued;
            int issuedAfter = after!.Stock.Single(s => s.DenominationId == chip.DenominationId).Issued;

            (issuedAfter - issuedBefore).ShouldBe(chip.Quantity);
        }
    }

    [Fact]
    public async Task DeleteTable_Should_NeedTheNameTypedExactly()
    {
        (Guid _, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        (Guid championshipId, Guid tableId) = await SetUpAsync();

        HttpResponseMessage wrong = await HttpClient.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            new Uri($"championships/{championshipId}/tables/{tableId}", UriKind.Relative))
        {
            Content = JsonContent.Create(new { confirmName = "Mesa" })
        });

        wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Still there.
        HttpResponseMessage stillThere = await HttpClient.GetAsync(
            $"championships/{championshipId}/tables/{tableId}");
        stillThere.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteTable_Should_TakeThePlayedNightWithIt()
    {
        (Guid _, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        (Guid championshipId, Guid tableId) = await SetUpAsync();

        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });
        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/start", new { });

        HttpResponseMessage deleted = await HttpClient.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            new Uri($"championships/{championshipId}/tables/{tableId}", UriKind.Relative))
        {
            Content = JsonContent.Create(new { confirmName = "Mesa 1" })
        });

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage gone = await HttpClient.GetAsync(
            $"championships/{championshipId}/tables/{tableId}");
        gone.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteChampionship_Should_BeOwnerOnly()
    {
        (Guid _, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        (Guid championshipId, Guid _) = await SetUpAsync();

        HttpResponseMessage inviteResponse = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/invites",
            new { role = "Admin", expiresAtUtc = (DateTime?)null, maxUses = (int?)null });
        InviteDto? invite = await inviteResponse.Content.ReadFromJsonAsync<InviteDto>();

        (Guid _, AccessTokens adminTokens) = await RegisterAndLoginAsync();
        Authenticate(adminTokens.AccessToken);
        await HttpClient.PostAsJsonAsync("championships/join", new { code = invite!.Code });

        // An admin can do almost everything, but not erase the championship.
        HttpResponseMessage refused = await HttpClient.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            new Uri($"championships/{championshipId}", UriKind.Relative))
        {
            Content = JsonContent.Create(new { confirmName = "Quinta" })
        });

        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteChampionship_Should_TakeEverythingWithIt()
    {
        (Guid _, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        (Guid championshipId, Guid tableId) = await SetUpAsync();

        await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/tables/{tableId}/join", new { code = (string?)null });
        await HttpClient.PostAsJsonAsync($"championships/{championshipId}/tables/{tableId}/start", new { });

        HttpResponseMessage deleted = await HttpClient.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            new Uri($"championships/{championshipId}", UriKind.Relative))
        {
            Content = JsonContent.Create(new { confirmName = "Quinta" })
        });

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        Summary[]? mine = await HttpClient.GetFromJsonAsync<Summary[]>("championships");
        mine!.ShouldNotContain(c => c.Id == championshipId);
    }

    private sealed record InviteDto(string Code);

    private sealed record TableSnapshot(PlayerDto[] Players, StockDto[] Stock);

    private sealed record PlayerDto(Guid TablePlayerId);

    private sealed record StockDto(Guid DenominationId, int Issued);
}
