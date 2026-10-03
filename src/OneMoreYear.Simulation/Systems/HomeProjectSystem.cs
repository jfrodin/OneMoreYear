using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Doing up the home (The Sims' build mode, docs/sims-inspiration.md): a new kitchen, a garden, a
/// sauna. Only for a home the household owns. It costs money, part of it comes back in the home's
/// value, and a home that has had love put into it makes the people in it a little happier every
/// year. Kitchens and bathrooms wear out. The projects are content/home_projects.json.
/// </summary>
public static class HomeProjectSystem
{
    /// <summary>Who holds the home the person lives in (themselves or their partner), if it is owned.</summary>
    public static Person? Holder(World w, Person p) =>
        !p.OwnsHome ? null : p.HomeValue > 0 ? p : w.TryGet(p.PartnerId) is { HomeValue: > 0 } partner ? partner : null;

    /// <summary>Done, and not yet worn out.</summary>
    public static bool IsDone(SimContext ctx, Person holder, HomeProjectDef def) =>
        holder.HomeProjects.TryGetValue(def.Id, out var year) && (def.Lasts == 0 || ctx.Year - year < def.Lasts);

    /// <summary>The projects that fit the person's home and life just now (done ones included).</summary>
    public static List<HomeProjectDef> Fitting(SimContext ctx, Person p)
    {
        if (Holder(ctx.World, p) is not { } holder) return new();
        bool garden = HousingSystem.HomeTypeOf(ctx, holder)?.Garden == true;
        bool children = Kinship.Children(ctx.World, p).Any(c => c.IsAlive && c.LivesWithParents && c.Age(ctx.Year) < 18);
        return ctx.Content.HomeProjects.Values
            .Where(d => d.MinYear <= ctx.Year && (d.Countries.Count == 0 || d.Countries.Contains(ctx.Country.Id))
                        && (!d.Garden || garden) && (!d.Children || children || IsDone(ctx, holder, d)))
            .OrderBy(d => d.Cost).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();
    }

    /// <summary>What it costs this person, in this year's money: handy people do some of it themselves.</summary>
    public static double Cost(SimContext ctx, Person p, HomeProjectDef def) =>
        ctx.NominalRef(def.Cost) * (1 - (def.Skill != null ? Math.Min(0.4, SkillSystem.Level(p, def.Skill) * 0.05) : 0));

    public static string Do(SimContext ctx, Person p, string id)
    {
        var w = ctx.World;
        if (!ctx.Content.HomeProjects.TryGetValue(id, out var def) || Holder(w, p) is not { } holder || !Fitting(ctx, p).Contains(def) || IsDone(ctx, holder, def))
            return "";
        double cost = Cost(ctx, p, def);
        if (p.Money < cost) return $"It would cost about {EconomySystem.Format(ctx, cost)}. You do not have it yet.";
        string text = def.Done;
        if (ctx.Rng.Chance(0.2))
        {
            cost *= 1.4;
            text = def.Overrun + " " + text;
        }
        p.Money -= cost;
        EconomySystem.Record(ctx, p, def.Name, -cost);
        holder.HomeValue += ctx.NominalRef(def.Cost) * def.Value;
        holder.HomeProjects[def.Id] = ctx.Year;
        p.Happiness = Math.Min(100, p.Happiness + def.Happiness);
        if (w.TryGet(p.PartnerId) is { IsAlive: true } partner && partner.OwnsHome) partner.Happiness = Math.Min(100, partner.Happiness + def.Happiness / 2);
        w.Log($"{p.FirstName} had {char.ToLowerInvariant(def.Name[0])}{def.Name[1..]} done at home.", 1, "home", p.Id);
        return text;
    }

    /// <summary>A home that has been looked after: a little happiness every year for those who live in it.</summary>
    public static void Update(SimContext ctx, Person p)
    {
        if (Holder(ctx.World, p) is not { } holder || holder.HomeProjects.Count == 0) return;
        int active = ctx.Content.HomeProjects.Values.Count(d => IsDone(ctx, holder, d));
        if (active > 0) p.Happiness = Math.Min(100, p.Happiness + Math.Min(1.5, active * 0.3));
    }
}
