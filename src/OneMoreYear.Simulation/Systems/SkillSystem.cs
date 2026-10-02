using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Hobbies and skills (docs/sims-inspiration.md): a person spends free time on one hobby, which builds
/// a skill from 0 to 10 over the years. Skills improve the chances in events that call for them, open
/// events of their own, and pass a little down: a child who grows up with a painter may pick up a brush.
/// </summary>
public static class SkillSystem
{
    public const int Max = 10;

    public static int Level(Person p, string skill) => p.Skills.GetValueOrDefault(skill);

    public static void TakeUp(SimContext ctx, Person p, string hobby)
    {
        if (!ctx.Content.Hobbies.ContainsKey(hobby)) return;
        p.Hobby = hobby;
        if (!p.Skills.ContainsKey(hobby)) p.Skills[hobby] = 0;
    }

    public static void Add(Person p, string skill, int amount) =>
        p.Skills[skill] = Math.Clamp(Level(p, skill) + amount, 0, Max);

    public static void Update(SimContext ctx, Person p)
    {
        int age = p.Age(ctx.Year);
        var rng = ctx.Rng;
        if (p.Hobby is { } hobby && ctx.Content.Hobbies.TryGetValue(hobby, out var def))
        {
            int level = Level(p, hobby);
            // Early levels come quickly, the last ones take years.
            double chance = 0.7 - level * 0.05 + (p.Smarts - 50) / 200 + (def.Traits.Any(p.HasTrait) ? 0.1 : 0);
            if (level < Max && rng.Chance(Math.Clamp(chance, 0.1, 0.9))) Add(p, hobby, 1);
            p.Happiness = Math.Min(100, p.Happiness + 1);
            // A long held hobby can fade in old age or a hard life.
            if (age > 80 && rng.Chance(0.05)) p.Hobby = null;
            return;
        }
        // Children pick up what they see at home; adults sometimes find something on their own.
        if (age >= 9 && age <= 14 && rng.Chance(0.15))
        {
            var fromParent = Kinship.Parents(ctx.World, p).Where(x => x.IsAlive && x.Hobby != null && Level(x, x.Hobby) >= 5)
                .Select(x => x.Hobby!).FirstOrDefault();
            if (fromParent != null) { TakeUp(ctx, p, fromParent); return; }
        }
        if (age >= 12 && p.Id != ctx.World.PlayerId && rng.Chance(0.06))
        {
            var options = ctx.Content.Hobbies.Values.Where(h => h.MinYear <= ctx.Year).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
            var pick = rng.PickWeighted(options, h => h.Traits.Any(p.HasTrait) ? 3 : 1);
            if (pick != null) TakeUp(ctx, p, pick.Id);
        }
    }

    /// <summary>"Painting 6": the hobby and how far along it is.</summary>
    public static string? Describe(SimContext ctx, Person p) =>
        p.Hobby is { } h && ctx.Content.Hobbies.TryGetValue(h, out var def) ? $"{def.Name} {Level(p, h)} of {Max}" : null;

    /// <summary>The skill words for a level: "beginner" to "master".</summary>
    public static string Rank(int level) => level switch
    {
        <= 1 => "beginner",
        <= 3 => "learning",
        <= 5 => "good",
        <= 7 => "very good",
        <= 9 => "excellent",
        _ => "a master",
    };
}
