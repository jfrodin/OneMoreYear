using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Names that fit the person: common the year they were born, in their family's heritage
/// (content/names). A Karl born in 1920, a Jimmy in 1978, a Selma in 2015 – and a Fatima when her
/// parents came from the Middle East.
/// </summary>
public static class Names
{
    /// <summary>The country's majority heritage: the first one in its names file ("swedish" in Sweden).</summary>
    public static string Default(SimContext ctx) => For(ctx)?.Heritages.FirstOrDefault()?.Id ?? "swedish";

    private static NamesDef? For(SimContext ctx) => ctx.Content.Names.GetValueOrDefault(ctx.Country.Id);

    public static HeritageDef? Heritage(SimContext ctx, string id) => For(ctx)?.Heritages.FirstOrDefault(h => h.Id == id);

    /// <summary>A heritage for someone with no parents in the world, as common as it was this year.</summary>
    public static string PickHeritage(SimContext ctx)
    {
        var names = For(ctx);
        if (names == null || names.Heritages.Count == 0) return Default(ctx);
        return ctx.Rng.PickWeighted(names.Heritages, h => SimContext.Interpolate(h.Share, ctx.Year, 0))?.Id ?? Default(ctx);
    }

    /// <summary>A child takes the heritage of one of its (biological) parents.</summary>
    public static string Inherit(SimContext ctx, IReadOnlyList<Person> parents)
    {
        var known = parents.Select(p => string.IsNullOrEmpty(p.Heritage) ? Default(ctx) : p.Heritage).ToList();
        if (known.Count == 0) return PickHeritage(ctx);
        return known.Distinct().Count() == 1 ? known[0] : ctx.Rng.Pick(known);
    }

    public static string LastName(SimContext ctx, string heritage) =>
        Heritage(ctx, heritage) is { LastNames.Count: > 0 } h ? ctx.Rng.Pick(h.LastNames) : ctx.Rng.Pick(ctx.Country.LastNames);

    /// <summary>A first name from the person's era and heritage that no living family member already has.</summary>
    public static string FirstName(SimContext ctx, Sex sex, string heritage, int birthYear)
    {
        string s = sex == Sex.Male ? "male" : "female";
        var groups = For(ctx)?.Groups.Where(g => g.Sex == s && birthYear >= g.From && birthYear <= g.To).ToList() ?? new();
        var pool = groups.Where(g => g.Heritage == heritage).SelectMany(g => g.Names).ToList();
        if (pool.Count == 0) pool = groups.Where(g => g.Heritage == Default(ctx)).SelectMany(g => g.Names).ToList();
        if (pool.Count == 0) pool = sex == Sex.Male ? ctx.Country.MaleNames : ctx.Country.FemaleNames;

        var taken = ctx.World.People.Where(p => p.IsAlive && p.InFamily).Select(p => p.FirstName).ToHashSet();
        for (int i = 0; i < 12; i++)
        {
            var name = ctx.Rng.Pick(pool);
            if (!taken.Contains(name)) return name;
        }
        return ctx.Rng.Pick(pool);
    }
}
