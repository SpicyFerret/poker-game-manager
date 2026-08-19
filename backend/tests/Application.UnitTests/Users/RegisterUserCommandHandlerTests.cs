using Application.Abstractions.Authentication;
using Application.UnitTests.Abstractions;
using Application.Users.Register;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class RegisterUserCommandHandlerTests : BaseHandlerTest
{
    private static RegisterUserCommand Command =>
        new("test@example.com", "Test", "User", "Password123");

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenEmailIsNotUnique()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = Command.Email,
            FirstName = "Existing",
            LastName = "User",
            DisplayName = "Existing",
            PasswordHash = "hash"
        });
        await context.SaveChangesAsync();

        var handler = new RegisterUserCommandHandler(context, Substitute.For<IPasswordHasher>());

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.EmailNotUnique);
    }

    [Fact]
    public async Task Handle_Should_CreateUserWithHashedPasswordAndRaiseDomainEvent_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Hash(Command.Password).Returns("hashed-password");

        var handler = new RegisterUserCommandHandler(context, passwordHasher);

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        User user = await context.Users.SingleAsync(u => u.Id == result.Value);
        user.Email.ShouldBe(Command.Email);
        user.PasswordHash.ShouldBe("hashed-password");
        user.DomainEvents.ShouldContain(domainEvent => domainEvent is UserRegisteredDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_DefaultDisplayNameToFirstName_WhenNotProvided()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new RegisterUserCommandHandler(context, Substitute.For<IPasswordHasher>());

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        User user = await context.Users.SingleAsync(u => u.Id == result.Value);
        user.DisplayName.ShouldBe(Command.FirstName);
    }

    [Theory]
    [InlineData("  Dan  ", "Dan")]
    [InlineData("Dan", "Dan")]
    public async Task Handle_Should_UseTrimmedDisplayName_WhenProvided(string provided, string expected)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new RegisterUserCommandHandler(context, Substitute.For<IPasswordHasher>());

        // Act
        Result<Guid> result = await handler.Handle(
            Command with { DisplayName = provided },
            CancellationToken.None);

        // Assert
        User user = await context.Users.SingleAsync(u => u.Id == result.Value);
        user.DisplayName.ShouldBe(expected);
    }
}
