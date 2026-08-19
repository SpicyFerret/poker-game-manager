using Domain.Users;

namespace Application.Users.GetById;

public sealed record UserResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    public string DisplayName { get; init; }

    public PaymentHandleType? PaymentType { get; init; }

    public string? PaymentHandle { get; init; }
}
