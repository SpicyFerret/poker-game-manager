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
}
