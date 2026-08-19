using Application.Abstractions.Authentication;
using Application.Championships.TransferOwnership;
using Application.UnitTests.Abstractions;
using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Championships;

public sealed class TransferOwnershipCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid ChampionshipId = Guid.NewGuid();
    private static readonly Guid CurrentOwnerId = Guid.NewGuid();
    private static readonly Guid SuccessorId = Guid.NewGuid();

    private static async Task<TestDbContext> SeedAsync(ChampionshipRole successorRole)
    {
        TestDbContext context = CreateDbContext();

        context.Championships.Add(new Championship
        {
            Id = ChampionshipId,
            OwnerId = CurrentOwnerId,
            Name = "Quinta-feira",
            MoneyPerUnit = 0.05m,
            DefaultBuyIn = 50m,
            DefaultRebuy = 50m
        });

        context.ChampionshipMembers.AddRange(
            new ChampionshipMember
            {
                Id = Guid.NewGuid(),
                ChampionshipId = ChampionshipId,
                UserId = CurrentOwnerId,
                Role = ChampionshipRole.Owner,
                JoinedAtUtc = DateTime.UtcNow
            },
            new ChampionshipMember
            {
                Id = Guid.NewGuid(),
                ChampionshipId = ChampionshipId,
                UserId = SuccessorId,
                Role = successorRole,
                JoinedAtUtc = DateTime.UtcNow
            });

        await context.SaveChangesAsync();

        return context;
    }

    private static TransferOwnershipCommandHandler HandlerFor(TestDbContext context, ChampionshipRole callerRole)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CurrentOwnerId);

        return new TransferOwnershipCommandHandler(context, new FakeChampionshipContext(callerRole), userContext);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenCallerIsNotTheOwner()
    {
        await using TestDbContext context = await SeedAsync(ChampionshipRole.Admin);

        Result result = await HandlerFor(context, ChampionshipRole.Admin).Handle(
            new TransferOwnershipCommand(ChampionshipId, SuccessorId),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ChampionshipErrors.InsufficientRole(ChampionshipRole.Owner));
    }

    [Theory]
    [InlineData(ChampionshipRole.Player)]
    [InlineData(ChampionshipRole.TableManager)]
    public async Task Handle_Should_Fail_WhenSuccessorIsNotAnAdmin(ChampionshipRole successorRole)
    {
        await using TestDbContext context = await SeedAsync(successorRole);

        Result result = await HandlerFor(context, ChampionshipRole.Owner).Handle(
            new TransferOwnershipCommand(ChampionshipId, SuccessorId),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ChampionshipErrors.NewOwnerMustBeAdmin);
    }

    [Fact]
    public async Task Handle_Should_SwapRolesAndMoveOwnership()
    {
        await using TestDbContext context = await SeedAsync(ChampionshipRole.Admin);

        Result result = await HandlerFor(context, ChampionshipRole.Owner).Handle(
            new TransferOwnershipCommand(ChampionshipId, SuccessorId),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        Championship championship = await context.Championships.SingleAsync();
        championship.OwnerId.ShouldBe(SuccessorId);

        List<ChampionshipMember> members = await context.ChampionshipMembers.ToListAsync();

        // Exactly one owner, and the outgoing one stays on as an Admin rather
        // than dropping out of the championship entirely.
        members.Count(m => m.Role == ChampionshipRole.Owner).ShouldBe(1);
        members.Single(m => m.UserId == SuccessorId).Role.ShouldBe(ChampionshipRole.Owner);
        members.Single(m => m.UserId == CurrentOwnerId).Role.ShouldBe(ChampionshipRole.Admin);
    }
}
