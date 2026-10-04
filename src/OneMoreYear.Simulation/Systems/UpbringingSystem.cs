using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Raising children (docs/sims-inspiration.md): every child at home is raised in some way each year,
/// warm, strict, free or distant. The player chooses for their own children; other parents raise theirs
/// as their personalities make them. It builds three hidden values (empathy, responsibility, self
/// control) that decide, at eighteen, what kind of adult the child becomes.
/// </summary>
public static class UpbringingSystem
{
    public const string Warm = "warm", Strict = "strict", Free = "free", Distant = "distant";
    public static readonly string[] Styles = { Warm, Strict, Free, Distant };

    /// <summary>A childhood in a family worth this much (reference kronor) is a rich one.</summary>
    public const double RichChildhood = 8_000_000;
    public const string GrewUpRich = "grew_up_rich";

    public static string Describe(string style) => style switch
    {
        Warm => "Warm: time, hugs and patience. Children grow kind and close to you.",
        Strict => "Strict: rules, chores and consequences. Children grow responsible, and a little afraid. In a rich home, it is what keeps them from being spoiled.",
        Free => "Free: few rules, a lot of trust. Children grow independent, and sometimes wild.",
        _ => "Distant: busy, tired or elsewhere. Children grow up anyway, mostly on their own.",
    };

    /// <summary>The way a parent raises children when nobody chooses: from their personality.</summary>
    public static string NaturalStyle(SimContext ctx, Person parent)
    {
        if (parent.Addiction != null || parent.HasTrait("cruel")) return Distant;
        if (parent.HasTrait("devoted_parent") || parent.HasTrait("kind")) return Warm;
        if (parent.HasTrait("hot_tempered") || parent.HasTrait("ambitious")) return Strict;
        if (parent.HasTrait("lazy") || parent.HasTrait("impulsive") || parent.HasTrait("eccentric")) return Free;
        return parent.Id % 2 == 0 ? Warm : Strict;
    }

    /// <summary>How this child is raised this year: the player's choice, or the parents' nature.</summary>
    public static string StyleFor(SimContext ctx, Person child)
    {
        var w = ctx.World;
        var parents = Kinship.Parents(w, child).Where(p => p.IsAlive && p.Abroad == child.Abroad).ToList();
        if (parents.Any(p => p.Id == w.PlayerId) && child.Upbringing != null) return child.Upbringing;
        var raiser = parents.FirstOrDefault(p => p.Id == w.PlayerId) ?? parents.OrderByDescending(p => p.Sex == Sex.Female).FirstOrDefault();
        return raiser == null ? Distant : NaturalStyle(ctx, raiser);
    }

    /// <summary>A year of childhood: the values move a little, and the bond with the parents with them.</summary>
    public static void Update(SimContext ctx, Person child)
    {
        int age = child.Age(ctx.Year);
        if (age < 1 || age >= ctx.Country.AdultAge || !child.IsAlive) return;
        var rng = ctx.Rng;
        string style = StyleFor(ctx, child);
        (double empathy, double responsibility, double control, double closeness) = style switch
        {
            Warm => (2.5, 0.8, 0.8, 2.0),
            Strict => (-0.3, 2.5, 2.2, -0.8),
            Free => (0.8, -0.5, -1.2, 1.0),
            _ => (-1.5, -1.0, -1.2, -1.5),
        };
        // Growing up with everything: unless someone is strict about it, duty and self control slip.
        double familyWorth = Kinship.Parents(ctx.World, child).Where(p => p.IsAlive).Sum(p => ctx.Real(EconomySystem.NetWorth(ctx, p))) / ctx.Country.ContentMoneyScale;
        if (familyWorth >= RichChildhood)
        {
            child.Flags.Add(GrewUpRich);
            if (style != Strict)
            {
                responsibility -= 1.1;
                control -= 0.6;
            }
        }
        child.Empathy = Math.Clamp(child.Empathy + empathy + rng.Gaussian(0, 1), -50, 50);
        child.Responsibility = Math.Clamp(child.Responsibility + responsibility + rng.Gaussian(0, 1), -50, 50);
        child.SelfControl = Math.Clamp(child.SelfControl + control + rng.Gaussian(0, 1), -50, 50);
        foreach (var parent in Kinship.Parents(ctx.World, child).Where(p => p.IsAlive))
        {
            var rel = ctx.World.Rel(child.Id, parent.Id);
            rel[RelDim.Closeness] += closeness;
            if (style == Strict) rel[RelDim.Fear] += 0.5;
        }
    }

    /// <summary>
    /// At eighteen the values become who the person is: a warm childhood leaves kindness, a strict one
    /// responsibility, a free one independence, a distant one scars.
    /// </summary>
    public static void BecomeAdult(SimContext ctx, Person p)
    {
        var rng = ctx.Rng;
        void Maybe(string trait, double value, double threshold)
        {
            if (Math.Abs(value) >= threshold && rng.Chance(Math.Min(0.6, Math.Abs(value) / 80)))
                PersonFactory.TryAddTrait(ctx, p, trait);
        }
        if (p.Empathy > 0) Maybe("kind", p.Empathy, 15); else Maybe("cruel", p.Empathy * 0.5, 20);
        if (p.Responsibility > 0) Maybe("loyal", p.Responsibility, 18); else Maybe("lazy", p.Responsibility, 15);
        if (p.SelfControl > 0) Maybe("resilient", p.SelfControl, 18); else Maybe("impulsive", p.SelfControl, 15);
        if (p.SelfControl < -25) Maybe("addictive", p.SelfControl * 0.5, 15);
        if (p.Responsibility > 25) Maybe("ambitious", p.Responsibility, 25);
        // A rich childhood without limits leaves its own marks.
        if (p.Flags.Contains(GrewUpRich) && p.Responsibility < 5)
        {
            if (rng.Chance(0.3)) PersonFactory.TryAddTrait(ctx, p, "vain");
            if (rng.Chance(0.2)) PersonFactory.TryAddTrait(ctx, p, "greedy");
        }
    }

    /// <summary>A short line for the child's page: what the upbringing is doing to them so far.</summary>
    public static string? Shaping(Person child)
    {
        var parts = new List<string>();
        if (child.Empathy >= 10) parts.Add("caring");
        else if (child.Empathy <= -10) parts.Add("cold");
        if (child.Responsibility >= 10) parts.Add("dutiful");
        else if (child.Responsibility <= -10) parts.Add("careless");
        if (child.Flags.Contains(GrewUpRich) && child.Responsibility < 0) parts.Add("spoiled");
        if (child.SelfControl >= 10) parts.Add("steady");
        else if (child.SelfControl <= -10) parts.Add("wild");
        return parts.Count == 0 ? null : "Growing up " + string.Join(", ", parts);
    }
}
