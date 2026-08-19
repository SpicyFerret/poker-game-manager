using System.Security.Cryptography;

namespace Domain.Championships;

public static class InviteCode
{
    /// <summary>
    /// No 0/O or 1/I/L: these codes get read aloud at a table and retyped on a
    /// phone, and those are the pairs people get wrong. U is dropped too, so no
    /// short code can accidentally spell something unfortunate.
    /// </summary>
    private const string Alphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789";

    public const int Length = 6;

    public static string Generate()
    {
        char[] code = new char[Length];

        for (int i = 0; i < Length; i++)
        {
            code[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(code);
    }

    /// <summary>
    /// Accepts what a person actually types: lowercase, and spaces or dashes they
    /// added while reading it out.
    /// </summary>
    public static string Normalize(string code) =>
        code.Trim().Replace(" ", string.Empty).Replace("-", string.Empty).ToUpperInvariant();

    public static bool IsWellFormed(string code) =>
        code.Length == Length && code.All(Alphabet.Contains);
}
