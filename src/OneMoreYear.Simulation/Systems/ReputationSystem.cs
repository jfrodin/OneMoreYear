using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// The family's reputation (docs/endgame.md): four meters read from how the last three generations of
/// the bloodline actually lived. When a meter passes a threshold the family becomes known for it (a
/// family trait, content/reputation.json), which shapes life for everyone of that blood; it is lost
/// again if the family changes. Nothing is announced in advance.
/// </summary>
public static class ReputationSystem
{
    public const string Learning = "learning", Wealth = "wealth", Warmth = "warmth", Notoriety = "notoriety";
    public const string GainedEvent = "family_trait_gained", LostEvent = "family_trait_lost";

    /// <summary>Adults of the blood in the last three generations, alive or dead.</summary>
    private static List<Person> Recent(World w)
    {
        var blood = w.People.Where(p => p.IsBlood && p.Age(p.DeathYear ?? w.Year) >= 25).ToList();
        if (blood.Count == 0) return blood;
        int newest = blood.Max(p => p.Generation);
        return blood.Where(p => p.Generation >= newest - 2).ToList();
    }

    public static Dictionary<string, double> Meters(SimContext ctx)
    {
        var w = ctx.World;
        var recent = Recent(w);
        var meters = new Dictionary<string, double> { [Learning] = 0, [Wealth] = 0, [Warmth] = 0, [Notoriety] = 0 };
        if (recent.Count < 3) return meters;
        // Learning: the share with a university degree, in percent.
        meters[Learning] = 100.0 * recent.Count(p => p.Education == EducationLevel.University) / recent.Count;
        // Wealth: the median of the best each one ever had, in million reference kronor.
        var worth = recent.Select(p => p.PeakRefWorth).OrderBy(v => v).ToList();
        meters[Wealth] = worth[worth.Count / 2] / 1e6;
        // Warmth: how grown children feel about their living parents.
        var opinions = recent.Where(p => p.IsAlive)
            .SelectMany(c => Kinship.Parents(w, c).Where(pa => pa.IsAlive).Select(pa => w.Opinion(c.Id, pa.Id))).ToList();
        meters[Warmth] = opinions.Count >= 2 ? opinions.Average() : 0;
        // Giving to others warms how people see the family, for a lifetime or two.
        meters[Warmth] += ctx.World.Gifts.Count(g => ctx.Year - g.Year < 60) * 6;
        // Notoriety: convictions and secrets that came out, per ten adults.
        int scandals = recent.Sum(p => p.CriminalRecord.Count)
                       + w.Secrets.Count(s => s.Revealed && s.Kind is "affair" or "murder" && recent.Any(p => p.Id == s.SubjectId));
        meters[Notoriety] = scandals * 10.0 / recent.Count;
        return meters;
    }

    /// <summary>Yearly: the family gains or loses what it is known for (only once a second generation is played).</summary>
    public static void Update(SimContext ctx)
    {
        var w = ctx.World;
        if (LegacySystem.GenerationsPlayed(w) < 2 || !w.Player.IsAlive) return;
        var meters = Meters(ctx);
        foreach (var t in ctx.Content.FamilyTraits.Values.OrderBy(t => t.Id, StringComparer.Ordinal))
        {
            double value = meters.GetValueOrDefault(t.Meter);
            if (!w.FamilyTraits.Contains(t.Id) && value >= t.Gain)
            {
                w.FamilyTraits.Add(t.Id);
                w.Log($"The {w.FamilyName} family has become {t.Name.ToLowerInvariant()}.", 2, "family", w.PlayerId);
                Queue(ctx, GainedEvent, t);
            }
            else if (w.FamilyTraits.Contains(t.Id) && value < t.Lose)
            {
                w.FamilyTraits.Remove(t.Id);
                w.Log($"The {w.FamilyName} family is no longer {t.Name.ToLowerInvariant()}.", 2, "family", w.PlayerId);
                Queue(ctx, LostEvent, t);
            }
        }
    }

    private static void Queue(SimContext ctx, string eventId, FamilyTraitDef t)
    {
        if (EventSystem.QueueSituation(ctx, eventId) is { } pending)
        {
            pending.Words["family_trait"] = t.Name;
            pending.Words["family_trait_text"] = eventId == GainedEvent ? t.Gained : t.Lost;
        }
    }

    /// <summary>The family's traits that count for this person (only people of the blood).</summary>
    public static IEnumerable<FamilyTraitDef> TraitsFor(SimContext ctx, Person p) =>
        p.IsBlood ? ctx.World.FamilyTraits.Select(id => ctx.Content.FamilyTraits.GetValueOrDefault(id)).OfType<FamilyTraitDef>() : Enumerable.Empty<FamilyTraitDef>();

    /// <summary>Grade factors from the family's name: readers help, a notorious name does not.</summary>
    public static IEnumerable<(string Label, double Points)> GradeFactors(SimContext ctx, Person p)
    {
        foreach (var t in TraitsFor(ctx, p))
            if (t.Meter == Learning) yield return (t.Name, 6);
            else if (t.Meter == Notoriety) yield return (t.Name, -4);
    }

    public static IEnumerable<(string Label, double Points)> PerformanceFactors(SimContext ctx, Person p)
    {
        foreach (var t in TraitsFor(ctx, p))
            if (t.Meter == Wealth) yield return (t.Name, 4);
            else if (t.Meter == Notoriety) yield return (t.Name, -5);
    }

    /// <summary>A close family lifts everyone's mood a little, and holds marriages together.</summary>
    public static double HappinessBonus(SimContext ctx, Person p) => TraitsFor(ctx, p).Any(t => t.Meter == Warmth) ? 5 : 0;

    public static double SplitFactor(SimContext ctx, Person p) => TraitsFor(ctx, p).Any(t => t.Meter == Warmth) ? 0.7 : 1;

    /// <summary>"The Berglunds: a family of readers, close knit" for the family tree.</summary>
    public static IReadOnlyList<FamilyTraitDef> Current(SimContext ctx) =>
        ctx.World.FamilyTraits.Select(id => ctx.Content.FamilyTraits.GetValueOrDefault(id)).OfType<FamilyTraitDef>().ToList();
}
