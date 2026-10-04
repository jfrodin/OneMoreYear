using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// The few things a player may choose about the first life (docs/design-decisions.md, "Choosing who
/// you are"): the family's circumstances, which set how easy the start is. Left to chance, the family
/// is whatever the seed gives – the way the game is meant to be played.
/// </summary>
public static class StartChoices
{
    public const string Comfortable = "comfortable", Ordinary = "ordinary", Hard = "hard";

    /// <summary>The choices offered, in order, with how they change the difficulty.</summary>
    public static readonly IReadOnlyList<(string Id, string Name, string Description)> Conditions = new[]
    {
        (Comfortable, "Comfortable (easy)", "Parents with money, steady jobs and a home of their own."),
        (Ordinary, "Ordinary (medium)", "An everyday family: work, a modest home, no big troubles to begin with."),
        (Hard, "A hard start (hard)", "Debt, a rented flat, a parent out of work, and maybe a bottle in the cupboard."),
    };

    // Traits that make a childhood harder; a comfortable start leaves them out of the parents.
    private static readonly string[] HardTraits = { "addictive", "cruel", "hot_tempered" };

    /// <summary>Applied right after the starting family is created.</summary>
    public static void Apply(SimContext ctx, string? conditions)
    {
        var w = ctx.World;
        if (conditions is not (Comfortable or Ordinary or Hard)) return;
        w.StartConditions = conditions;
        var parents = Kinship.Parents(w, w.Player).ToList();
        var father = parents.FirstOrDefault(p => p.Sex == Sex.Male);
        var mother = parents.FirstOrDefault(p => p.Sex == Sex.Female);

        foreach (var p in parents)
        {
            switch (conditions)
            {
                case Comfortable:
                    p.Addiction = null;
                    p.Traits.RemoveAll(t => HardTraits.Contains(t));
                    Tweak(ctx, p, new ScenarioTweak { Money = 400000 });
                    if (p.Activity == Activity.Unemployed) CareerSystem.Hire(ctx, p);
                    break;
                case Ordinary:
                    p.Addiction = null;
                    p.Money = Math.Clamp(p.Money, ctx.NominalRef(10000), ctx.NominalRef(150000));
                    if (p.Activity == Activity.Unemployed) CareerSystem.Hire(ctx, p);
                    break;
                case Hard:
                    // The home goes first, so what it sold for does not end up as savings.
                    Tweak(ctx, p, new ScenarioTweak { OwnsHome = false });
                    Tweak(ctx, p, new ScenarioTweak { Money = -60000 });
                    break;
            }
        }
        // One home for the family: the father's, or the mother's if there is no father.
        if (conditions == Comfortable) Tweak(ctx, father ?? mother, new ScenarioTweak { OwnsHome = true });
        if (conditions == Hard)
        {
            Tweak(ctx, father ?? mother, new ScenarioTweak { Unemployed = true });
            if (father != null && ctx.Happens(ContentCategories.Addiction) && ctx.Rng.Chance(0.5))
                Tweak(ctx, father, new ScenarioTweak { Addiction = "alcohol" });
        }
    }

    /// <summary>Changes a person's traits, attributes, money, home, job or addiction.</summary>
    public static void Tweak(SimContext ctx, Person? p, ScenarioTweak? t)
    {
        if (p == null || t == null) return;
        foreach (var trait in t.Traits ?? new())
        {
            if (ctx.Content.Traits.TryGetValue(trait, out var def) && def.Opposite != null) p.Traits.Remove(def.Opposite);
            p.Traits.RemoveAll(x => ctx.Content.Traits.TryGetValue(x, out var d) && d.Opposite == trait);
            PersonFactory.TryAddTrait(ctx, p, trait);
        }
        if (t.Smarts is { } smarts) p.Smarts = smarts;
        if (t.Looks is { } looks) p.Looks = looks;
        if (t.Fitness is { } fitness) p.Fitness = fitness;
        if (!string.IsNullOrWhiteSpace(t.FirstName)) p.FirstName = t.FirstName.Trim();
        if (t.Health is { } health) p.Health = health;
        if (t.Happiness is { } happiness) p.Happiness = happiness;
        if (t.Occupation != null && p.Age(ctx.Year) >= 16) { p.ProgrammeId = null; p.StudyingFor = null; if (!CareerSystem.Hire(ctx, p, t.Occupation)) CareerSystem.Hire(ctx, p, t.Occupation, 0); }
        if (t.Ailment != null) AilmentSystem.Begin(ctx, p, t.Ailment);
        if (t.Money is { } money) p.Money = ctx.NominalRef(money);
        if (t.OwnsHome == true && p.HomeValue <= 0) EconomySystem.GiveHome(p, HousingSystem.HomePrice(ctx, p), HousingSystem.HomePrice(ctx, p) * 0.3);
        if (t.OwnsHome == false && p.HomeValue > 0) EconomySystem.SellHome(ctx, p, log: false);
        if (t.Unemployed == true && p.Age(ctx.Year) >= 16) CareerSystem.BecomeJobSeeker(p, ctx);
        if (t.Addiction != null)
        {
            p.Addiction = t.Addiction;
            p.AddictionSince = ctx.Year;
        }
    }
}
