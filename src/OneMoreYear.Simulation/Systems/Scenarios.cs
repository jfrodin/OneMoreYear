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
    private const string ChildFlag = "scenario_child";

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
        StartChoices.Tweak(ctx, player, s.Player is { } pt ? new ScenarioTweak { Traits = pt.Traits, Smarts = pt.Smarts, Looks = pt.Looks, Fitness = pt.Fitness, Addiction = pt.Addiction } : null);
        foreach (var p in parents) StartChoices.Tweak(ctx, p, s.Parents);
        StartChoices.Tweak(ctx, father, s.Father);
        StartChoices.Tweak(ctx, mother, s.Mother);
        StartChoices.Tweak(ctx, grandfather, s.Grandfather);
        grandfather?.Flags.Add(GrandfatherFlag);

        // "The family secret": the mother was fifteen when the child was born, and her teenage
        // boyfriend – the legal father – has always believed the child is his.
        if (s.Storyline == "hidden_father" && mother != null && father != null)
        {
            MakeTeenager(ctx, mother, player.BirthYear - 15);
            MakeTeenager(ctx, father, player.BirthYear - 17);
            mother.PartnerStatus = father.PartnerStatus = PartnerStatus.Dating;
        }
        player.Flags.Add(ChildFlag);
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
        if (p.HomeValue > 0) EconomySystem.SellHome(ctx, p, log: false);
        p.Money = p.Funds = p.Stocks = 0;
        p.Holdings.Clear();
        p.OwnsHome = false;
        p.SharesFlat = false;
        p.LivesWithParents = true;
        PersonFactory.SetUpLifeStage(ctx, p);
    }

    /// <summary>Whether a started scenario is what it promises (see ScenarioDef.Requires and the storyline).</summary>
    public static bool Meets(GameSession session, ScenarioDef s)
    {
        var w = session.World;
        if (session.GameOver || !session.Player.IsAlive) return false;
        // The person the scenario was built around: the original child, even when the player took over someone else.
        var child = w.People.FirstOrDefault(p => p.Flags.Contains(ChildFlag)) ?? session.Player;
        if (s.Age > 0 && child.Age(session.Year) != s.Age) return false;
        if (s.Storyline == "hidden_father")
        {
            // The child must be the mother's first: she was fifteen when it was born.
            if (w.Secrets.FirstOrDefault(x => x.Kind == "origin") is not { } origin) return false;
            var mother = w.Get(origin.VictimId!.Value);
            if (Kinship.Children(w, mother).Any(k => k.BirthYear < child.BirthYear)) return false;
        }
        if (s.PlayAs != null && session.Player.Id == child.Id) return false;
        if (s.Requires is not { } r) return true;
        var p = child;
        if (r.Sex is { } sex && (p.Sex == Sex.Female) != (sex == "female")) return false;
        if (r.Partner is { } partner && (p.PartnerId != null) != partner) return false;
        if (r.Working is { } working && (p.Activity == Activity.Working) != working) return false;
        if (r.MinChildren is { } kids && Kinship.Children(w, p).Count(k => k.IsAlive) < kids) return false;
        if (r.MinGrandchildren is { } gk && Kinship.Grandchildren(w, p).Count(k => k.IsAlive) < gk) return false;
        return true;
    }

    /// <summary>Plays the years before the scenario's start age, then hands over and starts its storyline.</summary>
    public static void FastForward(GameSession session, ScenarioDef s)
    {
        var bot = new AutoPlayer(session.World.Seed, useActions: false);
        int guard = 0;
        while (session.Player.Age(session.Year) < s.Age && !session.GameOver && guard++ < 150)
            bot.PlayYear(session);
        if (s.Player is { } pt) StartChoices.Tweak(session.Ctx, session.Player, new ScenarioTweak { Money = pt.Money, OwnsHome = pt.OwnsHome, Unemployed = pt.Unemployed });

        var ctx = session.Ctx;
        var w = ctx.World;
        var child = session.Player;
        var grandfather = w.People.FirstOrDefault(p => p.Flags.Contains(GrandfatherFlag) && p.IsAlive);
        var mother = w.People.FirstOrDefault(p => p.Flags.Contains(MotherFlag) && p.IsAlive);
        var legalFather = w.People.FirstOrDefault(p => p.Flags.Contains(FatherFlag));

        if (s.Storyline == "hidden_father" && ctx.Happens(ContentCategories.SexualAbuse) && grandfather != null && mother != null && mother.ParentIds.Contains(grandfather.Id))
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
