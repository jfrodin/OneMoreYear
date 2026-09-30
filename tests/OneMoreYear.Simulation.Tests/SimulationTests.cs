using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Model;
using OneMoreYear.Simulation.Systems;

namespace OneMoreYear.Simulation.Tests;

public class ContentTests
{
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
