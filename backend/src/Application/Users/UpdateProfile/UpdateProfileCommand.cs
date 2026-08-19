using Application.Abstractions.Messaging;
using Domain.Users;

namespace Application.Users.UpdateProfile;

/// <summary>
/// Always applies to the caller — there is no user id here on purpose, so no
/// endpoint can be tricked into editing someone else's payment handle.
/// </summary>
public sealed record UpdateProfileCommand(
    string DisplayName,
    PaymentHandleType? PaymentType,
    string? PaymentHandle)
    : ICommand;
