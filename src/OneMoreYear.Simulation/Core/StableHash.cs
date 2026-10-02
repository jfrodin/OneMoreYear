namespace OneMoreYear.Simulation.Core;

/// <summary>FNV-1a: the same on every machine and run (string.GetHashCode is not).</summary>
public static class StableHash
{
    public static ulong Of(string s)
    {
        ulong h = 14695981039346656037UL;
        foreach (char c in s) h = (h ^ c) * 1099511628211UL;
        return h;
    }
}
