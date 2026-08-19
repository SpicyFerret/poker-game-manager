namespace Domain.Users;

/// <summary>
/// How a player wants to be paid when a table is settled. Pix is the default
/// because it is what this was built for, but the settlement report only needs
/// something it can show the payer — keeping it generic costs nothing and makes
/// the system usable outside Brazil.
/// </summary>
public enum PaymentHandleType
{
    Pix = 0,
    Other = 1
}
