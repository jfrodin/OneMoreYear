using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Faces are inherited from the biological parents. Each person's face is created the first time it
/// is needed, with a random generator seeded from the world seed and the person's id – so portraits
/// are stable, work for old saves, and never disturb the simulation's own random numbers.
/// </summary>
public static class Faces
{
    public const int HairStyles = 4;

    public static Face Of(World w, Person p, Content.ContentDb? content = null)
    {
        if (p.Face != null) return p.Face;
        content ??= Content.ContentDb.Embedded;
        var parents = Kinship.BiologicalParents(w, p).Select(x => Of(w, x, content)).ToList();
        var rng = new SimRandom(w.Seed * 0x9E3779B97F4A7C15UL ^ (ulong)p.Id * 0xBF58476D1CE4E5B9UL);
        // The heritage can come from another country's names after an emigration.
        var heritage = content.Names.GetValueOrDefault(w.CountryId)?.Heritages.FirstOrDefault(h => h.Id == p.Heritage)
                       ?? content.Names.Values.SelectMany(n => n.Heritages).FirstOrDefault(h => h.Id == p.Heritage);
        p.Face = parents.Count == 0 ? Founder(p, rng, heritage) : Child(p, parents, rng);
        Style(p, p.Face, rng);
        return p.Face;
    }

    private static Face Founder(Person p, SimRandom rng, Content.HeritageDef? heritage)
    {
        double G(double mean = 0.5, double sd = 0.18) => Math.Clamp(rng.Gaussian(mean, sd), 0, 1);
        // Skin follows the heritage; for older saves without one, dark hair and brown eyes make darker skin more likely.
        bool darkFeatures = p.HairColor == "black" || (p.HairColor == "dark brown" && p.EyeColor == "brown");
        double skin = heritage is { Skin.Count: 2 } h
            ? rng.Range(h.Skin[0], h.Skin[1])
            : rng.NextDouble() switch
            {
                var r when darkFeatures && r < 0.35 => rng.Range(0.45, 0.95),
                var r when r < 0.06 => rng.Range(0.3, 0.5),
                _ => rng.Range(0.02, 0.25),
            };
        return new Face
        {
            Skin = skin,
            Width = G(), Jaw = G(p.Sex == Sex.Male ? 0.6 : 0.4), Chin = G(), Nose = G(p.Sex == Sex.Male ? 0.55 : 0.45),
            Eyes = G(), EyeSpacing = G(), Mouth = G(), Lips = G(p.Sex == Sex.Male ? 0.4 : 0.55), Brows = G(p.Sex == Sex.Male ? 0.6 : 0.4),
            Ears = G(), Curl = skin > 0.6 ? G(0.75, 0.15) : G(0.25, 0.2),
            Freckles = p.HairColor == "red" ? G(0.7, 0.2) : p.HairColor is "blond" or "dark blond" ? G(0.2, 0.2) : G(0.05, 0.1),
            Baldness = G(p.Sex == Sex.Male ? 0.5 : 0.1, 0.25), Greying = G(),
        };
    }

    private static Face Child(Person p, IReadOnlyList<Face> parents, SimRandom rng)
    {
        // Each gene comes from somewhere between the parents, with a little mutation.
        double Mix(Func<Face, double> gene, double mutation = 0.08)
        {
            double a = gene(parents[0]);
            double b = parents.Count > 1 ? gene(parents[1]) : a;
            double t = Math.Clamp(rng.Gaussian(0.5, 0.3), 0, 1);
            return Math.Clamp(a + (b - a) * t + rng.Gaussian(0, mutation), 0, 1);
        }
        double sexShift = p.Sex == Sex.Male ? 0.08 : -0.08;
        return new Face
        {
            Skin = Mix(f => f.Skin, 0.03),
            Width = Mix(f => f.Width), Jaw = Math.Clamp(Mix(f => f.Jaw) + sexShift, 0, 1), Chin = Mix(f => f.Chin),
            Nose = Math.Clamp(Mix(f => f.Nose) + sexShift * 0.5, 0, 1), Eyes = Mix(f => f.Eyes), EyeSpacing = Mix(f => f.EyeSpacing),
            Mouth = Mix(f => f.Mouth), Lips = Math.Clamp(Mix(f => f.Lips) - sexShift, 0, 1), Brows = Math.Clamp(Mix(f => f.Brows) + sexShift, 0, 1),
            Ears = Mix(f => f.Ears), Curl = Mix(f => f.Curl), Freckles = Mix(f => f.Freckles, 0.1),
            Baldness = Math.Clamp(Mix(f => f.Baldness, 0.15) + (p.Sex == Sex.Male ? 0.1 : -0.3), 0, 1), Greying = Mix(f => f.Greying, 0.12),
        };
    }

    private static void Style(Person p, Face f, SimRandom rng)
    {
        f.HairStyle = rng.Next(HairStyles);
        f.Beard = p.Sex == Sex.Male ? rng.PickWeighted(new[] { 0, 1, 2, 3 }, b => b switch { 0 => 5, 1 => 2, 2 => 1.5, _ => 0.6 }) : 0;
        // Near-sighted children, or reading glasses later in life.
        f.GlassesFromAge = rng.Chance(0.12 + p.Smarts / 1000) ? rng.Range(7, 30) : rng.Chance(0.3) ? rng.Range(45, 65) : 999;
    }

    /// <summary>0–1: how grey the hair is at this age.</summary>
    public static double GreyAt(Face f, int age) => Math.Clamp((age - (60 - f.Greying * 25)) / 32.0, 0, 1);

    /// <summary>0–1: how much hair is lost at this age.</summary>
    public static double BaldAt(Face f, Sex sex, int age)
    {
        double onset = sex == Sex.Male ? 70 - f.Baldness * 50 : 95 - f.Baldness * 20;
        return Math.Clamp((age - onset) / 25.0, 0, sex == Sex.Male ? 1 : 0.35);
    }
}
