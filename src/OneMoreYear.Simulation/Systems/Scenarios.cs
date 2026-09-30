using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Test scenarios (content/scenarios.json): tweaks the starting family, then plays the early years
/// automatically so the playtester starts at the interesting point. See docs/test-scenarios.md.
/// </summary>
public static class Scenarios
{
    private const string GrandfatherFlag = "scenario_grandfather";

    /// <summary>Applied right after the starting family is created.</summary>
    public static void ApplyFamily(SimContext ctx, ScenarioDef s)
    {
        var w = ctx.World;
        var player = w.Player;
        var parents = Kinship.Parents(w, player).ToList();
        var father = parents.FirstOrDefault(p => p.Sex == Sex.Male);
        var mother = parents.FirstOrDefault(p => p.Sex == Sex.Female);
        // The mother's father if he lives, otherwise any living grandfather.
        var grandfather = (mother == null ? null : Kinship.Parents(w, mother).FirstOrDefault(p => p.Sex == Sex.Male && p.IsAlive))
                          ?? Kinship.Grandparents(w, player).FirstOrDefault(p => p.Sex == Sex.Male && p.IsAlive);

        // The player's own money and home are set when the scenario hands over (see FastForward).
        Apply(ctx, player, s.Player is { } pt ? new ScenarioTweak { Traits = pt.Traits, Smarts = pt.Smarts, Looks = pt.Looks, Fitness = pt.Fitness, Addiction = pt.Addiction } : null);
        foreach (var p in parents) Apply(ctx, p, s.Parents);
        Apply(ctx, father, s.Father);
        Apply(ctx, mother, s.Mother);
        Apply(ctx, grandfather, s.Grandfather);
        grandfather?.Flags.Add(GrandfatherFlag);
    }

    private static void Apply(SimContext ctx, Person? p, ScenarioTweak? t)
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
        if (t.Money is { } money) p.Money = ctx.Nominal(money);
        if (t.OwnsHome is { } owns) p.OwnsHome = owns;
        if (t.Unemployed == true && p.Age(ctx.Year) >= 16) CareerSystem.BecomeJobSeeker(p, ctx);
        if (t.Addiction != null)
        {
            p.Addiction = t.Addiction;
            p.AddictionSince = ctx.Year;
        }
    }

    /// <summary>Plays the years before the scenario's start age, then hands over and starts its storyline.</summary>
    public static void FastForward(GameSession session, ScenarioDef s)
    {
        var bot = new AutoPlayer(session.World.Seed, useActions: false);
        int guard = 0;
        while (session.Player.Age(session.Year) < s.Age && !session.GameOver && guard++ < 150)
            bot.PlayYear(session);
        if (s.Player is { } pt) Apply(session.Ctx, session.Player, new ScenarioTweak { Money = pt.Money, OwnsHome = pt.OwnsHome, Unemployed = pt.Unemployed });

        var ctx = session.Ctx;
        var child = session.Player;
        var grandfather = ctx.World.People.FirstOrDefault(p => p.Flags.Contains(GrandfatherFlag) && p.IsAlive);
        if (s.PlayAs == "grandfather" && grandfather != null)
        {
            session.TakeOver(grandfather);
            EventSystem.GenerateRandomEvents(ctx);
        }
        if (s.Storyline == "abuse_past" && grandfather != null) AbusePast(ctx, grandfather, child);
    }

    /// <summary>
    /// The grandfather abused the grandchild years ago (never shown). The player – the grandfather –
    /// starts with the secret and lives with the consequences. The player never makes the choice to abuse.
    /// </summary>
    private static void AbusePast(SimContext ctx, Person grandfather, Person grandchild)
    {
        int when = Math.Min(ctx.Year - 1, grandchild.BirthYear + 9);
        var secret = DarkSystem.StartAbuse(ctx, grandfather, grandchild, when);
        if (grandfather.Id == ctx.World.PlayerId)
            EventSystem.QueueSituation(ctx, "abuse_past", new() { ["target"] = grandchild.Id }, new() { ["secret"] = secret.Id });
    }
}
