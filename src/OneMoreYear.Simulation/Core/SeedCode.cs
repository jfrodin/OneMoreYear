namespace OneMoreYear.Simulation.Core;

/// <summary>
/// Shareable seed codes, like "7LB2WVPK". Any text works – "SVENSSON" is a world of its own – and is
/// turned into the numeric seed with a stable hash. Plain numbers are used as they are, so old
/// numeric seeds and the test scenarios still work.
/// </summary>
public static class SeedCode
{
    // No I, O, 0 or 1: easy to read out loud and type on a controller.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int Length = 8;

    public static string Random()
    {
        var rng = new SimRandom((ulong)DateTime.UtcNow.Ticks ^ (ulong)Environment.TickCount64 << 17);
        return new string(Enumerable.Range(0, Length).Select(_ => Alphabet[rng.Next(Alphabet.Length)]).ToArray());
    }

    /// <summary>Upper case, without spaces and dashes.</summary>
    public static string Normalize(string code) =>
        new string(code.Trim().ToUpperInvariant().Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());

    public static ulong ToSeed(string code)
    {
        string c = Normalize(code);
        if (c.Length > 0 && c.All(char.IsDigit) && ulong.TryParse(c, out var number)) return number;
        // FNV-1a: the same on every machine and in every version.
        ulong h = 14695981039346656037UL;
        foreach (char ch in c) h = (h ^ ch) * 1099511628211UL;
        return h;
    }
}
