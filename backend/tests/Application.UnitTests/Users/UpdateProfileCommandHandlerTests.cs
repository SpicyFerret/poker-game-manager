using Application.Abstractions.Authentication;
using Application.UnitTests.Abstractions;
using Application.Users.UpdateProfile;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class UpdateProfileCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid CallerId = Guid.NewGuid();

    private static IUserContext CallerContext()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CallerId);
        return userContext;
    }

    private static User Caller() => new()
    {
        Id = CallerId,
        Email = "caller@example.com",
        FirstName = "Caller",
        LastName = "User",
        DisplayName = "Caller",
        PasswordHash = "hash"
    };

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenCallerDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new UpdateProfileCommandHandler(context, CallerContext());

        // Act
        Result result = await handler.Handle(
            new UpdateProfileCommand("Dan", null, null),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(CallerId));
    }

    [Fact]
    public async Task Handle_Should_UpdateDisplayNameAndPaymentHandle()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        context.Users.Add(Caller());
        await context.SaveChangesAsync();

        var handler = new UpdateProfileCommandHandler(context, CallerContext());

        // Act
        Result result = await handler.Handle(
            new UpdateProfileCommand("  Dan  ", PaymentHandleType.Pix, "  dan@example.com  "),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        User user = await context.Users.SingleAsync(u => u.Id == CallerId);
        user.DisplayName.ShouldBe("Dan");
        user.PaymentType.ShouldBe(PaymentHandleType.Pix);
        user.PaymentHandle.ShouldBe("dan@example.com");
    }

    [Fact]
    public async Task Handle_Should_ClearPaymentHandle_WhenNullsAreSent()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User caller = Caller();
        caller.PaymentType = PaymentHandleType.Pix;
        caller.PaymentHandle = "dan@example.com";
        context.Users.Add(caller);
        await context.SaveChangesAsync();

        var handler = new UpdateProfileCommandHandler(context, CallerContext());

        // Act
        Result result = await handler.Handle(
            new UpdateProfileCommand("Dan", null, null),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        User user = await context.Users.SingleAsync(u => u.Id == CallerId);
        user.PaymentType.ShouldBeNull();
        user.PaymentHandle.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_NotTouchAnotherUser()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var otherId = Guid.NewGuid();
        context.Users.Add(Caller());
        context.Users.Add(new User
        {
            Id = otherId,
            Email = "other@example.com",
            FirstName = "Other",
            LastName = "User",
            DisplayName = "Other",
            PasswordHash = "hash"
        });
        await context.SaveChangesAsync();

        var handler = new UpdateProfileCommandHandler(context, CallerContext());

        // Act
        await handler.Handle(new UpdateProfileCommand("Dan", null, null), CancellationToken.None);

        // Assert
        User other = await context.Users.SingleAsync(u => u.Id == otherId);
        other.DisplayName.ShouldBe("Other");
    }
}
