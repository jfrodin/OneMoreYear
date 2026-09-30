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
    private const string MotherFlag = "scenario_mother";
    private const string FatherFlag = "scenario_father";

    /// <summary>Applied right after the starting family is created.</summary>
    public static void ApplyFamily(SimContext ctx, ScenarioDef s)
    {
        var w = ctx.World;
        var player = w.Player;
        var parents = Kinship.Parents(w, player).ToList();
        var father = parents.FirstOrDefault(p => p.Sex == Sex.Male);
        var mother = parents.FirstOrDefault(p => p.Sex == Sex.Female);
        // The mother's father if he lives, otherwise any living grandfather (storylines check which one it is).
        var grandfather = (mother == null ? null : Kinship.Parents(w, mother).FirstOrDefault(p => p.Sex == Sex.Male && p.IsAlive))
                          ?? Kinship.Grandparents(w, player).FirstOrDefault(p => p.Sex == Sex.Male && p.IsAlive);

        // The player's own money and home are set when the scenario hands over (see FastForward).
        Apply(ctx, player, s.Player is { } pt ? new ScenarioTweak { Traits = pt.Traits, Smarts = pt.Smarts, Looks = pt.Looks, Fitness = pt.Fitness, Addiction = pt.Addiction } : null);
        foreach (var p in parents) Apply(ctx, p, s.Parents);
        Apply(ctx, father, s.Father);
        Apply(ctx, mother, s.Mother);
        Apply(ctx, grandfather, s.Grandfather);
        grandfather?.Flags.Add(GrandfatherFlag);

        // "The family secret": the mother was fifteen when the child was born, and her teenage
        // boyfriend – the legal father – has always believed the child is his.
        if (s.Storyline == "hidden_father" && mother != null && father != null)
        {
            MakeTeenager(ctx, mother, player.BirthYear - 15);
            MakeTeenager(ctx, father, player.BirthYear - 17);
            mother.PartnerStatus = father.PartnerStatus = PartnerStatus.Dating;
        }
        mother?.Flags.Add(MotherFlag);
        father?.Flags.Add(FatherFlag);
    }

    /// <summary>Makes a generated adult a teenager living at home: no job, no degrees, no savings.</summary>
    private static void MakeTeenager(SimContext ctx, Person p, int birthYear)
    {
        p.BirthYear = birthYear;
        p.Degrees.Clear();
        p.Education = EducationLevel.None;
        p.OccupationId = null;
        p.OccupationLevel = 0;
        p.Income = 0;
        p.YearsInJob = 0;
        p.ProgrammeId = null;
        p.StudyingFor = null;
        p.StudyYearsLeft = 0;
        p.Money = 0;
        p.OwnsHome = false;
        p.SharesFlat = false;
        p.LivesWithParents = true;
        PersonFactory.SetUpLifeStage(ctx, p);
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
        var w = ctx.World;
        var child = session.Player;
        var grandfather = w.People.FirstOrDefault(p => p.Flags.Contains(GrandfatherFlag) && p.IsAlive);
        var mother = w.People.FirstOrDefault(p => p.Flags.Contains(MotherFlag) && p.IsAlive);
        var legalFather = w.People.FirstOrDefault(p => p.Flags.Contains(FatherFlag));

        if (s.Storyline == "hidden_father" && grandfather != null && mother != null && mother.ParentIds.Contains(grandfather.Id))
        {
            var origin = DarkSystem.StartOrigin(ctx, grandfather, mother, child, legalFather);
            // The mother's "carry the secret" moment is this scenario's opening, not a separate event.
            foreach (var abuse in w.Secrets.Where(x => x.Kind == "abuse" && x.VictimId == mother.Id)) mother.Flags.Add($"carried_secret_{abuse.Id}");
            var playAs = s.PlayAs switch { "grandfather" => grandfather, "mother" => mother, _ => null };
            if (playAs != null)
            {
                session.TakeOver(playAs);
                EventSystem.GenerateRandomEvents(ctx);
            }
            var vars = new Dictionary<string, double> { ["secret"] = origin.Id };
            if (playAs == grandfather) EventSystem.QueueSituation(ctx, "origin_start_grandfather", new() { ["target"] = child.Id, ["other"] = mother.Id }, vars);
            else if (playAs == mother) EventSystem.QueueSituation(ctx, "origin_start_mother", new() { ["target"] = child.Id, ["other"] = grandfather.Id }, vars);
            else EventSystem.QueueSituation(ctx, "origin_start_child", new() { ["target"] = mother.Id, ["other"] = grandfather.Id }, vars);
        }
    }
}
