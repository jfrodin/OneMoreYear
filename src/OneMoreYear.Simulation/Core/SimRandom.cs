namespace OneMoreYear.Simulation.Core;

/// <summary>
/// Deterministic random generator (xoshiro256**). We use our own implementation instead of
/// System.Random so that results never change between .NET versions and the full state can be
/// saved and restored with the world.
/// </summary>
public sealed class SimRandom
{
    public ulong[] State { get; set; } = new ulong[4];

    public SimRandom() { }

    public SimRandom(ulong seed)
    {
        // Seed the state with SplitMix64 as recommended by the xoshiro authors.
        for (int i = 0; i < 4; i++)
        {
            seed += 0x9E3779B97F4A7C15UL;
            ulong z = seed;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            State[i] = z ^ (z >> 31);
        }
    }

    public ulong NextULong()
    {
        var s = State;
        ulong result = RotL(s[1] * 5, 7) * 9;
        ulong t = s[1] << 17;
        s[2] ^= s[0];
        s[3] ^= s[1];
        s[1] ^= s[2];
        s[0] ^= s[3];
        s[2] ^= t;
        s[3] = RotL(s[3], 45);
        return result;
    }

    /// <summary>Uniform double in [0, 1).</summary>
    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

    /// <summary>Uniform int in [0, maxExclusive).</summary>
    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 0) return 0;
        return (int)(NextDouble() * maxExclusive);
    }

    /// <summary>Uniform int in [min, maxInclusive].</summary>
    public int Range(int min, int maxInclusive) => min + Next(maxInclusive - min + 1);

    public double Range(double min, double max) => min + NextDouble() * (max - min);

    public bool Chance(double probability) => NextDouble() < probability;

    /// <summary>Approximately normal distributed value (sum of uniforms).</summary>
    public double Gaussian(double mean, double stdDev)
    {
        double sum = 0;
        for (int i = 0; i < 6; i++) sum += NextDouble();
        return mean + (sum - 3.0) * stdDev / 0.7071;
    }

    public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];

    public T? PickWeighted<T>(IReadOnlyList<T> items, Func<T, double> weight)
    {
        double total = 0;
        foreach (var item in items) total += Math.Max(0, weight(item));
        if (total <= 0) return default;
        double roll = NextDouble() * total;
        foreach (var item in items)
        {
            roll -= Math.Max(0, weight(item));
            if (roll < 0) return item;
        }
        return items[^1];
    }

    private static ulong RotL(ulong x, int k) => (x << k) | (x >> (64 - k));
}
