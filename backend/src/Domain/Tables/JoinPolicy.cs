namespace Domain.Tables;

public enum JoinPolicy
{
    /// <summary>Anyone in the championship can sit down.</summary>
    AnyMember = 0,

    /// <summary>A manager adds people; nobody joins on their own.</summary>
    InviteOnly = 1,

    /// <summary>Anyone in the championship holding the table's code.</summary>
    Code = 2
}
