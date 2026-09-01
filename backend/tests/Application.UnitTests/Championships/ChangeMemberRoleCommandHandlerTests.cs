using Application.Abstractions.Realtime;
using Application.Championships.Members.ChangeRole;
using Application.UnitTests.Abstractions;
using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Championships;

public sealed class ChangeMemberRoleCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid ChampionshipId = Guid.NewGuid();
    private static readonly Guid TargetUserId = Guid.NewGuid();

    private static async Task<TestDbContext> ContextWithMemberAsync(ChampionshipRole targetRole)
    {
        TestDbContext context = CreateDbContext();

        context.ChampionshipMembers.Add(new ChampionshipMember
        {
            Id = Guid.NewGuid(),
            ChampionshipId = ChampionshipId,
            UserId = TargetUserId,
            Role = targetRole,
            JoinedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        return context;
    }

    private static ChangeMemberRoleCommandHandler HandlerFor(TestDbContext context, ChampionshipRole callerRole) =>
        new(context, new FakeChampionshipContext(callerRole), Substitute.For<IChampionshipActivityNotifier>());

    [Fact]
    public async Task Handle_Should_Fail_WhenCallerIsOnlyATableManager()
    {
        await using TestDbContext context = await ContextWithMemberAsync(ChampionshipRole.Player);

        Result result = await HandlerFor(context, ChampionshipRole.TableManager).Handle(
            new ChangeMemberRoleCommand(ChampionshipId, TargetUserId, ChampionshipRole.TableManager),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ChampionshipErrors.InsufficientRole(ChampionshipRole.Admin));
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenAssigningOwner()
    {
        await using TestDbContext context = await ContextWithMemberAsync(ChampionshipRole.Admin);

        Result result = await HandlerFor(context, ChampionshipRole.Owner).Handle(
            new ChangeMemberRoleCommand(ChampionshipId, TargetUserId, ChampionshipRole.Owner),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ChampionshipErrors.OwnerRoleIsTransferredNotAssigned);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenTargetHoldsTheSameRoleAsTheCaller()
    {
        await using TestDbContext context = await ContextWithMemberAsync(ChampionshipRole.Admin);

        Result result = await HandlerFor(context, ChampionshipRole.Admin).Handle(
            new ChangeMemberRoleCommand(ChampionshipId, TargetUserId, ChampionshipRole.Player),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ChampionshipErrors.CannotActOnEqualOrHigherRole);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenPromotingSomeoneToTheCallersOwnRole()
    {
        // The half of the rule that is easy to forget: an Admin promoting a
        // Player to Admin would create a peer they can no longer demote.
        await using TestDbContext context = await ContextWithMemberAsync(ChampionshipRole.Player);

        Result result = await HandlerFor(context, ChampionshipRole.Admin).Handle(
            new ChangeMemberRoleCommand(ChampionshipId, TargetUserId, ChampionshipRole.Admin),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ChampionshipErrors.CannotActOnEqualOrHigherRole);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenMemberIsNotInTheChampionship()
    {
        await using TestDbContext context = CreateDbContext();

        Result result = await HandlerFor(context, ChampionshipRole.Owner).Handle(
            new ChangeMemberRoleCommand(ChampionshipId, TargetUserId, ChampionshipRole.Admin),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ChampionshipErrors.MemberNotFound);
    }

    [Theory]
    [InlineData(ChampionshipRole.Owner, ChampionshipRole.Admin)]
    [InlineData(ChampionshipRole.Owner, ChampionshipRole.TableManager)]
    [InlineData(ChampionshipRole.Admin, ChampionshipRole.TableManager)]
    public async Task Handle_Should_ChangeRole_WhenStrictlyBelowTheCaller(
        ChampionshipRole callerRole,
        ChampionshipRole newRole)
    {
        await using TestDbContext context = await ContextWithMemberAsync(ChampionshipRole.Player);

        Result result = await HandlerFor(context, callerRole).Handle(
            new ChangeMemberRoleCommand(ChampionshipId, TargetUserId, newRole),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        ChampionshipMember member = await context.ChampionshipMembers.SingleAsync(m => m.UserId == TargetUserId);
        member.Role.ShouldBe(newRole);
    }
}
