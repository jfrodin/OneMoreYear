using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// The player's social world outside the family: classmates, colleagues, a boss and friends of
/// friends. They are the pool that friendships, crushes and relationships grow from.
/// </summary>
public static class SocialSystem
{
    private const int Classmates = 4, Colleagues = 3, FormerKept = 6;

    /// <summary>Keeps the player's classmates and colleagues in step with where they study or work.</summary>
    public static void Update(SimContext ctx)
    {
        var w = ctx.World;
        var p = w.Player;
        if (!p.IsAlive) return;
        int age = p.Age(ctx.Year);

        p.Acquaintances.RemoveAll(a => !w.Get(a.Id).IsAlive || p.FriendIds.Contains(a.Id) || p.PartnerId == a.Id);

        bool inSchool = p.Activity is Activity.School or Activity.Studying && age >= 7;
        string place = PlaceKey(p);

        // Leaving a school or a job turns everyone there into "old" acquaintances.
        foreach (var a in p.Acquaintances.Where(a => a.Current))
        {
            bool stillThere = a.Kind switch
            {
                "classmate" => inSchool && p.Flags.Contains(PlaceFlag(place)),
                "colleague" or "boss" => p.Activity == Activity.Working && p.Flags.Contains(PlaceFlag(place)),
                _ => true
            };
            if (!stillThere) a.Current = false;
        }
        p.Flags.RemoveWhere(f => f.StartsWith("place:") && f != PlaceFlag(place));
        p.Flags.Add(PlaceFlag(place));

        if (inSchool)
            while (p.Acquaintances.Count(a => a.Current && a.Kind == "classmate") < Classmates) AddClassmate(ctx, p);
        if (p.Activity == Activity.Working)
        {
            while (p.Acquaintances.Count(a => a.Current && a.Kind == "colleague") < Colleagues) AddColleague(ctx, p, boss: false);
            if (!p.Acquaintances.Any(a => a.Current && a.Kind == "boss")) AddColleague(ctx, p, boss: true);
        }

        // Old acquaintances fade away.
        var former = p.Acquaintances.Where(a => !a.Current).OrderBy(a => w.Rel(p.Id, a.Id).Closeness).ToList();
        foreach (var a in former.Take(Math.Max(0, former.Count - FormerKept))) p.Acquaintances.Remove(a);

        // Attraction grows (or not) between people who see each other and could be a couple.
        foreach (var a in p.Acquaintances.Where(a => a.Current).Select(a => w.Get(a.Id)).Concat(p.FriendIds.Select(w.Get)))
        {
            if (!EventSystem.Compatible(ctx, p, a)) continue;
            var r = w.Rel(a.Id, p.Id);
            r[RelDim.Attraction] += (p.Looks - 50) * 0.08 + ctx.Rng.Gaussian(0, 5);
            var mine = w.Rel(p.Id, a.Id);
            mine[RelDim.Attraction] += (a.Looks - 50) * 0.08 + ctx.Rng.Gaussian(0, 5);
        }
    }

    private static string PlaceKey(Person p) => p.Activity switch
    {
        Activity.School => $"school",
        Activity.Studying => $"study:{p.ProgrammeId}",
        Activity.Working => $"work:{p.OccupationId}:{p.Flags.Count(f => f.StartsWith("job_start:"))}",
        _ => "none"
    };

    private static string PlaceFlag(string place) => "place:" + place;

    /// <summary>A new job means new colleagues, even in the same field.</summary>
    public static void OnNewJob(Person p, int year)
    {
        p.Flags.Add($"job_start:{year}:{p.OccupationId}");
    }

    private static Person AddClassmate(SimContext ctx, Person p)
    {
        int age = p.Age(ctx.Year);
        int spread = p.Activity == Activity.Studying && p.StudyingFor == EducationLevel.University ? 3 : 0;
        var sex = ctx.Rng.Chance(0.5) ? Sex.Male : Sex.Female;
        var mate = PersonFactory.CreateStranger(ctx, sex, Math.Max(6, age + ctx.Rng.Range(-Math.Min(1, spread), spread)));
        if (p.Activity == Activity.Studying && p.ProgrammeId != null && ctx.Content.Programme(p.ProgrammeId) is { } prog)
        {
            mate.Activity = Activity.Studying;
            mate.ProgrammeId = prog.Id;
            mate.StudyingFor = prog.Level;
            mate.StudyYearsLeft = Math.Max(1, p.StudyYearsLeft);
            mate.OccupationId = null;
        }
        Meet(ctx, p, mate, "classmate");
        return mate;
    }

    private static Person AddColleague(SimContext ctx, Person p, bool boss)
    {
        var occ = ctx.Content.Occupation(p.OccupationId)!;
        int level = boss ? Math.Min(occ.Levels.Count - 1, p.OccupationLevel + 1) : Math.Max(0, p.OccupationLevel + ctx.Rng.Range(-1, 0));
        int age = Math.Clamp(p.Age(ctx.Year) + ctx.Rng.Range(boss ? 3 : -8, boss ? 20 : 12), 20, ctx.Country.PensionAge - 1);
        var c = PersonFactory.CreateStranger(ctx, ctx.Rng.Chance(0.5) ? Sex.Male : Sex.Female, age);
        c.Activity = Activity.Working;
        c.OccupationId = occ.Id;
        c.OccupationLevel = level;
        c.Income = occ.Levels[level].Salary;
        Meet(ctx, p, c, boss ? "boss" : "colleague");
        if (boss) ctx.World.Rel(c.Id, p.Id).Respect = Math.Clamp(ctx.Rng.Gaussian(45, 12), 0, 100);
        return c;
    }

    /// <summary>Someone new the player knows. Returns the acquaintance.</summary>
    public static Acquaintance Meet(SimContext ctx, Person p, Person other, string kind, int? viaId = null)
    {
        var a = new Acquaintance { Id = other.Id, Kind = kind, SinceYear = ctx.Year, ViaId = viaId };
        p.Acquaintances.Add(a);
        PersonFactory.SetBond(ctx, p, other, 28, 45);
        PersonFactory.SetBond(ctx, other, p, 28, 45);
        if (EventSystem.Compatible(ctx, p, other))
        {
            ctx.World.Rel(other.Id, p.Id).Attraction = Math.Clamp(ctx.Rng.Gaussian(20 + (p.Looks - 50) * 0.6, 15), 0, 100);
            ctx.World.Rel(p.Id, other.Id).Attraction = Math.Clamp(ctx.Rng.Gaussian(20 + (other.Looks - 50) * 0.6, 15), 0, 100);
        }
        return a;
    }

    /// <summary>Going out with a friend and meeting someone from their circle.</summary>
    public static Person? MeetThroughFriend(SimContext ctx, Person p)
    {
        var w = ctx.World;
        var friends = p.FriendIds.Select(w.Get).Where(f => f.IsAlive).ToList();
        if (friends.Count == 0) return null;
        var friend = ctx.Rng.Pick(friends);
        int age = p.Age(ctx.Year);
        // Often someone who could become more than a friend – that is half the point of going out.
        bool romantic = p.PartnerId == null && ctx.Rng.Chance(0.6);
        var sex = romantic ? (p.AttractedToSameSex ? p.Sex : (p.Sex == Sex.Male ? Sex.Female : Sex.Male))
            : ctx.Rng.Chance(0.5) ? Sex.Male : Sex.Female;
        int theirAge = romantic ? EventSystem.RomanticAge(ctx, age, age + ctx.Rng.Range(-4, 4)) : Math.Max(12, age + ctx.Rng.Range(-5, 5));
        var person = PersonFactory.CreateStranger(ctx, sex, theirAge);
        if (romantic) person.AttractedToSameSex = p.AttractedToSameSex;
        PersonFactory.SetBond(ctx, friend, person, 60, 60);
        PersonFactory.SetBond(ctx, person, friend, 60, 60);
        Meet(ctx, p, person, "friend_of_friend", friend.Id);
        return person;
    }

    public static Acquaintance? Find(Person p, int otherId) => p.Acquaintances.FirstOrDefault(a => a.Id == otherId);

    /// <summary>"classmate", "old colleague", "Anna's friend" ... or null if not an acquaintance.</summary>
    public static string? Label(World w, Person viewer, Person other)
    {
        var a = Find(viewer, other.Id);
        if (a == null) return null;
        return a.Kind switch
        {
            "classmate" => a.Current ? "classmate" : "old classmate",
            "colleague" => a.Current ? "colleague" : "former colleague",
            "boss" => a.Current ? "boss" : "former boss",
            "friend_of_friend" when w.TryGet(a.ViaId) is { } via => $"{Kinship.Genitive(via.FirstName)} friend",
            _ => "acquaintance"
        };
    }
}
