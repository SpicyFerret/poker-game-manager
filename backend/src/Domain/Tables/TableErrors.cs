using SharedKernel;

namespace Domain.Tables;

public static class TableErrors
{
    public static Error NotFound(Guid tableId) => Error.NotFound(
        "Tables.NotFound",
        $"The table with the Id = '{tableId}' was not found in this championship");

    public static Error WrongStatus(TableStatus actual, TableStatus required) => Error.Conflict(
        "Tables.WrongStatus",
        $"This action needs the table to be {required}, but it is {actual}");

    public static readonly Error NotAPlayer = Error.NotFound(
        "Tables.NotAPlayer",
        "That person is not at this table");

    public static readonly Error AlreadyAtTheTable = Error.Conflict(
        "Tables.AlreadyAtTheTable",
        "That person is already at this table");

    public static readonly Error JoinRefused = Error.Forbidden(
        "Tables.JoinRefused",
        "This table is not open for you to join. Ask a table manager to add you");

    public static readonly Error WrongJoinCode = Error.NotFound(
        "Tables.WrongJoinCode",
        "That table code is not valid");

    public static readonly Error LateEntryNotAllowed = Error.Conflict(
        "Tables.LateEntryNotAllowed",
        "This table does not allow joining after it has started");

    public static readonly Error NoJoinRequestPending = Error.Conflict(
        "Tables.NoJoinRequestPending",
        "There is no request from this person waiting to be answered");

    public static readonly Error NoPlayers = Error.Problem(
        "Tables.NoPlayers",
        "A table cannot start with nobody at it");

    /// <summary>
    /// Carries the exact gap so the manager can decide: add chips to the case,
    /// lower the buy-in, or start with fewer players.
    /// </summary>
    public static Error NotEnoughChips(long shortfallUnits) => Error.Conflict(
        "Tables.NotEnoughChips",
        $"The chip case cannot cover this, {shortfallUnits} units short. Add chips to the case, or lower the buy-in");

    /// <summary>
    /// The opening deal, where the shortfall is per player rather than in total —
    /// saying "300 units short" when every one of five stacks is 300 short would
    /// send someone hunting for 300 units when the case needs 1500.
    /// </summary>
    public static Error NotEnoughChipsForStacks(long shortfallUnits, int playerCount) => Error.Conflict(
        "Tables.NotEnoughChips",
        $"The chip case cannot make {playerCount} equal stacks — each one is {shortfallUnits} units short. " +
        "Add chips to the case, or lower the buy-in");

    public static readonly Error ChipSetEmpty = Error.Problem(
        "Tables.ChipSetEmpty",
        "The chosen chip case has no chips in it");

    public static readonly Error CounterpartyIsTheSamePlayer = Error.Problem(
        "Tables.CounterpartyIsTheSamePlayer",
        "A player cannot buy chips from themselves");

    public static readonly Error CounterpartyNotPlaying = Error.Problem(
        "Tables.CounterpartyNotPlaying",
        "Chips can only be bought from someone still in the game");

    public static readonly Error PlayerNotPlaying = Error.Conflict(
        "Tables.PlayerNotPlaying",
        "That player is not in the game right now");

    public static readonly Error ChipSetBelongsToAnotherChampionship = Error.Problem(
        "Tables.ChipSetBelongsToAnotherChampionship",
        "That chip case belongs to a different championship");

    public static readonly Error AmountMustBePositive = Error.Problem(
        "Tables.AmountMustBePositive",
        "The amount must be greater than zero");

    public static readonly Error CountsDoNotBalance = Error.Conflict(
        "Tables.CountsDoNotBalance",
        "The chips counted do not match what left the case. Settle up only once they do");

    public static readonly Error StillWaitingOnCounts = Error.Conflict(
        "Tables.StillWaitingOnCounts",
        "Some players have not reported what they are holding yet");

    public static readonly Error QuantityCannotBeNegative = Error.Problem(
        "Tables.QuantityCannotBeNegative",
        "A chip count cannot be negative");

    public static readonly Error DenominationNotInThisCase = Error.Problem(
        "Tables.DenominationNotInThisCase",
        "One of the chips reported is not part of this table's chip case");

    public static readonly Error CannotCountForSomeoneElse = Error.Forbidden(
        "Tables.CannotCountForSomeoneElse",
        "You can only report your own chips. A table manager can report for anyone");

    public static readonly Error AlreadySettled = Error.Conflict(
        "Tables.AlreadySettled",
        "This table has already been settled");

    public static readonly Error InvalidBlindLevel = Error.Problem(
        "Tables.InvalidBlindLevel",
        "Blinds must be greater than zero, and ante and duration cannot be negative");

    /// <summary>
    /// The clock is optional: a table with no blind ladder has nothing to count.
    /// </summary>
    public static readonly Error NoBlindLevels = Error.Problem(
        "Tables.NoBlindLevels",
        "This table has no blind levels, so it has no clock. Add levels first");

    /// <summary>
    /// Typing the name is the only thing between a misplaced tap and a night's
    /// bookkeeping. A yes/no prompt is too easy to answer wrong at 2am.
    /// </summary>
    public static readonly Error ConfirmationDoesNotMatch = Error.Problem(
        "Tables.ConfirmationDoesNotMatch",
        "Type the table's name exactly to confirm");

    public static readonly Error StackNotFound = Error.NotFound(
        "Tables.StackNotFound",
        "That stack does not belong to this table");

    /// <summary>
    /// The point of the notice is that a second person counted the chips. A
    /// manager confirming for the player would be the same pair of eyes that
    /// counted them out of the case.
    /// </summary>
    public static readonly Error CannotAcknowledgeSomeoneElsesStack = Error.Forbidden(
        "Tables.CannotAcknowledgeSomeoneElsesStack",
        "Only the player themselves can confirm they received these chips");

    /// <summary>A rebuy is self-service; dealing someone else in, or rebuying for someone else, is not.</summary>
    public static readonly Error CannotRebuySomeoneElse = Error.Forbidden(
        "Tables.CannotRebuySomeoneElse",
        "You can only rebuy for yourself. A table manager can rebuy for anyone");

    public static readonly Error CannotDealInSomeoneElse = Error.Forbidden(
        "Tables.CannotDealInSomeoneElse",
        "Only a table manager can deal a player in");

    /// <summary>Going home is your own decision; sending someone else home is not.</summary>
    public static readonly Error CannotCashOutSomeoneElse = Error.Forbidden(
        "Tables.CannotCashOutSomeoneElse",
        "You can only cash out for yourself. A table manager can cash out anyone");

    /// <summary>
    /// More of a chip handed back than the whole table was ever given. That is a
    /// miscount, and letting it through would drive the issued total negative and
    /// quietly break the reconciliation for everyone still playing.
    /// </summary>
    public static readonly Error CashOutMoreThanIsInPlay = Error.Problem(
        "Tables.CashOutMoreThanIsInPlay",
        "That is more chips than this table has in play. Count again");

    /// <summary>
    /// Chips left the case for this player and they paid in for them. Removing
    /// the row would leave those chips belonging to nobody and the night unable
    /// to reconcile — someone who is done playing leaves the table, they do not
    /// vanish from its books.
    /// </summary>
    public static readonly Error CannotRemoveAPlayerWhoHasChips = Error.Conflict(
        "Tables.CannotRemoveAPlayerWhoHasChips",
        "This player has already been dealt in, so they cannot be taken off the table");

    /// <summary>Buying chips off another player is self-service; recording that purchase for someone else is not.</summary>
    public static readonly Error CannotBuyChipsForSomeoneElse = Error.Forbidden(
        "Tables.CannotBuyChipsForSomeoneElse",
        "You can only record a purchase where you are the buyer. A table manager can record one for anyone");
}
