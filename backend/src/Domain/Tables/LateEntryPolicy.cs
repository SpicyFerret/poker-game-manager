namespace Domain.Tables;

/// <summary>
/// What happens when someone tries to sit down at a table already in play.
///
/// A yes/no answer turned out to be the wrong shape. "No" is what a table wants
/// early on and "yes" is what it wants when a friend turns up at ten o'clock,
/// and nobody wants to go and edit a setting at the moment it matters. Asking
/// leaves the decision where it belongs — with whoever is running the night, at
/// the time it comes up — without either pre-committing.
/// </summary>
public enum LateEntryPolicy
{
    /// <summary>Nobody joins once play has started.</summary>
    Blocked = 0,

    /// <summary>Anyone who could have joined before can still join, no questions asked.</summary>
    Open = 1,

    /// <summary>They ask, and a manager lets them in or turns them away.</summary>
    Request = 2
}
