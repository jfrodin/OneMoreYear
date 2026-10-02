using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// The year's wish (The Sims' wants, docs/sims-inspiration.md): every year the player wants one small
/// thing that fits the life just now, like calling a parent, getting in shape or finding a job. It is
/// kept by an action or by getting there before the year is over, and gives a little happiness.
/// Not a quest: nothing happens if it is not kept. The wishes are content/wishes.json.
/// </summary>
public static class WishSystem
{
    public static WishDef? Of(SimContext ctx) => ctx.World.Wish is { } w ? ctx.Content.Wishes.GetValueOrDefault(w.Id) : null;

    /// <summary>Has the player done what this year's wish asks?</summary>
    public static bool Kept(SimContext ctx)
    {
        var w = ctx.World;
        if (w.Wish is not { } wish || Of(ctx) is not { } def || !w.Player.IsAlive) return false;
        if (wish.TargetId is { } tid && !w.Get(tid).IsAlive) return false;
        if (def.Done != null) return EventSystem.Matches(ctx, def.Done, w.Player, w.Player);
        string who = wish.TargetId?.ToString() ?? "self";
        return def.Actions.Any(a => w.ActionsThisYear.Contains($"{a}:{who}"));
    }

    /// <summary>The wish as the side panel shows it, or null.</summary>
    public static string? Describe(SimContext ctx)
    {
        if (ctx.World.Wish is not { } wish || Of(ctx) is not { } def) return null;
        return TextFormatter.Format(ctx, def.Text, Probe(wish));
    }

    /// <summary>At the end of the year: a kept wish gives its happiness and a line in the chronicle.</summary>
    public static void EndOfYear(SimContext ctx)
    {
        var w = ctx.World;
        var p = w.Player;
        if (Of(ctx) is { } def && w.Wish is { } wish && Kept(ctx))
        {
            p.Happiness = Math.Min(100, p.Happiness + def.Happiness);
            p.WishesKept++;
            string text = TextFormatter.Format(ctx, def.Text, Probe(wish));
            w.Log($"{p.FirstName} wanted to {char.ToLowerInvariant(text[0])}{text[1..].TrimEnd('.')} this year, and did.", 1, "wish", p.Id);
        }
        w.Wish = null;
    }

    /// <summary>
    /// Picks a wish for the year: one that fits the player, is not already true, and can be done
    /// (available tells whether an action can be taken, with a target or alone).
    /// </summary>
    public static void Pick(SimContext ctx, Func<string, int?, bool> available)
    {
        var w = ctx.World;
        var p = w.Player;
        string? last = w.Wish?.Id;
        w.Wish = null;
        if (!p.IsAlive || p.Age(ctx.Year) < 6 || p.Activity == Activity.Prison) return;

        var distances = Kinship.Distances(w, p, 3);
        var circle = Kinship.Circle(w, p, false);
        var options = new List<(WishDef Def, int? Target)>();
        foreach (var def in ctx.Content.Wishes.Values.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            if (def.Id == last || (def.Countries.Count > 0 && !def.Countries.Contains(ctx.Country.Id))) continue;
            if (!EventSystem.Matches(ctx, def.Conditions, p, p)) continue;
            if (def.Done != null && EventSystem.Matches(ctx, def.Done, p, p)) continue;
            if (def.Target == null)
            {
                if (def.Done != null || def.Actions.Any(a => available(a, null))) options.Add((def, null));
                continue;
            }
            var people = circle.Where(o => Kinship.MatchesRole(w, p, o, def.Target.Role, distances)
                                           && EventSystem.Matches(ctx, def.Target.Conditions, o, p)
                                           && def.Actions.Any(a => available(a, o.Id))).ToList();
            if (people.Count > 0) options.Add((def, people[ctx.Rng.Next(people.Count)].Id));
        }
        if (options.Count == 0) return;
        var (chosen, target) = ctx.Rng.PickWeighted(options, o => o.Def.Weight);
        w.Wish = new Wish { Id = chosen.Id, TargetId = target, Year = ctx.Year };
    }

    private static PendingEvent Probe(Wish wish)
    {
        var probe = new PendingEvent { EventId = wish.Id };
        if (wish.TargetId is { } t) probe.Roles["target"] = t;
        return probe;
    }
}
