using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Model;
using OneMoreYear.Simulation.Systems;

namespace OneMoreYear.Simulation.Tests;

public class ContentTests
{
    [Fact]
    public void MisspeltContentFieldsAreErrors()
    {
        var json = "[ { \"id\": \"x\", \"name\": \"X\", \"tone\": \"light\", \"wieght\": 2 } ]";
        Assert.Throws<InvalidDataException>(() => ContentDb.Load(new[] { ("content/traits.json", json) }));
    }

    [Fact]
    public void EmbeddedContentLoadsAndValidates()
    {
        var db = ContentDb.LoadEmbedded();
        Assert.Empty(db.Validate());
        Assert.True(db.Traits.Count >= 12);
        Assert.Contains("sweden", db.Countries.Keys);
        Assert.NotEmpty(db.RandomEvents);
    }
}

public class DeterminismTests
{
    private static string Play(ulong seed, int years)
    {
        var session = GameSession.NewGame(new NewGameOptions { Seed = seed, StartYear = 1960 });
        var bot = new AutoPlayer(seed);
        for (int i = 0; i < years && bot.PlayYear(session); i++) { }
        return session.Save();
    }

    [Fact]
    public void SameSeedGivesIdenticalWorld()
    {
        Assert.Equal(Play(42, 80), Play(42, 80));
    }

    [Fact]
    public void DifferentSeedsGiveDifferentWorlds()
    {
        Assert.NotEqual(Play(1, 30), Play(2, 30));
    }

    [Fact]
    public void SaveAndLoadContinuesIdentically()
    {
        var a = GameSession.NewGame(new NewGameOptions { Seed = 7, StartYear = 1970 });
        var botA = new AutoPlayer(99);
        for (int i = 0; i < 30; i++) botA.PlayYear(a);

        var b = GameSession.Load(a.Save());
        var botB = new AutoPlayer(1234);
        var botA2 = new AutoPlayer(1234);
        for (int i = 0; i < 30; i++)
        {
            botA2.PlayYear(a);
            botB.PlayYear(b);
        }
        Assert.Equal(a.Save(), b.Save());
    }
}

public class LongRunTests
{
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    [InlineData(5UL)]
    [InlineData(6UL)]
    public void FamilySurvivesManyGenerationsWithoutErrors(ulong seed)
    {
        var session = GameSession.NewGame(new NewGameOptions { Seed = seed, StartYear = 1950 });
        var bot = new AutoPlayer(seed);
        var unresolved = new List<string>();
        bot.OnText = t => { if (t.Contains('{')) unresolved.Add(t); };

        for (int i = 0; i < 150 && bot.PlayYear(session); i++) { }

        Assert.Empty(unresolved);
        Assert.True(session.World.People.Count < 5000, $"Population exploded: {session.World.People.Count}");
        foreach (var line in session.Chronicle()) Assert.DoesNotContain("{", line.Text);
        // The views used by the UI must work at any point.
        if (!session.GameOver && session.Player.IsAlive)
        {
            Assert.NotNull(session.Describe(session.Player.Id));
            foreach (var p in session.Family()) Assert.False(string.IsNullOrEmpty(p.RoleLabel));
        }
        Assert.NotEmpty(session.FamilyTree());
    }
}

public class KinshipTests
{
    [Fact]
    public void StartingFamilyHasSensibleLabels()
    {
        var session = GameSession.NewGame(new NewGameOptions { Seed = 11, StartYear = 1980 });
        var w = session.World;
        var player = w.Player;
        Assert.Equal(0, player.Age(w.Year));
        Assert.Equal(2, player.ParentIds.Count);
        foreach (var parentId in player.ParentIds)
        {
            var label = Kinship.Label(w, player, w.Get(parentId));
            Assert.Contains(label, new[] { "father", "mother" });
            foreach (var gp in w.Get(parentId).ParentIds)
                Assert.Matches("^(paternal|maternal) (grandfather|grandmother)$", Kinship.Label(w, player, w.Get(gp)));
        }
    }
}

public class AnnotationTests
{
    [Fact]
    public void NamesGetRelationToPlayer()
    {
        var session = GameSession.NewGame(new NewGameOptions { Seed = 5, StartYear = 1980 });
        var w = session.World;
        var mother = w.Get(w.Player.ParentIds.Select(w.Get).First(p => p.Sex == OneMoreYear.Simulation.Model.Sex.Female).Id);
        var text = session.Annotate($"{mother.FirstName} got a job.", new[] { mother.Id });
        Assert.Equal($"{mother.FirstName} (your mother) got a job.", text);
        // Not twice, and not when the text already says it.
        Assert.Equal(text, session.Annotate(text, new[] { mother.Id }));
        Assert.Equal($"Your mother, {mother.FirstName}, called.", session.Annotate($"Your mother, {mother.FirstName}, called.", new[] { mother.Id }));
    }
}

public class AgeLawTests
{
    private static (GameSession S, OneMoreYear.Simulation.Model.Person A, OneMoreYear.Simulation.Model.Person B) Pair(int ageA, int ageB)
    {
        var s = GameSession.NewGame(new NewGameOptions { Seed = 9, StartYear = 1990 });
        var a = PersonFactory.CreateStranger(s.Ctx, OneMoreYear.Simulation.Model.Sex.Male, ageA);
        var b = PersonFactory.CreateStranger(s.Ctx, OneMoreYear.Simulation.Model.Sex.Female, ageB);
        a.AttractedToSameSex = b.AttractedToSameSex = false;
        return (s, a, b);
    }

    [Theory]
    [InlineData(13, 13, true)]   // innocent "going steady" between kids of the same age
    [InlineData(13, 16, false)]  // under the age of consent with someone older
    [InlineData(10, 10, false)]  // too young for any of it
    [InlineData(15, 17, true)]
    [InlineData(16, 20, false)]  // minor with a much older partner
    [InlineData(25, 14, false)]  // adult and child – never
    [InlineData(30, 45, true)]
    public void CouplesFollowTheLaw(int ageA, int ageB, bool allowed)
    {
        var (s, a, b) = Pair(ageA, ageB);
        Assert.Equal(allowed, EventSystem.Compatible(s.Ctx, a, b));
    }

    [Fact]
    public void NobodyBecomesAParentBelowTheAgeOfConsent()
    {
        var session = GameSession.NewGame(new NewGameOptions { Seed = 21, StartYear = 1950 });
        var bot = new AutoPlayer(21);
        for (int i = 0; i < 150 && bot.PlayYear(session); i++) { }
        int consent = session.Country.AgeOfConsent;
        foreach (var child in session.World.People.Where(p => p.ParentIds.Count > 0 && !p.IsAdopted))
        {
            var mother = child.ParentIds.Select(session.World.Get).FirstOrDefault(p => p.Sex == OneMoreYear.Simulation.Model.Sex.Female);
            if (mother != null) Assert.True(child.BirthYear - mother.BirthYear >= consent, $"{mother.FullName} was {child.BirthYear - mother.BirthYear}");
        }
    }
}

public class CareerAndMoneyTests
{
    [Fact]
    public void EveryoneWorkingIsQualifiedForTheirJob()
    {
        var session = GameSession.NewGame(new NewGameOptions { Seed = 33, StartYear = 1960 });
        var bot = new AutoPlayer(33);
        for (int i = 0; i < 120 && bot.PlayYear(session); i++) { }
        foreach (var p in session.World.People.Where(p => p.IsAlive && p.Activity == OneMoreYear.Simulation.Model.Activity.Working))
        {
            var occ = session.Content.Occupation(p.OccupationId)!;
            Assert.True(CareerSystem.QualifiesFor(p, occ.Levels[p.OccupationLevel]),
                $"{p.FullName} works as {occ.Levels[p.OccupationLevel].Title} without the right education");
        }
    }

    [Fact]
    public void LedgerExplainsEveryYearsChangeInMoney()
    {
        var session = GameSession.NewGame(new NewGameOptions { Seed = 44, StartYear = 1970 });
        var bot = new AutoPlayer(44, useActions: false);
        for (int i = 0; i < 60; i++)
        {
            if (session.NeedsSuccession || session.GameOver || session.HasUnresolvedEvents) { bot.PlayYear(session); continue; }
            var player = session.Player;
            double before = player.Money;
            session.AdvanceYear();
            if (!player.IsAlive || session.Player.Id != player.Id) continue;
            double explained = session.World.Ledger.Where(l => l.Year == session.Year).Sum(l => l.Amount);
            Assert.True(Math.Abs(player.Money - before - explained) < 1,
                $"{session.Year}: money changed by {player.Money - before:0} but the ledger explains {explained:0}");
        }
    }
}

public class FamilyLifeTests
{
    private static GameSession AdultPlayerWithPartner(ulong seed)
    {
        var s = GameSession.NewGame(new NewGameOptions { Seed = seed, StartYear = 1960 });
        var p = s.Player;
        p.BirthYear = s.Year - 28;
        p.AttractedToSameSex = false;
        var partner = PersonFactory.CreateStranger(s.Ctx, p.Sex == OneMoreYear.Simulation.Model.Sex.Male
            ? OneMoreYear.Simulation.Model.Sex.Female : OneMoreYear.Simulation.Model.Sex.Male, 27);
        partner.AttractedToSameSex = false;
        FamilySystem.StartDating(s.Ctx, p, partner);
        return s;
    }

    [Fact]
    public void BabyArrivesNextYearAndCanBeNamed()
    {
        var s = AdultPlayerWithPartner(3);
        var p = s.Player;
        int kidsBefore = p.ChildIds.Count;
        FamilySystem.Expect(s.Ctx, p, s.World.Get(p.PartnerId!.Value));
        Assert.Equal(kidsBefore, p.ChildIds.Count);

        s.World.PendingEvents.Clear();
        s.AdvanceYear();
        Assert.Equal(kidsBefore + 1, p.ChildIds.Count);
        var naming = s.CurrentEvents().Single(e => e.Title == "A new baby");
        var chosen = naming.Choices[2];
        s.Choose(naming.Uid, chosen.Index);
        Assert.Equal(chosen.Text, s.World.Get(p.ChildIds.Last()).FirstName);
    }

    [Fact]
    public void NewRomanceWhileTakenBecomesAnAffair()
    {
        var s = AdultPlayerWithPartner(4);
        var p = s.Player;
        int partner = p.PartnerId!.Value;
        var other = PersonFactory.CreateStranger(s.Ctx, s.World.Get(partner).Sex, 28);
        other.AttractedToSameSex = false;
        var pending = new OneMoreYear.Simulation.Model.PendingEvent { EventId = "admirer", Roles = { ["target"] = other.Id } };
        EffectApplier.Apply(s.Ctx, new OneMoreYear.Simulation.Content.EffectDef { Type = "start_dating", To = "target" }, pending);

        Assert.Equal(partner, p.PartnerId);
        Assert.Contains(s.World.Secrets, x => x.Kind == "affair" && x.SubjectId == p.Id && x.OtherId == other.Id);
        Assert.Equal("lover", Kinship.Label(s.World, p, other));
        Assert.Contains(Kinship.Circle(s.World, p), x => x.Id == other.Id);
    }
}

public class FamilyTieTests
{
    [Fact]
    public void CloseRelativesNeverCousinsPerCountryLaw()
    {
        var s = GameSession.NewGame(new NewGameOptions { Seed = 17, StartYear = 1950 });
        var w = s.World;
        var bot = new AutoPlayer(17, useActions: false);
        for (int i = 0; i < 60; i++) bot.PlayYear(s);

        int checkedPairs = 0;
        foreach (var a in w.People.Where(p => p.IsBlood).Take(60))
            foreach (var b in w.People.Where(p => p.IsBlood && p.Id > a.Id).Take(60))
            {
                var tie = Kinship.Blood(w, a, b);
                bool siblings = a.ParentIds.Intersect(b.ParentIds).Any() && !a.IsAdopted && !b.IsAdopted
                                && a.BiologicalFatherId == null && b.BiologicalFatherId == null;
                if (siblings) Assert.Equal(OneMoreYear.Simulation.Model.BloodTie.Close, tie);
                // A legal parent is a blood relative unless the child is adopted or has a hidden biological father.
                bool bloodParent = Kinship.BiologicalParents(w, b).Any(x => x.Id == a.Id) || Kinship.BiologicalParents(w, a).Any(x => x.Id == b.Id);
                if (bloodParent) Assert.Equal(OneMoreYear.Simulation.Model.BloodTie.Close, tie);
                if (tie == OneMoreYear.Simulation.Model.BloodTie.Close) checkedPairs++;
            }
        Assert.True(checkedPairs > 0);

        // Nobody in the family ever ended up with a close relative.
        foreach (var p in w.People.Where(p => p.PartnerId != null || p.ExPartnerIds.Count > 0))
            foreach (var partner in p.ExPartnerIds.Append(p.PartnerId ?? 0).Where(id => id > 0).Select(w.Get))
                Assert.NotEqual(OneMoreYear.Simulation.Model.BloodTie.Close, Kinship.Blood(w, p, partner));
    }
}

public class HousingTests
{
    [Fact]
    public void ChildrenLiveWithParentsAndFamiliesMoveTogether()
    {
        var s = GameSession.NewGame(new NewGameOptions { Seed = 8, StartYear = 1970 });
        var w = s.World;
        var p = s.Player;
        var father = w.Get(p.ParentIds[0]);
        Assert.True(p.LivesWithParents);
        Assert.Equal(father.CityId, p.CityId);

        var mother = w.Get(p.ParentIds[1]);
        string target = s.Country.Cities.First(c => c.Id != father.CityId).Id;
        HousingSystem.MoveTo(s.Ctx, father, target);
        Assert.Equal(target, mother.CityId);
        Assert.Equal(target, p.CityId);
        Assert.StartsWith("Lives with parents in", s.Describe(p.Id).Home);
    }
}

public class CrimeTests
{
    [Fact]
    public void ConvictedPlayerGoesToPrisonAndComesOut()
    {
        var s = GameSession.NewGame(new NewGameOptions { Seed = 12, StartYear = 1960 });
        var p = s.Player;
        p.BirthYear = s.Year - 30;
        var text = CrimeSystem.Arrest(s.Ctx, p, s.Content.Crimes["robbery"], null, isPlayer: true);
        Assert.Contains("prison", text);
        Assert.Equal(OneMoreYear.Simulation.Model.Activity.Prison, p.Activity);
        Assert.Single(p.CriminalRecord);
        Assert.All(s.Actions(null), a => Assert.Equal("prison", a.Category));

        var bot = new AutoPlayer(12, useActions: false);
        for (int i = 0; i < 12 && p.Activity == OneMoreYear.Simulation.Model.Activity.Prison && p.IsAlive; i++) bot.PlayYear(s);
        if (p.IsAlive) Assert.NotEqual(OneMoreYear.Simulation.Model.Activity.Prison, p.Activity);
    }

    [Fact]
    public void CrimeCanBeCommittedByAnyone()
    {
        var s = GameSession.NewGame(new NewGameOptions { Seed = 13, StartYear = 1960 });
        var p = s.Player;
        p.BirthYear = s.Year - 25;
        p.Traits.Clear();
        p.Traits.Add("kind");
        s.World.ActionPoints = 3;
        Assert.Contains(s.Actions(null), a => a.Id == "crime_burglary" && a.Enabled);
        double before = p.Happiness;
        s.PerformAction("crime_burglary", null);
        Assert.True(p.Happiness < before, "A kind person should feel guilty");
    }

    [Fact]
    public void Every_test_scenario_starts_where_it_promises()
    {
        var scenarios = GameSession.AvailableScenarios();
        Assert.NotEmpty(scenarios);
        foreach (var sc in scenarios)
        {
            var s = GameSession.NewGame(new NewGameOptions { ScenarioId = sc.Id });
            Assert.True(s.Player.IsAlive, sc.Id);
            Assert.False(s.GameOver, sc.Id);
            if (sc.PlayAs == null) Assert.Equal(sc.Age, s.Player.Age(s.Year));
            foreach (var trait in sc.PlayAs == null ? sc.Player?.Traits ?? new() : new())
                Assert.Contains(trait, s.Player.Traits);
            // The same scenario always gives the same start.
            var again = GameSession.NewGame(new NewGameOptions { ScenarioId = sc.Id });
            Assert.Equal(s.Player.FullName, again.Player.FullName);
        }
        var poor = GameSession.NewGame(new NewGameOptions { ScenarioId = "nothing_to_lose" });
        Assert.Contains(Kinship.Parents(poor.World, poor.Player), p => p.Addiction == "alcohol");
    }

    [Fact]
    public void Start_choices_shape_the_first_life()
    {
        for (ulong seed = 1; seed <= 5; seed++)
        {
            var girl = GameSession.NewGame(new NewGameOptions { Seed = seed, PlayerSex = Sex.Female, CityId = "kiruna" });
            Assert.Equal(Sex.Female, girl.Player.Sex);
            Assert.Equal("kiruna", girl.Player.CityId);

            var easy = GameSession.NewGame(new NewGameOptions { Seed = seed, StartConditions = StartChoices.Comfortable });
            var parents = Kinship.Parents(easy.World, easy.Player).ToList();
            Assert.Contains(parents, p => p.OwnsHome);
            Assert.All(parents, p => Assert.True(p.Money > 0 && p.Addiction == null && p.Activity == Activity.Working));

            var hard = GameSession.NewGame(new NewGameOptions { Seed = seed, StartConditions = StartChoices.Hard });
            var poor = Kinship.Parents(hard.World, hard.Player).ToList();
            Assert.All(poor, p => Assert.True(p.Money < 0 && !p.OwnsHome));
            Assert.Contains(poor, p => p.Activity == Activity.Unemployed);
            Assert.Equal(StartChoices.Hard, hard.World.StartConditions);
        }
        // Left to chance, nothing is recorded.
        Assert.Null(GameSession.NewGame(new NewGameOptions { Seed = 1 }).World.StartConditions);
    }

    [Fact]
    public void Texts_follow_the_times_and_speak_to_the_player()
    {
        const string text = "[[2000: A letter || A text message]] a week later.";
        Assert.Equal("A letter a week later.", TextFormatter.ByEra(1975, text));
        Assert.Equal("A text message a week later.", TextFormatter.ByEra(2000, text));

        var s = GameSession.NewGame(new NewGameOptions { Seed = 3, StartYear = 1970 });
        var name = s.Player.FirstName;
        Assert.Equal("You inherited 5,000 kr from Karin.", s.ToYou($"{name} inherited 5,000 kr from Karin."));
        Assert.Equal("You were released from prison.", s.ToYou($"{name} was released from prison."));
        Assert.Equal("Karin got married.", s.ToYou("Karin got married."));
    }

    [Fact]
    public void A_player_who_wants_love_usually_finds_it()
    {
        string[] love = { "A proposal", "Your place or mine?", "Baby talk", "A spark", "Saturday dance", "A dinner party", "Stuck", "A match", "Someone likes you", "After work" };
        int partnered = 0, married = 0, n = 12;
        for (ulong seed = 1; seed <= (ulong)n; seed++)
        {
            var s = GameSession.NewGame(new NewGameOptions { Seed = seed, StartYear = 1960 });
            var bot = new AutoPlayer(seed, useActions: false) { Prefer = ev => love.Contains(ev.Title) ? ev.Choices[0] : null };
            int playerId = s.Player.Id;
            while (s.Player.Id == playerId && s.Player.Age(s.Year) < 35 && bot.PlayYear(s)) { }
            var p = s.World.Get(playerId);
            if (p.PartnerId != null) partnered++;
            if (p.PartnerStatus == PartnerStatus.Married) married++;
        }
        Assert.True(partnered >= n * 2 / 3, $"Only {partnered} of {n} had a partner at 35");
        Assert.True(married >= n / 3, $"Only {married} of {n} were married at 35");
    }

    [Fact]
    public void Abuse_of_children_is_withdrawn_whatever_the_settings_say()
    {
        Assert.DoesNotContain(ContentCategories.Selectable, c => c.Id == ContentCategories.SexualAbuse);
        Assert.DoesNotContain(GameSession.AvailableScenarios(), s => s.Storyline == "hidden_father");
        Assert.Throws<ArgumentException>(() => GameSession.NewGame(new NewGameOptions { ScenarioId = "the_family_secret" }));
        for (ulong seed = 1; seed <= 6; seed++)
        {
            var s = GameSession.NewGame(new NewGameOptions
            {
                Seed = seed, StartYear = 1950,
                ContentSettings = new Dictionary<string, ContentLevel> { [ContentCategories.SexualAbuse] = ContentLevel.On },
            });
            Assert.Equal(ContentLevel.Off, s.ContentLevelOf(ContentCategories.SexualAbuse));
            var bot = new AutoPlayer(seed);
            for (int i = 0; i < 100 && bot.PlayYear(s); i++) { }
            var w = s.World;
            Assert.DoesNotContain(w.Secrets, x => x.Kind is "abuse" or "origin");
            Assert.DoesNotContain(w.People, p => p.Traits.Contains("predatory") || p.Memories.Any(m => m.Kind == "abused"));
        }
    }

    [Fact(Skip = "Abuse of children is withdrawn (0.23.0). Un-skip if ContentCategories.Withdrawn lets it back in.")]
    public void The_family_secret_from_three_sides()
    {
        // "The family secret" from three sides: the same family, the same hidden father.
        foreach (var (id, opening) in new[] { ("the_family_secret", "Roots"), ("the_family_secret_mother", "The DNA kit"), ("the_family_secret_daughter", "Roots") })
        {
            var secret = GameSession.NewGame(new NewGameOptions { ScenarioId = id });
            var w = secret.World;
            var origin = Assert.Single(w.Secrets, x => x.Kind == "origin");
            var father = w.Get(origin.SubjectId);
            var mother = w.Get(origin.VictimId!.Value);
            var child = w.Get(origin.ChildId!.Value);
            Assert.Contains(father.Id, mother.ParentIds);
            Assert.Equal(father.Id, child.BiologicalFatherId);
            Assert.Equal(Sex.Female, child.Sex);
            Assert.Equal(25, child.Age(secret.Year));
            Assert.Equal(15, child.BirthYear - mother.BirthYear);
            Assert.DoesNotContain(Kinship.Children(w, mother), k => k.BirthYear < child.BirthYear);
            Assert.Contains(w.Secrets, x => x.Kind == "abuse" && x.SubjectId == father.Id && x.VictimId == mother.Id && !x.Revealed);
            Assert.Contains(secret.CurrentEvents(), e => e.Title == opening);
            int expectedPlayer = id.EndsWith("mother") ? mother.Id : id.EndsWith("daughter") ? child.Id : father.Id;
            Assert.Equal(expectedPlayer, secret.Player.Id);
        }

        // Telling the truth reveals both secrets, and the grandfather faces the police.
        var gf = GameSession.NewGame(new NewGameOptions { ScenarioId = "the_family_secret" });
        var roots = gf.CurrentEvents().First(e => e.Title == "Roots");
        gf.Choose(roots.Uid, roots.Choices.Single(c => c.Text == "Tell them the truth").Index);
        Assert.All(gf.World.Secrets.Where(x => x.Kind is "origin" or "abuse"), x => Assert.True(x.Revealed));
        Assert.Contains(gf.CurrentEvents(), e => e.Title == "Everyone knows");
    }
}

public class FaceTests
{
    private static double Distance(Face a, Face b) =>
        Math.Abs(a.Width - b.Width) + Math.Abs(a.Jaw - b.Jaw) + Math.Abs(a.Nose - b.Nose) + Math.Abs(a.Eyes - b.Eyes)
        + Math.Abs(a.Mouth - b.Mouth) + Math.Abs(a.Skin - b.Skin) + Math.Abs(a.Curl - b.Curl);

    [Fact]
    public void ChildrenLookLikeTheirParentsAndFacesAreStable()
    {
        // The whole simulated world: everyone with two known biological parents.
        var s = GameSession.NewGame(new NewGameOptions { Seed = 8, StartYear = 1950 });
        var bot = new AutoPlayer(8);
        for (int i = 0; i < 120 && bot.PlayYear(s); i++) { }
        var w = s.World;
        var children = w.People.Where(p => Kinship.BiologicalParents(w, p).Count() == 2).Take(60).ToList();
        Assert.True(children.Count >= 12, $"only {children.Count} children");

        double related = children.Average(c => Kinship.BiologicalParents(w, c).Average(p => Distance(Faces.Of(w, c), Faces.Of(w, p))));
        var rng = new Random(1);
        double strangers = children.Average(c => Distance(Faces.Of(w, c), Faces.Of(w, w.People[rng.Next(w.People.Count)])));
        Assert.True(related < strangers * 0.8, $"related {related:0.00} vs strangers {strangers:0.00}");

        // The same face after saving and loading, and for a fresh copy of the world.
        var again = GameSession.Load(s.Save());
        foreach (var c in children.Take(5))
            Assert.Equal(Faces.Of(w, c).Nose, Faces.Of(again.World, again.World.Get(c.Id)).Nose);
    }
}

public class EmployerTests
{
    [Fact]
    public void WorkingPeopleHaveEmployers()
    {
        var s = GameSession.NewGame(new NewGameOptions { Seed = 12, StartYear = 1970 });
        var bot = new AutoPlayer(12);
        for (int i = 0; i < 50; i++) bot.PlayYear(s);
        var working = s.World.People.Where(p => p.IsAlive && p.Activity == Activity.Working && p.OccupationId != "crime").ToList();
        Assert.NotEmpty(working);
        // Everyone hired since employers exist has one (people created already working may not).
        Assert.True(working.Count(p => p.Employer != null) > working.Count / 2);
        Assert.All(working.Where(p => p.Employer != null), p => Assert.DoesNotContain("{", p.Employer));
    }
}

public class InvestmentTests
{
    [Fact]
    public void MarketIsStablePerSeedAndCrashesInCrises()
    {
        var a = GameSession.NewGame(new NewGameOptions { Seed = 3, StartYear = 1950 });
        var b = GameSession.NewGame(new NewGameOptions { Seed = 3, StartYear = 1950 });
        Assert.Equal(Market.For(a.Ctx, 1987), Market.For(b.Ctx, 1987));
        Assert.True(Market.For(a.Ctx, 2008).Stocks < -0.2, "2008 should be a crash");
    }

    [Fact]
    public void BuyingInvestingAndSellingMoveMoneyCorrectly()
    {
        var s = GameSession.NewGame(new NewGameOptions { Seed = 4, StartYear = 1990 });
        var p = s.Player;
        p.BirthYear = s.Year - 30;
        p.Money = 2_000_000;
        p.Income = 600_000;
        p.LivesWithParents = false;
        Assert.True(EconomySystem.CanBuyHome(s.Ctx, p));
        double before = EconomySystem.NetWorth(s.Ctx, p);
        EconomySystem.BuyHome(s.Ctx, p);
        Assert.True(p.Mortgage > 0 && p.HomeValue > p.Mortgage);
        Assert.Equal(before, EconomySystem.NetWorth(s.Ctx, p), 3);

        double invested = EconomySystem.Invest(s.Ctx, p, "funds", 0.5);
        Assert.Equal(invested, InvestmentSystem.Value(p), 3);
        EconomySystem.SellInvestments(s.Ctx, p);
        Assert.Empty(p.Holdings);
        Assert.Equal(before, EconomySystem.NetWorth(s.Ctx, p), 3);
    }

    [Fact]
    public void FortunesDoNotExplodeOverGenerations()
    {
        var s = GameSession.NewGame(new NewGameOptions { Seed = 1, StartYear = 1950 });
        var bot = new AutoPlayer(1);
        for (int i = 0; i < 150 && bot.PlayYear(s); i++) { }
        double richest = s.World.People.Where(p => p.IsAlive).Max(p => s.Ctx.Real(EconomySystem.NetWorth(s.Ctx, p)));
        Assert.True(richest < 150_000_000, $"Richest has {richest / 1e6:0} million in 2020-kronor");
    }
}

public class SeedCodeTests
{
    [Fact]
    public void CodesAreReadableAndGiveTheSameWorld()
    {
        var a = GameSession.NewGame(new NewGameOptions { StartYear = 1970 });
        Assert.Matches("^[A-HJ-NP-Z2-9]{8}$", a.SeedCode);
        var b = GameSession.NewGame(new NewGameOptions { StartYear = 1970, SeedCode = a.SeedCode.ToLowerInvariant() });
        Assert.Equal(a.World.Seed, b.World.Seed);
        Assert.Equal(a.Player.FullName, b.Player.FullName);
        // Any text is a world; plain numbers stay the old numeric seeds.
        Assert.Equal(12345UL, GameSession.NewGame(new NewGameOptions { SeedCode = "12345" }).World.Seed);
        Assert.Equal(OneMoreYear.Simulation.Core.SeedCode.ToSeed("svensson"), GameSession.NewGame(new NewGameOptions { SeedCode = "Svens son" }).World.Seed);
    }
}

public class ContentSettingsTests
{
    private static Dictionary<string, ContentLevel> All(ContentLevel level) =>
        ContentCategories.All.ToDictionary(c => c.Id, _ => level);

    [Fact]
    public void ThemesTurnedOffNeverHappen()
    {
        var s = GameSession.NewGame(new NewGameOptions { Seed = 7, StartYear = 1950, ContentSettings = All(ContentLevel.Off) });
        var bot = new AutoPlayer(7);
        var titles = new List<string>();
        bot.OnText = titles.Add;
        for (int i = 0; i < 150 && bot.PlayYear(s); i++) { }
        var w = s.World;
        Assert.DoesNotContain(w.Secrets, x => x.Kind is "abuse" or "affair" or "paternity" or "murder" or "origin");
        Assert.DoesNotContain(w.People, p => p.Traits.Contains("predatory") || p.Addiction != null);
        Assert.DoesNotContain(w.People, p => p.Ailments.Keys.Any(k => k != "dementia"));
        Assert.DoesNotContain(w.People, p => p.Memories.Any(m => m.Kind is "hit" or "abused" or "betrayed" or "addicted_parent"));
        Assert.DoesNotContain(w.People, p => p.CriminalRecord.Any(r => s.Content.Crimes[r.CrimeId].Violent));
    }

    [Fact]
    public void MentionedThemesNeverReachThePlayer()
    {
        var s = GameSession.NewGame(new NewGameOptions { Seed = 7, StartYear = 1950, ContentSettings = All(ContentLevel.Mentioned) });
        var bot = new AutoPlayer(7);
        var seen = new HashSet<string>();
        for (int i = 0; i < 150 && bot.PlayYear(s); i++)
        {
            foreach (var e in s.World.PendingEvents) seen.Add(e.EventId);
            foreach (var a in s.Actions(null)) seen.Add(a.Id);
        }
        var tagged = s.Content.Events.Values.Where(e => e.Content.Count > 0).Select(e => e.Id).ToHashSet();
        Assert.Empty(seen.Intersect(tagged));
    }
}

public class InsightTests
{
    [Fact]
    public void TraitsRevealInsightsAndUnlockChoices()
    {
        var s = GameSession.NewGame(new NewGameOptions { Seed = 5, StartYear = 1980 });
        var p = s.Player;
        p.Traits.Clear();
        var pending = EventSystem.QueueSituation(s.Ctx, "life_speeding")!;
        var plain = s.DescribeEvent(pending);
        Assert.DoesNotContain(plain.Choices, c => c.Tag != null);

        p.Traits.Add("charming");
        var charming = s.DescribeEvent(pending);
        var tagged = Assert.Single(charming.Choices, c => c.Tag == "Charming");
        Assert.True(tagged.Available);

        var scam = EventSystem.QueueSituation(s.Ctx, "old_scam")!;
        p.Traits.Add("paranoid");
        Assert.Contains(s.DescribeEvent(scam).Insights!, i => i.Label == "Paranoid");
        // A trait choice can be chosen only with the trait.
        p.Traits.Remove("charming");
        Assert.Throws<InvalidOperationException>(() => s.Choose(pending.Uid, tagged.Index));
    }
}

public class MarketAndHomeTests
{
    private static GameSession Adult(int year, double money)
    {
        var s = GameSession.NewGame(new NewGameOptions { Seed = 3, StartYear = year });
        var p = s.Player;
        p.BirthYear = s.Year - 30;
        p.LivesWithParents = false;
        p.Money = money;
        return s;
    }

    [Fact]
    public void Holdings_follow_their_asset_and_pay_dividends()
    {
        var s = Adult(1985, 200_000);
        var p = s.Player;
        Assert.Contains(s.InvestmentOptions(), a => a.Id == "norrbruk");
        s.BuyInvestment("norrbruk", 100_000);
        Assert.Equal(100_000, p.Money, 3);
        var h = Assert.Single(p.Holdings);
        double before = h.Value;
        double money = p.Money;
        InvestmentSystem.Update(s.Ctx, p);
        double expected = InvestmentSystem.Return(s.Ctx, InvestmentSystem.Asset(s.Ctx, "norrbruk")!, s.Year);
        if (expected > -1)
        {
            Assert.Equal(before * (1 + expected), h.Value, 3);
            Assert.True(p.Money > money, "Norrbruk pays a dividend");
        }
        // The same world always has the same market.
        Assert.Equal(expected, InvestmentSystem.Return(Adult(1985, 0).Ctx, InvestmentSystem.Asset(s.Ctx, "norrbruk")!, s.Year), 9);
    }

    [Fact]
    public void History_hits_the_right_assets()
    {
        var s = Adult(1995, 0);
        var tech = InvestmentSystem.Asset(s.Ctx, "tech_fund")!;
        var bank = InvestmentSystem.Asset(s.Ctx, "vasabanken")!;
        Assert.True(InvestmentSystem.Return(s.Ctx, tech, 2001) < -0.2);
        Assert.True(InvestmentSystem.Return(s.Ctx, bank, 1992) < -0.4 || InvestmentSystem.BankruptIn(s.Ctx, bank, 1992));
        Assert.Contains(s.InvestmentOptions(), a => a.Id == "tech_fund");
        Assert.DoesNotContain(Adult(1960, 0).InvestmentOptions(), a => a.Id == "tech_fund");
    }

    [Fact]
    public void Young_players_cannot_invest()
    {
        var s = GameSession.NewGame(new NewGameOptions { Seed = 3, StartYear = 1990 });
        s.Player.Money = 5000;
        Assert.NotNull(s.Money().CannotInvest);
        Assert.Contains("15", s.BuyInvestment("sweden_fund", 1000));
        Assert.Empty(s.Player.Holdings);
    }

    [Fact]
    public void Homes_can_be_rented_and_bought_by_type()
    {
        var s = Adult(1990, 0);
        var p = s.Player;
        var options = s.HomeOptions();
        var house = Assert.Single(options, o => o.TypeId == "house");
        Assert.False(house.CanBuy);
        Assert.Contains("down payment", house.CannotBuyReason);
        Assert.Null(house.RentPerMonth);

        s.ChooseHome("three_room", buy: false);
        Assert.Equal("three_room", p.HomeType);
        Assert.False(p.OwnsHome);
        Assert.StartsWith("Rents a three-room flat", HousingSystem.Describe(s.Ctx, p));

        p.Money = 5_000_000;
        p.Income = 900_000;
        Assert.True(s.HomeOptions().Single(o => o.TypeId == "terraced").CanBuy);
        s.ChooseHome("terraced", buy: true);
        Assert.True(p.OwnsHome);
        Assert.Equal(HousingSystem.HomePrice(s.Ctx, p, "terraced"), p.HomeValue, 3);
        Assert.True(HousingSystem.HomePrice(s.Ctx, p, "house") > HousingSystem.HomePrice(s.Ctx, p, "one_room"));
    }

    [Fact]
    public void A_summer_cottage_is_an_asset()
    {
        var s = Adult(1990, 500_000);
        var p = s.Player;
        double before = EconomySystem.NetWorth(s.Ctx, p);
        EffectApplier.Apply(s.Ctx, new Content.EffectDef { Type = "buy_cottage", Amount = 250000 }, new PendingEvent { EventId = "x" });
        Assert.True(p.CottageValue > 0);
        Assert.Equal(before, EconomySystem.NetWorth(s.Ctx, p), 3);
        s.SellCottage();
        Assert.Equal(0, p.CottageValue);
        Assert.Equal(before, EconomySystem.NetWorth(s.Ctx, p), 3);
    }
}
