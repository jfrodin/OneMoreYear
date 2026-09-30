using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Lasting health states (content/ailments.json): depression, anxiety, burnout, trauma, dementia.
/// They start from what happens in a life – low mood, losses, stress, personality, age – cost
/// happiness, health and performance every year, and pass with a chance that treatment raises.
/// Mental illness follows the content settings; dementia is part of growing old.
/// </summary>
public static class AilmentSystem
{
    public const string TreatedPrefix = "treated_";

    public static void Update(SimContext ctx)
    {
        var w = ctx.World;
        int count = w.People.Count;
        for (int i = 0; i < count; i++)
        {
            var p = w.People[i];
            if (!p.IsAlive || !(p.InFamily || p.Id == w.PlayerId)) continue;
            foreach (var id in p.Ailments.Keys.ToList()) Live(ctx, p, id);
            Onset(ctx, p);
        }
    }

    public static bool Allowed(SimContext ctx, Person p, AilmentDef def) =>
        def.Content == null || (p.Id == ctx.World.PlayerId ? ctx.Shown(def.Content) : ctx.Happens(def.Content));

    /// <summary>A year with the ailment: its cost, and maybe recovery.</summary>
    private static void Live(SimContext ctx, Person p, string id)
    {
        if (!ctx.Content.Ailments.TryGetValue(id, out var def)) { p.Ailments.Remove(id); return; }
        p.Happiness = Math.Clamp(p.Happiness + def.Happiness, 0, 100);
        p.Health = Math.Clamp(p.Health + def.Health, 1, 100);
        p.Performance = Math.Clamp(p.Performance + def.Performance, 0, 100);
        bool treated = p.Flags.Contains(TreatedPrefix + id);
        double chance = (treated ? def.TreatedRecovery : def.Recovery) + ctx.Mod(p, "resilience") * 0.05;
        if (ctx.Year - p.Ailments[id] >= 1 && chance > 0 && ctx.Rng.Chance(chance)) Recover(ctx, p, id);
    }

    public static void Recover(SimContext ctx, Person p, string id)
    {
        if (!p.Ailments.Remove(id)) return;
        p.Flags.Remove(TreatedPrefix + id);
        var def = ctx.Content.Ailments[id];
        p.Happiness = Math.Min(100, p.Happiness + 10);
        if (p.InFamily || p.Id == ctx.World.PlayerId)
            ctx.World.Log($"{p.FirstName} came through {def.Name.ToLowerInvariant()}.", ctx.Importance(false, p), "health", p.Id);
    }

    /// <summary>Starts an ailment. For the player a situation follows, so they can choose what to do.</summary>
    public static bool Begin(SimContext ctx, Person p, string id)
    {
        if (p.Ailments.ContainsKey(id) || !ctx.Content.Ailments.TryGetValue(id, out var def) || !Allowed(ctx, p, def)) return false;
        p.Ailments[id] = ctx.Year;
        if (p.InFamily || p.Id == ctx.World.PlayerId)
            ctx.World.Log(id == "dementia" ? $"{p.FirstName} started to forget things." : $"{p.FirstName} was struck by {def.Name.ToLowerInvariant()}.",
                ctx.Importance(false, p), "health", p.Id);
        if (p.Id == ctx.World.PlayerId) EventSystem.QueueSituation(ctx, $"ailment_{id}");
        return true;
    }

    private static void Onset(SimContext ctx, Person p)
    {
        var rng = ctx.Rng;
        int age = p.Age(ctx.Year);
        if (age >= 65 && !p.Ailments.ContainsKey("dementia") && rng.Chance(Math.Max(0, (age - 65) * 0.0016)))
            Begin(ctx, p, "dementia");
        if (age < 13) return;

        // Heavy memories from the last two years (a death, a betrayal, violence) weigh on the mind.
        double recentPain = p.Memories.Where(m => m.Impact < 0 && ctx.Year - m.Year <= 2).Sum(m => -m.Impact * m.Strength);
        double low = p.Happiness < 30 ? 0.04 : p.Happiness < 45 ? 0.01 : 0;
        double gloom = -Math.Min(0, ctx.Mod(p, "happiness")) * 0.002;
        if (rng.Chance(0.003 + low + gloom + Math.Min(0.05, recentPain / 4000))) Begin(ctx, p, "depression");
        if ((p.HasTrait("paranoid") || p.HasTrait("hypochondriac") || p.HasTrait("conflict_averse")) && rng.Chance(0.012)) Begin(ctx, p, "anxiety");
        else if (rng.Chance(0.002)) Begin(ctx, p, "anxiety");

        if (p.Activity == Activity.Working && age is >= 28 and <= 62)
        {
            int kids = Kinship.Children(ctx.World, p).Count(k => k.IsAlive && k.Age(ctx.Year) < 12);
            double burnout = 0.003 + (p.HasTrait("ambitious") ? 0.01 : 0) + (p.Performance > 75 ? 0.008 : 0) + kids * 0.003;
            if (rng.Chance(burnout)) Begin(ctx, p, "burnout");
        }
    }

    /// <summary>After something terrible (violence, abuse), trauma may follow.</summary>
    public static void MaybeTrauma(SimContext ctx, Person p, double chance)
    {
        if (ctx.Rng.Chance(Math.Clamp(chance - ctx.Mod(p, "resilience") * 0.15, 0, 1))) Begin(ctx, p, "trauma");
    }

    /// <summary>"Depression · Dementia" – for the person page.</summary>
    public static string? Describe(SimContext ctx, Person p) =>
        p.Ailments.Count == 0 ? null
            : string.Join("  ·  ", p.Ailments.Keys.Select(id => ctx.Content.Ailments.GetValueOrDefault(id)?.Name ?? id));
}
