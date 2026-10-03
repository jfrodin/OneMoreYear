using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Achievements (docs/endgame.md): what counts is the family the player has played, read from the
/// world each year. The texts live in content/achievements.json; the rules live here, keyed by id.
/// Which ones a player has unlocked is stored by the presentation layer, outside the save files.
/// </summary>
public static class AchievementSystem
{
    public const string Common = "common", Rare = "rare", Legendary = "legendary", Secret = "secret";

    // Money in reference kronor (2020), so a Swedish and an American family are measured alike.
    private const double Rich = 10_000_000, VeryRich = 40_000_000;

    private static IEnumerable<Person> Played(SimContext ctx) => ctx.World.PlayedIds.Select(ctx.World.Get);

    private static int GenerationsPlayed(SimContext ctx)
    {
        var played = Played(ctx).ToList();
        return played.Count == 0 ? 0 : played.Max(p => p.Generation) - played.Min(p => p.Generation) + 1;
    }

    private static int AgeReached(SimContext ctx, Person p) => (p.DeathYear ?? ctx.Year) - p.BirthYear;

    private static bool AffairBy(SimContext ctx, Person p) => ctx.World.Secrets.Any(s => s.Kind == "affair" && s.SubjectId == p.Id);

    /// <summary>The rules, by achievement id.</summary>
    private static readonly Dictionary<string, Func<SimContext, bool>> Checks = new()
    {
        // Common: most families get these.
        ["married"] = ctx => Played(ctx).Any(p => p.Memories.Any(m => m.Kind == "wedding")),
        ["graduate"] = ctx => Played(ctx).Any(p => p.Education == EducationLevel.University),
        ["home_owner"] = ctx => Played(ctx).Any(p => p.IsAlive && p.OwnsHome),
        ["grandparent"] = ctx => Played(ctx).Any(p => Kinship.Children(ctx.World, p).Any(c => c.ChildIds.Count > 0)),
        ["next_generation"] = ctx => ctx.World.PlayedIds.Count >= 2,
        ["centenarian"] = ctx => Played(ctx).Any(p => AgeReached(ctx, p) >= 100),
        ["emigrant"] = ctx => Played(ctx).Any(p => p.Memories.Any(m => m.Kind == "emigrated")),
        ["dreamer"] = ctx => ctx.World.Feats.Contains("dream"),

        // Rare: they take intent, or luck.
        ["five_generations"] = ctx => LongestLivingLine(ctx) >= 5,
        ["three_doctors"] = ctx => ctx.World.People.Any(p => Doctors(ctx, p, 3)),
        ["debt_to_riches"] = ctx => Played(ctx).Any(p => p.LowRefWorth <= -100_000 && p.PeakRefWorth >= Rich),
        ["no_divorce"] = ctx => GenerationsPlayed(ctx) >= 4 && Played(ctx).All(p => !p.Flags.Contains("divorced")),
        ["hundred_years"] = ctx => !ctx.World.GameOver && ctx.Year - ctx.World.StartYear >= 100,
        ["golden_wedding"] = ctx => Played(ctx).Any(p => p.IsAlive && p.PartnerStatus == PartnerStatus.Married && ctx.Year - p.PartnerSinceYear >= 50),
        ["full_circle"] = ctx => ctx.World.Feats.Contains("full_circle"),
        ["inherited_dream"] = ctx => ctx.World.Feats.Contains("inherited_dream"),
        ["head_of_government"] = ctx => Played(ctx).Any(p => p.Flags.Contains("top:politics")),
        ["household_name"] = ctx => Played(ctx).Any(p => p.PeakFame >= FameSystem.Household),
        ["famous_family"] = ctx => Played(ctx).Count(p => p.PeakFame >= FameSystem.National) >= 3,
        ["wish_a_year"] = ctx => Played(ctx).Any(p => p.WishesKept >= 40),
        ["century_heirloom"] = ctx => ctx.World.Heirlooms.Any(h => h.OwnerId == ctx.World.PlayerId && !h.Interrupted && ctx.Year - Math.Max(h.SinceYear, ctx.World.StartYear) >= 100),

        // Legendary: very hard, never impossible.
        ["ten_generations"] = ctx => GenerationsPlayed(ctx) >= 10,
        ["dynasty"] = ctx => ctx.World.PlayedIds.Count > 0 && LivingDescendants(ctx, ctx.World.Get(ctx.World.PlayedIds[0])) >= 50,
        ["from_nothing"] = ctx => ctx.World.StartConditions == StartChoices.Hard && Played(ctx).Any(p => p.PeakRefWorth >= VeryRich),
        ["clean_record"] = ctx => GenerationsPlayed(ctx) >= 7
                                  && Played(ctx).All(p => p.CriminalRecord.Count == 0 && !p.Flags.Contains("divorced") && !AffairBy(ctx, p)),
        ["two_centuries"] = ctx => !ctx.World.GameOver && ctx.Year - ctx.World.StartYear >= 200,

        // Secret: not shown until they happen.
        ["the_return"] = ctx => Played(ctx).Any(p => p.Flags.Contains("came_home")),
        ["full_table"] = ctx => Played(ctx).Any(p => p.ChildIds.Count >= 7),
        ["survivor"] = ctx => Played(ctx).Any(p =>
        {
            var kids = Kinship.Children(ctx.World, p).ToList();
            return kids.Count >= 3 && kids.All(k => !k.IsAlive && k.DeathYear <= (p.DeathYear ?? ctx.Year));
        }),
        ["cell_to_corner_office"] = ctx => Played(ctx).Any(p => p.CriminalRecord.Any(r => r.Sentence.Contains("prison")) && p.Activity == Activity.Working
            && ctx.Content.Occupation(p.OccupationId) is { } occ && occ.Id != "crime" && p.OccupationLevel >= occ.Levels.Count - 1),
        ["one_hundred_ten"] = ctx => Played(ctx).Any(p => AgeReached(ctx, p) >= 110),
        ["myth_true"] = ctx => ctx.World.Feats.Contains("myth_true"),
        ["family_book"] = ctx => ctx.World.Feats.Contains("family_book"),
        ["bought_back"] = ctx => ctx.World.Feats.Contains("bought_back"),
        ["published"] = ctx => ctx.World.Feats.Contains("published_book"),
        ["family_firm"] = ctx => ctx.World.Businesses.Any(b => b.IsOpen && b.Owners.Distinct().Count() >= 3 && b.Owners.Count(ctx.World.PlayedIds.Contains) >= 2),
    };

    public static IEnumerable<string> RuleIds => Checks.Keys;

    /// <summary>The achievements this world has earned that are not in <paramref name="already"/>.</summary>
    public static List<AchievementDef> NewlyEarned(SimContext ctx, IReadOnlySet<string> already) =>
        ctx.Content.Achievements.Where(a => !already.Contains(a.Id) && Checks.TryGetValue(a.Id, out var check) && check(ctx)).ToList();

    /// <summary>The longest chain of living people where each is the child of the next: five means five generations alive.</summary>
    private static int LongestLivingLine(SimContext ctx)
    {
        var w = ctx.World;
        var depth = new Dictionary<int, int>();
        int Depth(Person p)
        {
            if (depth.TryGetValue(p.Id, out var d)) return d;
            d = 1 + p.ParentIds.Select(w.Get).Where(x => x.IsAlive).Select(Depth).DefaultIfEmpty(0).Max();
            depth[p.Id] = d;
            return d;
        }
        return w.People.Where(p => p.IsAlive && (p.InFamily || p.IsBlood)).Select(Depth).DefaultIfEmpty(0).Max();
    }

    /// <summary>A doctor whose parent was a doctor, whose parent was a doctor ...</summary>
    private static bool Doctors(SimContext ctx, Person p, int inARow)
    {
        if (!p.Jobs.Contains("medicine")) return false;
        if (inARow <= 1) return true;
        return p.ParentIds.Select(ctx.World.Get).Any(parent => Doctors(ctx, parent, inARow - 1));
    }

    private static int LivingDescendants(SimContext ctx, Person founder)
    {
        var w = ctx.World;
        var seen = new HashSet<int>();
        var queue = new Queue<int>(founder.ChildIds);
        int living = 0;
        while (queue.Count > 0)
        {
            int id = queue.Dequeue();
            if (!seen.Add(id)) continue;
            var p = w.Get(id);
            if (p.IsAlive) living++;
            foreach (var c in p.ChildIds) queue.Enqueue(c);
        }
        return living;
    }
}
