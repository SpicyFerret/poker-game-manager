using Application.Abstractions.Authentication;
using Application.Abstractions.Realtime;
using Application.Championships.Join;
using Application.UnitTests.Abstractions;
using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Championships;

public sealed class JoinByCodeCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid ChampionshipId = Guid.NewGuid();
    private static readonly Guid JoinerId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    private const string Code = "ABCD23";

    private static async Task<TestDbContext> SeedAsync(Action<Invite>? customise = null)
    {
        TestDbContext context = CreateDbContext();

        context.Championships.Add(new Championship
        {
            Id = ChampionshipId,
            OwnerId = Guid.NewGuid(),
            Name = "Quinta-feira",
            MoneyPerUnit = 0.05m
        });

        var invite = new Invite
        {
            Id = Guid.NewGuid(),
            ChampionshipId = ChampionshipId,
            Code = Code,
            Role = ChampionshipRole.Player,
            CreatedBy = Guid.NewGuid(),
            CreatedAtUtc = Now.AddDays(-1)
        };

        customise?.Invoke(invite);

        context.Invites.Add(invite);

        await context.SaveChangesAsync();

        return context;
    }

    private static JoinByCodeCommandHandler HandlerFor(TestDbContext context)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(JoinerId);

        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        IChampionshipActivityNotifier notifier = Substitute.For<IChampionshipActivityNotifier>();

        return new JoinByCodeCommandHandler(context, userContext, clock, notifier);
    }

    [Theory]
    [InlineData("abcd23")]
    [InlineData(" ABCD23 ")]
    [InlineData("ABC-D23")]
    public async Task Handle_Should_AcceptTheCodeAsPeopleActuallyTypeIt(string typed)
    {
        await using TestDbContext context = await SeedAsync();

        Result<JoinByCodeResponse> result = await HandlerFor(context).Handle(
            new JoinByCodeCommand(typed),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ChampionshipId.ShouldBe(ChampionshipId);
    }

    [Fact]
    public async Task Handle_Should_AddTheMemberWithTheInvitedRoleAndCountTheUse()
    {
        await using TestDbContext context = await SeedAsync(i => i.Role = ChampionshipRole.TableManager);

        await HandlerFor(context).Handle(new JoinByCodeCommand(Code), CancellationToken.None);

        ChampionshipMember member = await context.ChampionshipMembers.SingleAsync();
        member.UserId.ShouldBe(JoinerId);
        member.Role.ShouldBe(ChampionshipRole.TableManager);

        Invite invite = await context.Invites.SingleAsync();
        invite.Uses.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_Succeed_WithoutBurningAUse_WhenAlreadyAMember()
    {
        // Someone tapping the group's link twice should land in the championship,
        // not read an error — and the second tap must not consume a use.
        await using TestDbContext context = await SeedAsync();
        context.ChampionshipMembers.Add(new ChampionshipMember
        {
            Id = Guid.NewGuid(),
            ChampionshipId = ChampionshipId,
            UserId = JoinerId,
            Role = ChampionshipRole.Player,
            JoinedAtUtc = Now
        });
        await context.SaveChangesAsync();

        Result<JoinByCodeResponse> result = await HandlerFor(context).Handle(
            new JoinByCodeCommand(Code),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await context.ChampionshipMembers.CountAsync()).ShouldBe(1);
        (await context.Invites.SingleAsync()).Uses.ShouldBe(0);
    }

    public static TheoryData<string, Action<Invite>?> UnusableInvites => new()
    {
        { Code, i => i.ExpiresAtUtc = Now.AddMinutes(-1) },
        { Code, i => { i.MaxUses = 2; i.Uses = 2; } },
        { Code, i => i.IsRevoked = true },
        { "ZZZZZZ", null },
        { "SHORT", null },
        { "ABCD2O", null },
    };

    [Theory]
    [MemberData(nameof(UnusableInvites))]
    public async Task Handle_Should_ReturnTheSameError_ForEveryUnusableCode(
        string code,
        Action<Invite>? customise)
    {
        // One error for "expired", "used up", "revoked", "no such code" and
        // "malformed" alike: telling them apart would let someone probe for
        // valid codes.
        await using TestDbContext context = await SeedAsync(customise);

        Result<JoinByCodeResponse> result = await HandlerFor(context).Handle(
            new JoinByCodeCommand(code),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(InviteErrors.NotUsable);
        (await context.ChampionshipMembers.CountAsync()).ShouldBe(0);
    }
}
