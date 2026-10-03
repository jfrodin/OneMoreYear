using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Fame (The Sims' Get Famous, docs/sims-inspiration.md): musicians, writers, artists and athletes who
/// make it become known, first in their town, then in the country. Fame raises the pay of a creative
/// career and brings its own events: fans, interviews, gossip, scandal. Without new work it fades.
/// Getting there is hard: every step up a creative track needs a high skill, and few take it.
/// </summary>
public static class FameSystem
{
    public const double Local = 15, National = 35, Famous = 60, Household = 85;

    /// <summary>The year's fame and pay for someone in a creative career; the end of a sporting one.</summary>
    public static void Update(SimContext ctx, Person p)
    {
        var occ = p.Activity == Activity.Working ? ctx.Content.Occupation(p.OccupationId) : null;
        int age = p.Age(ctx.Year);
        if (occ?.MaxAge is { } maxAge && age > maxAge)
        {
            if (p.InFamily || p.Id == ctx.World.PlayerId)
                ctx.World.Log($"{p.FirstName} retired from {occ.Name.ToLowerInvariant()} at {age}. The body had had enough.", ctx.Importance(false, p), "career", p.Id);
            CareerSystem.BecomeJobSeeker(p, ctx);
            occ = null;
        }
        var level = occ != null && p.OccupationLevel < occ.Levels.Count ? occ.Levels[p.OccupationLevel] : null;
        // Working at it every day is practice too.
        if (level?.Skill is { } practised && p.Hobby != practised && ctx.Rng.Chance(0.3)) SkillSystem.Add(p, practised, 1);
        double before = p.Fame;
        if (level is { Fame: > 0 })
        {
            double skill = level.Skill != null ? SkillSystem.Level(p, level.Skill) : 5;
            p.Fame += level.Fame * Math.Max(0, ctx.Rng.Gaussian(1, 0.5)) * (0.6 + skill * 0.06);
            p.Fame = Math.Min(100, p.Fame * 0.97);
            // Fame pays: the same title earns far more when people know your name.
            p.Income = CareerSystem.Salary(ctx, level) * (1 + p.Fame / 40);
        }
        else if (p.Fame > 0)
            p.Fame = Math.Max(0, p.Fame * 0.88 - 0.5);
        p.PeakFame = Math.Max(p.PeakFame, p.Fame);
        if (p.InFamily || p.Id == ctx.World.PlayerId)
        {
            foreach (var (mark, text) in new[] { (Local, $"is getting known in {HousingSystem.City(ctx, p).Name}"), (National, $"is known all over {ctx.Country.Name}"), (Famous, "is famous"), (Household, "is a household name") })
                if (before < mark && p.Fame >= mark)
                    ctx.World.Log($"{p.FirstName} {text}.", ctx.Importance(true, p), "career", p.Id);
        }
    }

    /// <summary>"Known in Malmö", "Famous" ..., or null for most people.</summary>
    public static string? Label(SimContext ctx, Person p) => p.Fame switch
    {
        >= Household => "A household name",
        >= Famous => "Famous",
        >= National => $"Known all over {ctx.Country.Name}",
        >= Local => $"Known in {HousingSystem.City(ctx, p).Name}",
        _ => p.PeakFame >= National && p.IsAlive ? "Once famous" : null,
    };

    /// <summary>The "fame" effect: a change, up or down.</summary>
    public static void Change(Person p, double amount)
    {
        p.Fame = Math.Clamp(p.Fame + amount, 0, 100);
        p.PeakFame = Math.Max(p.PeakFame, p.Fame);
    }
}
