namespace Domain.Tables;

public enum LedgerEntryType
{
    /// <summary>Opening stack. Chips leave the case.</summary>
    BuyIn = 0,

    /// <summary>Topping up during play. Chips leave the case.</summary>
    Rebuy = 1,

    /// <summary>
    /// Bought chips from another player because the case was empty. Money in,
    /// no chips out of the case.
    /// </summary>
    ChipPurchaseFromPlayer = 2,

    /// <summary>
    /// The other side of that trade: handed over chips already in play and is
    /// credited what the buyer paid.
    /// </summary>
    ChipSaleToPlayer = 3,

    /// <summary>Manual correction, for when the table and the books disagree.</summary>
    Adjustment = 4,

    /// <summary>
    /// Cashed out and went home before the night ended. The only entry whose
    /// chips move *back into* the case rather than out of it, which is why the
    /// issued totals subtract them: those chips are in the case again and can be
    /// dealt to somebody else, and counting them as still out would put the
    /// night's reconciliation out by exactly that much.
    /// </summary>
    CashOut = 5
}

/// <summary>
/// One movement of money for one player. The night's whole accounting is the sum
/// of these plus the final chip count, and nothing else.
/// </summary>
public sealed class LedgerEntry
{
    public Guid Id { get; set; }
    public Guid TableId { get; set; }
    public Guid TablePlayerId { get; set; }
    public LedgerEntryType Type { get; set; }

    /// <summary>
    /// Always positive; the type says which way it moves. Signing the amount as
    /// well would let the two disagree.
    /// </summary>
    public decimal MoneyAmount { get; set; }

    /// <summary>The other player, for a chip trade between two people.</summary>
    public Guid? CounterpartyPlayerId { get; set; }

    /// <summary>
    /// When the player confirmed they had physically received these chips.
    ///
    /// Null means they have not seen the notice yet, which is how the table
    /// screen knows to put it in front of them. Only ever set for entries where
    /// chips actually left the case — a trade between players hands nothing over
    /// that the case can be wrong about.
    /// </summary>
    public DateTime? AcknowledgedAtUtc { get; set; }

    public string? Note { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Which chips left the case for this entry, per denomination. **Empty unless
    /// chips actually came out of the case** — a trade between players moves chips
    /// that were already in play, so it records none, and the end-of-night
    /// reconciliation still balances.
    /// </summary>
    public List<LedgerEntryChip> Chips { get; set; } = [];
}

public sealed class LedgerEntryChip
{
    public Guid Id { get; set; }
    public Guid LedgerEntryId { get; set; }
    public Guid ChipDenominationId { get; set; }
    public int Quantity { get; set; }
}
