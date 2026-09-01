using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Championships;

public sealed class ChampionshipsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record Summary(Guid Id, string Name, string Role, int MemberCount);

    private sealed record Detail(
        Guid Id,
        string Name,
        Guid OwnerId,
        decimal MoneyPerUnit,
        int[] PointsByPosition,
        string Role);

    private sealed record Member(Guid UserId, string DisplayName, string Role, bool HasPaymentHandle);

    private sealed record Invite(Guid Id, string Code, string Role, int? MaxUses, int Uses, bool IsRevoked);

    private sealed record JoinResult(Guid ChampionshipId, string Name);

    private async Task<Guid> CreateChampionshipAsync(string name = "Quinta-feira")
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("championships", new
        {
            name,
            description = "Jogo semanal",
            defaultBuyIn = 50m,
            defaultRebuy = 50m,
            enforceDefaults = false,
            moneyPerUnit = 0.05m
        });

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    [Fact]
    public async Task Create_Should_MakeTheCreatorTheOwnerAndAMember()
    {
        // Arrange
        (Guid _, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        Guid championshipId = await CreateChampionshipAsync();

        // Assert
        Detail? detail = await HttpClient.GetFromJsonAsync<Detail>($"championships/{championshipId}");
        detail!.Role.ShouldBe("Owner");

        // Without a membership row the owner would be locked out of everything,
        // since every check resolves through membership.
        Member[]? members = await HttpClient.GetFromJsonAsync<Member[]>($"championships/{championshipId}/members");
        members!.Length.ShouldBe(1);
        members[0].Role.ShouldBe("Owner");
    }

    [Fact]
    public async Task Create_Should_ApplyTheDefaultPointsTable_WhenNoneIsGiven()
    {
        // Arrange
        (Guid _, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        Guid championshipId = await CreateChampionshipAsync();

        // Assert
        Detail? detail = await HttpClient.GetFromJsonAsync<Detail>($"championships/{championshipId}");
        detail!.PointsByPosition.ShouldBe([10, 7, 5, 3, 2, 1]);
    }

    [Fact]
    public async Task GetMine_Should_ListOnlyChampionshipsTheCallerBelongsTo()
    {
        // Arrange
        (Guid _, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        await CreateChampionshipAsync("Mine");

        (Guid _, AccessTokens strangerTokens) = await RegisterAndLoginAsync();
        Authenticate(strangerTokens.AccessToken);

        // Act
        Summary[]? mine = await HttpClient.GetFromJsonAsync<Summary[]>("championships");

        // Assert
        mine!.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetById_Should_AnswerNotFound_ForANonMember()
    {
        // Arrange
        (Guid _, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid championshipId = await CreateChampionshipAsync();

        (Guid _, AccessTokens strangerTokens) = await RegisterAndLoginAsync();
        Authenticate(strangerTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"championships/{championshipId}");

        // Assert — a non-member must not be able to tell an id they can't see
        // from one that doesn't exist.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invite_Should_LetSomeoneJoinWithTheCode()
    {
        // Arrange
        (Guid _, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid championshipId = await CreateChampionshipAsync();

        HttpResponseMessage created = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/invites",
            new { role = "Player", expiresAtUtc = (DateTime?)null, maxUses = (int?)null });
        created.EnsureSuccessStatusCode();

        Invite? invite = await created.Content.ReadFromJsonAsync<Invite>();

        (Guid joinerId, AccessTokens joinerTokens) = await RegisterAndLoginAsync();
        Authenticate(joinerTokens.AccessToken);

        // Act — typed the way a person would, in lowercase.
        // CA1308 guards against lowercasing for normalization; here lowercase IS
        // the input under test, since the handler has to accept it.
#pragma warning disable CA1308
        string asTyped = invite!.Code.ToLowerInvariant();
#pragma warning restore CA1308

        HttpResponseMessage joined = await HttpClient.PostAsJsonAsync(
            "championships/join",
            new { code = asTyped });

        // Assert
        joined.EnsureSuccessStatusCode();
        JoinResult? result = await joined.Content.ReadFromJsonAsync<JoinResult>();
        result!.ChampionshipId.ShouldBe(championshipId);

        Summary[]? mine = await HttpClient.GetFromJsonAsync<Summary[]>("championships");
        Summary summary = mine!.Single();
        summary.Id.ShouldBe(championshipId);
        summary.Role.ShouldBe("Player");
        summary.MemberCount.ShouldBe(2);

        joinerId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Join_Should_ReturnNotFound_ForARevokedCode()
    {
        // Arrange
        (Guid _, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid championshipId = await CreateChampionshipAsync();

        HttpResponseMessage created = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/invites",
            new { role = "Player", expiresAtUtc = (DateTime?)null, maxUses = (int?)null });
        Invite? invite = await created.Content.ReadFromJsonAsync<Invite>();

        HttpResponseMessage revoked = await HttpClient.DeleteAsync(
            $"championships/{championshipId}/invites/{invite!.Id}");
        revoked.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (Guid _, AccessTokens joinerTokens) = await RegisterAndLoginAsync();
        Authenticate(joinerTokens.AccessToken);

        // Act
        HttpResponseMessage joined = await HttpClient.PostAsJsonAsync(
            "championships/join",
            new { code = invite.Code });

        // Assert
        joined.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Members_Should_RefuseARoleChangeFromAPlayer()
    {
        // Arrange
        (Guid ownerId, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid championshipId = await CreateChampionshipAsync();

        HttpResponseMessage created = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/invites",
            new { role = "Player", expiresAtUtc = (DateTime?)null, maxUses = (int?)null });
        Invite? invite = await created.Content.ReadFromJsonAsync<Invite>();

        (Guid _, AccessTokens playerTokens) = await RegisterAndLoginAsync();
        Authenticate(playerTokens.AccessToken);
        await HttpClient.PostAsJsonAsync("championships/join", new { code = invite!.Code });

        // Act — a Player trying to demote the Owner.
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"championships/{championshipId}/members/{ownerId}/role",
            new { role = "Player" });

        // Assert — the exact code matters. Asserting only "not a success" let a
        // 500 pass here, which is how the missing Forbidden error type reached
        // production: authorization failures fell through to Failure.
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Invites_Should_BeForbiddenForAPlayer()
    {
        // Arrange
        (Guid _, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid championshipId = await CreateChampionshipAsync();

        HttpResponseMessage created = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/invites",
            new { role = "Player", expiresAtUtc = (DateTime?)null, maxUses = (int?)null });
        Invite? invite = await created.Content.ReadFromJsonAsync<Invite>();

        (Guid _, AccessTokens playerTokens) = await RegisterAndLoginAsync();
        Authenticate(playerTokens.AccessToken);
        await HttpClient.PostAsJsonAsync("championships/join", new { code = invite!.Code });

        // Act — codes are credentials: reading one is enough to hand out membership.
        HttpResponseMessage response = await HttpClient.GetAsync(
            $"championships/{championshipId}/invites");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChipSets_Should_BeReadableByAPlayerButNotWritable()
    {
        // Arrange
        (Guid _, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid championshipId = await CreateChampionshipAsync();

        HttpResponseMessage created = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/invites",
            new { role = "Player", expiresAtUtc = (DateTime?)null, maxUses = (int?)null });
        Invite? invite = await created.Content.ReadFromJsonAsync<Invite>();

        (Guid _, AccessTokens playerTokens) = await RegisterAndLoginAsync();
        Authenticate(playerTokens.AccessToken);
        await HttpClient.PostAsJsonAsync("championships/join", new { code = invite!.Code });

        // Act + Assert — knowing what the case holds is part of playing, but only
        // an Admin changes it.
        HttpResponseMessage read = await HttpClient.GetAsync(
            $"championships/{championshipId}/chip-sets");
        read.StatusCode.ShouldBe(HttpStatusCode.OK);

        HttpResponseMessage write = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/chip-sets",
            new { name = "Minha", denominations = new[] { new { faceValue = 5, effectiveValue = 5, quantity = 1, colour = (string?)null } } });
        write.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TransferOwnership_Should_ReturnBadRequest_WhenSuccessorIsNotAnAdmin()
    {
        // Arrange
        (Guid _, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid championshipId = await CreateChampionshipAsync();

        HttpResponseMessage created = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/invites",
            new { role = "Player", expiresAtUtc = (DateTime?)null, maxUses = (int?)null });
        Invite? invite = await created.Content.ReadFromJsonAsync<Invite>();

        (Guid playerId, AccessTokens playerTokens) = await RegisterAndLoginAsync();
        Authenticate(playerTokens.AccessToken);
        await HttpClient.PostAsJsonAsync("championships/join", new { code = invite!.Code });

        Authenticate(ownerTokens.AccessToken);

        // Act — the caller is allowed to transfer; they just picked someone who
        // cannot receive it. That is a bad request, not a permission problem.
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/transfer-ownership",
            new { newOwnerId = playerId });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChipSet_Should_RoundTripWithTheEffectiveValueOverride()
    {
        // Arrange
        (Guid _, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid championshipId = await CreateChampionshipAsync();

        // Act — the case the whole override exists for: a chip printed 5, played
        // as 100.
        HttpResponseMessage created = await HttpClient.PostAsJsonAsync(
            $"championships/{championshipId}/chip-sets",
            new
            {
                name = "Maleta 300",
                denominations = new[]
                {
                    new { faceValue = 5, effectiveValue = 100, quantity = 100, colour = "Vermelha" },
                    new { faceValue = 25, effectiveValue = 500, quantity = 50, colour = "Verde" }
                }
            });

        // Assert
        created.EnsureSuccessStatusCode();

        ChipSetDto[]? sets = await HttpClient.GetFromJsonAsync<ChipSetDto[]>(
            $"championships/{championshipId}/chip-sets");

        ChipSetDto set = sets!.Single();
        set.Denominations.Length.ShouldBe(2);
        set.Denominations.Single(d => d.FaceValue == 5).EffectiveValue.ShouldBe(100);

        // 100 x 100 + 50 x 500 = 35,000 units.
        set.TotalUnits.ShouldBe(35_000);
    }


    private sealed record ChipSetDto(Guid Id, string Name, long TotalUnits, DenominationDto[] Denominations);

    private sealed record DenominationDto(Guid Id, int FaceValue, int EffectiveValue, int Quantity, string? Colour);

    [Fact]
    public async Task Reorder_Should_ChangeTheOrderTheListComesBackIn()
    {
        // Arrange
        (Guid _, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid first = await CreateChampionshipAsync("Primeira");
        Guid second = await CreateChampionshipAsync("Segunda");
        Guid third = await CreateChampionshipAsync("Terceira");

        // Act — the newest-created reordered to the very top.
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            "championships/order",
            new { championshipIds = new[] { third, first, second } });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        Summary[]? mine = await HttpClient.GetFromJsonAsync<Summary[]>("championships");
        mine!.Select(c => c.Id).ShouldBe([third, first, second]);
    }

    [Fact]
    public async Task Reorder_Should_RefuseAListMissingAChampionship()
    {
        // Arrange
        (Guid _, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid first = await CreateChampionshipAsync("Primeira");
        await CreateChampionshipAsync("Segunda");

        // Act — only one of the two the caller belongs to.
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            "championships/order",
            new { championshipIds = new[] { first } });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reorder_Should_RefuseAChampionshipThatIsNotTheCallers()
    {
        // Arrange
        (Guid _, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid notMine = await CreateChampionshipAsync("Não é minha");

        (Guid _, AccessTokens callerTokens) = await RegisterAndLoginAsync();
        Authenticate(callerTokens.AccessToken);
        Guid mine = await CreateChampionshipAsync("Minha");

        // Act — smuggling in someone else's championship id.
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            "championships/order",
            new { championshipIds = new[] { mine, notMine } });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reorder_Should_PlaceANewlyJoinedChampionshipAfterExistingOnes()
    {
        // Arrange
        (Guid _, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid theirs = await CreateChampionshipAsync("Deles");

        HttpResponseMessage created = await HttpClient.PostAsJsonAsync(
            $"championships/{theirs}/invites",
            new { role = "Player", expiresAtUtc = (DateTime?)null, maxUses = (int?)null });
        Invite? invite = await created.Content.ReadFromJsonAsync<Invite>();

        (Guid _, AccessTokens callerTokens) = await RegisterAndLoginAsync();
        Authenticate(callerTokens.AccessToken);
        Guid mine = await CreateChampionshipAsync("Minha");

        // Act — joining a second championship after already having one.
        await HttpClient.PostAsJsonAsync("championships/join", new { code = invite!.Code });

        // Assert — the one already there stays first; the new one lands after it.
        Summary[]? mine2 = await HttpClient.GetFromJsonAsync<Summary[]>("championships");
        mine2!.Select(c => c.Id).ShouldBe([mine, theirs]);
    }
}
