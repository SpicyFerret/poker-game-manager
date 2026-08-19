using SharedKernel;

namespace Domain.Users;

public sealed class User : Entity
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }

    /// <summary>
    /// The name shown at the table and in rankings. Defaults to the first name
    /// at registration — nicknames are how people actually know each other in a
    /// home game.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Optional: without it the settlement report still says who owes whom, it
    /// just cannot show the payer where to send the money.
    /// </summary>
    public PaymentHandleType? PaymentType { get; set; }
    public string? PaymentHandle { get; set; }

    public string PasswordHash { get; set; }
}
