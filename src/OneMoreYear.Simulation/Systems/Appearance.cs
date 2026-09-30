using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>Attributes (smarts, looks, fitness) and appearance, partly inherited from the parents.</summary>
public static class Appearance
{
    private static readonly string[] HairColors = { "black", "dark brown", "brown", "dark blond", "blond", "red" };
    private static readonly double[] HairWeights = { 1, 3, 3, 3, 2.5, 0.6 };
    private static readonly string[] EyeColors = { "brown", "blue", "green", "grey", "hazel" };
    private static readonly double[] EyeWeights = { 2, 4, 1.2, 1.5, 1.3 };
    private static readonly string[] Builds = { "slim", "average", "stocky" };

    public static void Generate(SimContext ctx, Person p, IReadOnlyList<Person> parents)
    {
        var rng = ctx.Rng;
        double Inherit(Func<Person, double> get, double spread)
        {
            if (parents.Count == 0) return Math.Clamp(rng.Gaussian(50, spread), 1, 100);
            double mid = parents.Average(get);
            return Math.Clamp(mid * 0.5 + 25 + rng.Gaussian(0, spread * 0.8), 1, 100);
        }
        p.Smarts = Inherit(x => x.Smarts, 15);
        p.Looks = Inherit(x => x.Looks, 15);
        p.Fitness = Math.Clamp(rng.Gaussian(55, 12), 5, 95);

        double mean = p.Sex == Sex.Male ? 180 : 166;
        double sameSexParentOffset = parents.Count == 0 ? 0
            : parents.Average(x => x.HeightCm - (x.Sex == Sex.Male ? 180 : 166));
        p.HeightCm = (int)Math.Round(mean + sameSexParentOffset * 0.6 + rng.Gaussian(0, 5));

        string Pick(string[] options, double[] weights, Func<Person, string> fromParent)
        {
            if (parents.Count > 0 && rng.Chance(0.7))
            {
                var fromP = fromParent(rng.Pick(parents));
                if (!string.IsNullOrEmpty(fromP)) return fromP;
            }
            var idx = rng.PickWeighted(Enumerable.Range(0, options.Length).ToList(), i => weights[i]);
            return options[idx];
        }
        p.HairColor = Pick(HairColors, HairWeights, x => x.HairColor);
        p.EyeColor = Pick(EyeColors, EyeWeights, x => x.EyeColor);
        p.Build = rng.Pick(Builds);
    }

    /// <summary>Fitness slowly fades with age unless you keep at it.</summary>
    public static void UpdateYear(SimContext ctx, Person p)
    {
        int age = p.Age(ctx.Year);
        double drift = age < 25 ? 0.3 : age < 45 ? -0.6 : -1.2;
        p.Fitness = Math.Clamp(p.Fitness + drift + ctx.Rng.Gaussian(0, 1), 1, 100);
        if (age > 50) p.Looks = Math.Clamp(p.Looks - 0.3, 1, 100);
    }

    public static string BuildText(Person p) =>
        p.Fitness >= 72 ? "athletic" : p.Fitness < 25 ? (p.Build == "slim" ? "frail" : "heavy") : p.Build;

    public static string HairText(Person p, int age) =>
        age >= 78 ? "white" : age >= 60 ? "grey" : age >= 50 ? $"greying {p.HairColor}" : p.HairColor;

    /// <summary>"186 cm, athletic build, dark blond hair and blue eyes."</summary>
    public static string Describe(Person p, int year)
    {
        int age = p.Age(year);
        if (age < 16)
        {
            string size = age < 3 ? "A baby" : age < 12 ? "A child" : "A teenager";
            return $"{size} with {HairText(p, age)} hair and {p.EyeColor} eyes.";
        }
        string height = p.HeightCm >= (p.Sex == Sex.Male ? 188 : 174) ? "Tall" : p.HeightCm <= (p.Sex == Sex.Male ? 172 : 158) ? "Short" : "Average height";
        return $"{height} ({p.HeightCm} cm), {BuildText(p)} build, {HairText(p, age)} hair and {p.EyeColor} eyes.";
    }
}
