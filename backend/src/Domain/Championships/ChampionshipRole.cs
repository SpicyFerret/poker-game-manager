namespace Domain.Championships;

/// <summary>
/// Ordered on purpose: a member may only act on roles strictly below their own,
/// so the comparison is the rule. Don't reorder or renumber — the values are
/// persisted and compared.
/// </summary>
public enum ChampionshipRole
{
    Player = 0,
    TableManager = 1,
    Admin = 2,
    Owner = 3
}
