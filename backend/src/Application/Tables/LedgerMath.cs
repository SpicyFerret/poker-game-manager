using Domain.Tables;

namespace Application.Tables;

/// <summary>
/// How each kind of ledger entry moves a player's stake.
///
/// One definition, because there are three screens that need it — the live
/// table, the settlement, and a player's statement — and three copies of a
/// money rule is three chances for them to disagree about what someone is
/// owed. A cash-out was very nearly the fourth copy.
/// </summary>
public static class LedgerMath
{
    /// <summary>
    /// What this entry contributes to a player's <c>PaidIn</c>: positive for
    /// money going in, negative for chips handed back and paid for.
    /// </summary>
    public static decimal SignedPaidIn(LedgerEntryType type, decimal amount) => type switch
    {
        LedgerEntryType.BuyIn or
        LedgerEntryType.Rebuy or
        LedgerEntryType.ChipPurchaseFromPlayer or
        LedgerEntryType.Adjustment => amount,

        // Both hand chips back and take money for them. For a cash-out that is
        // the whole night collected early: the final count will be nothing, and
        // this is what stops that reading as having lost the lot.
        LedgerEntryType.ChipSaleToPlayer or
        LedgerEntryType.CashOut => -amount,

        _ => 0m
    };

    public static decimal PaidIn(IEnumerable<LedgerEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries.Sum(entry => SignedPaidIn(entry.Type, entry.MoneyAmount));
    }
}
