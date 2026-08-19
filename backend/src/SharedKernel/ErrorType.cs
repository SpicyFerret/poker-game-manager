namespace SharedKernel;

public enum ErrorType
{
    /// <summary>Something went wrong on our side. Surfaces as a 500.</summary>
    Failure = 0,
    Validation = 1,
    Problem = 2,
    NotFound = 3,
    Conflict = 4,

    /// <summary>
    /// The caller is authenticated but not allowed to do this. Without it,
    /// authorization failures fell into Failure and came back as 500s with the
    /// reason stripped out — the caller could not tell a permission problem from
    /// a broken server.
    /// </summary>
    Forbidden = 5
}
