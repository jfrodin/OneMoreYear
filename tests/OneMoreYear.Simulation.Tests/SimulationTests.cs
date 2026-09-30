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
                Assert.Contains(Kinship.Label(w, player, w.Get(gp)), new[] { "grandfather", "grandmother" });
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
