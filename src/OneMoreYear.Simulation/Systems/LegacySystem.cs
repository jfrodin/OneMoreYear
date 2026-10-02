using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// What only comes with long play (docs/endgame.md): stories about the family's own dead, told wrong
/// by time (myths); a diary found in the third generation; a genealogist in the fifth; the hundredth
/// birthday of the first of you; a reunion when the family is big; a book about it in the eighth.
/// Nothing here is announced. It just happens, once, when the family has lived long enough.
/// </summary>
public static class LegacySystem
{
    public const string MythEvent = "family_myth", DiaryEvent = "legacy_diary", GenealogistEvent = "legacy_genealogist",
        CentenaryEvent = "legacy_centenary", ReunionEvent = "legacy_reunion", BookEvent = "legacy_book";

    private static List<Person> Played(World w) => w.PlayedIds.Select(w.Get).ToList();

    public static int GenerationsPlayed(World w)
    {
        var played = Played(w);
        return played.Count == 0 ? 0 : played.Max(p => p.Generation) - played.Min(p => p.Generation) + 1;
    }

    private static string His(Person p) => p.Sex == Sex.Male ? "his" : "her";
    private static string He(Person p) => p.Sex == Sex.Male ? "he" : "she";

    public static void Update(SimContext ctx)
    {
        var w = ctx.World;
        var player = w.Player;
        if (!player.IsAlive || player.Age(ctx.Year) < 12 || w.PendingEvents.Count(e => !e.Resolved) >= 3) return;
        int generations = GenerationsPlayed(w);
        var founder = w.PlayedIds.Count > 0 ? w.Get(w.PlayedIds[0]) : null;

        // The hundredth birthday of the first of you.
        if (founder != null && ctx.Year == founder.BirthYear + 100 && w.Feats.Add($"centenary_{founder.Id}"))
        {
            Queue(ctx, CentenaryEvent, founder, new() { ["founder_years"] = founder.IsAlive ? "is still alive, and furious about the fuss" : $"died in {founder.DeathYear}" });
            return;
        }
        if (generations >= 3 && !w.Feats.Contains("diary") && ctx.Rng.Chance(0.15) && Diary(ctx) is { } diary)
        {
            w.Feats.Add("diary");
            Queue(ctx, DiaryEvent, diary.Owner, new() { ["diary"] = diary.Text });
            return;
        }
        if (generations >= 5 && !w.Feats.Contains("genealogist") && ctx.Rng.Chance(0.12) && founder != null)
        {
            w.Feats.Add("genealogist");
            int blood = w.People.Count(p => p.IsBlood);
            Queue(ctx, GenealogistEvent, founder, new()
            {
                ["founder_born"] = founder.BirthYear.ToString(),
                ["blood"] = blood.ToString(),
                ["generations"] = generations.ToString(),
            });
            return;
        }
        if (founder != null && !w.Feats.Contains("reunion") && player.Age(ctx.Year) >= 30 && LivingDescendants(w, founder) >= 15 && ctx.Rng.Chance(0.3))
        {
            w.Feats.Add("reunion");
            Queue(ctx, ReunionEvent, founder, new() { ["living"] = LivingDescendants(w, founder).ToString() });
            return;
        }
        if (generations >= 8 && !w.Feats.Contains("family_book") && player.Age(ctx.Year) >= 25 && ctx.Rng.Chance(0.1))
        {
            w.Feats.Add("family_book");
            Queue(ctx, BookEvent, founder ?? player, new() { ["generations"] = generations.ToString() });
            return;
        }
        // From the third generation, old stories start to circulate, now and then.
        if (generations >= 3 && ctx.Rng.Chance(0.08) && (!w.EventHistory.TryGetValue(MythEvent, out var last) || ctx.Year - last >= 6)
            && Myth(ctx) is { } myth)
            Queue(ctx, MythEvent, myth.Ancestor, new() { ["myth"] = myth.Told, ["truth"] = myth.Truth, ["myth_secret"] = myth.SecretId?.ToString() ?? "" });
    }

    private static void Queue(SimContext ctx, string eventId, Person target, Dictionary<string, string> words)
    {
        if (EventSystem.QueueSituation(ctx, eventId, new() { ["target"] = target.Id }) is { } pending)
            foreach (var (k, v) in words) pending.Words[k] = v;
    }

    private static int LivingDescendants(World w, Person founder)
    {
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

    // --- The diary ------------------------------------------------------------------------------

    /// <summary>A dead played ancestor's strongest memories, as diary entries in their own words.</summary>
    private static (Person Owner, string Text)? Diary(SimContext ctx)
    {
        var w = ctx.World;
        var owner = Played(w).Where(p => !p.IsAlive && p.Id != w.PlayerId && p.Memories.Count >= 3)
            .OrderBy(p => p.BirthYear).FirstOrDefault();
        if (owner == null) return null;
        var entries = owner.Memories.Where(m => Math.Abs(m.Impact) >= 15 && m.Text.Length > 0)
            .OrderByDescending(m => Math.Abs(m.Impact)).Take(4).OrderBy(m => m.Year)
            .Select(m => $"{m.Year}: {m.Text}.").ToList();
        return entries.Count < 2 ? null : (owner, string.Join(" ", entries));
    }

    // --- Myths ------------------------------------------------------------------------------

    public sealed record MythStory(Person Ancestor, string Told, string Truth, int? SecretId, string Kind = "", double Weight = 1);

    /// <summary>
    /// A story about a dead ancestor (grandparent or further back, or someone the player once was),
    /// grown in the telling. Built from what really happened to them.
    /// </summary>
    public static MythStory? Myth(SimContext ctx)
    {
        var w = ctx.World;
        var player = w.Player;
        var ancestors = new List<Person>();
        for (var gen = player.ParentIds.Select(w.Get).SelectMany(p => p.ParentIds).Select(w.Get).ToList(); gen.Count > 0;
             gen = gen.SelectMany(x => x.ParentIds).Select(w.Get).ToList())
            ancestors.AddRange(gen);
        var candidates = ancestors.Where(a => !a.IsAlive && ctx.Year - a.DeathYear!.Value >= 5).Distinct().ToList();
        if (candidates.Count == 0) return null;
        // Each story is told once; then it is family lore.
        var stories = candidates.SelectMany(a => StoriesAbout(ctx, a)).Where(s => !w.Feats.Contains(Key(s))).ToList();
        if (stories.Count == 0) return null;
        // Dark secrets and rare lives make the best stories.
        var story = ctx.Rng.PickWeighted(stories, s => s.SecretId != null ? 4 : s.Weight)!;
        w.Feats.Add(Key(story));
        return story;
    }

    private static string Key(MythStory s) => $"myth:{s.Ancestor.Id}:{s.Kind}";

    private static IEnumerable<MythStory> StoriesAbout(SimContext ctx, Person a)
    {
        var w = ctx.World;
        string name = a.FirstName;
        int age = a.DeathYear!.Value - a.BirthYear;
        bool man = a.Sex == Sex.Male;

        // A killing nobody ever found out about: the story is a whisper, and the truth is worse.
        foreach (var s in w.Secrets.Where(s => s.Kind == "murder" && s.SubjectId == a.Id && !s.Revealed))
            if (w.TryGet(s.VictimId) is { } victim)
                yield return new(a, $"people used to whisper that {name} knew more than {He(a)} ever said about what happened to {victim.FullName} in {s.Year}",
                    $"In a box of old letters there is one from {name}, never sent. It says, plainly, that {He(a)} killed {victim.FirstName}.", s.Id, $"murder{s.Id}");
        // Crimes against a person need a victim in the sentence; sexual crimes never become a family legend.
        foreach (var r in a.CriminalRecord.Where(r => r.CrimeId is not ("child_abuse" or "sexual_assault")).Take(1))
            if (ctx.Content.Crimes.TryGetValue(r.CrimeId, out var crime))
                yield return new(a, r.CrimeId switch
                    {
                        "assault" => $"{name} was the hardest fighter in town, and nobody ever dared to cross {(man ? "him" : "her")}",
                        "murder" => $"{name} was a feared gangster that even the police were afraid of",
                        "blackmail" => $"{name} knew everybody's secrets, and the whole town was polite to {(man ? "him" : "her")} because of it",
                        _ => $"{name} was a famous outlaw who {crime.Did} and was never caught",
                    },
                    $"The court records say otherwise: {name} was caught in {r.Year} and got {r.Sentence}.", null, "crime");
        if (w.Secrets.Any(s => s.Kind == "affair" && s.SubjectId == a.Id))
            yield return new(a, $"{name} had a great forbidden love, the kind they write songs about",
                "It was not a song. It was an affair, and the whole street knew, and nobody said a word for years.", null, "affair");
        if (a.Homeland != null && ctx.Content.Countries.TryGetValue(a.Homeland, out var home))
            yield return new(a, $"{name} came over from {home.Name} with nothing but a suitcase and a song",
                $"The papers in the attic show {name} came with two suitcases, a debt for the tickets, and no song anyone remembers.", null, "emigrant");
        if (a.PeakRefWorth >= 10_000_000)
            yield return new(a, $"{name} was the richest person in {HousingSystem.City(ctx, a).Name} and lit {His(a)} cigars with banknotes",
                $"{name} was well off for a few good years, and the bank owned half of it. Nobody remembers a single cigar.", null, "rich");
        if (age >= 95)
            yield return new(a, $"{name} lived to well over a hundred and never once saw a doctor",
                $"{name} lived to {age}, and the medical file is as thick as a phone book.", null, "old");
        if (age < 45)
            yield return new(a, $"{name} died a hero, saving someone from a fire",
                $"{name} died of {a.CauseOfDeath ?? "an illness"} at {age}. There was no fire. There was a family left behind, which was hard enough.", null, "young");
        if (a.ChildIds.Count >= 5)
            yield return new(a, $"{name} had a dozen children and knew every one of their birthdays by heart",
                $"{name} had {a.ChildIds.Count}. According to the old letters, {He(a)} mixed up the birthdays every single year.", null, "children");
        if (a.DreamState == DreamState.Fulfilled && DreamSystem.Of(ctx, a) is { } dream)
            yield return new(a, $"{name} was the best in the whole country at what {He(a)} did",
                $"{name} was not the best in the country. But {He(a)} got the one thing {He(a)} wanted: {dream.Name.ToLowerInvariant()}. That was enough.", null, "dream");
        if (a.Jobs.LastOrDefault() is { } job && ctx.Content.Occupation(job) is { } occ)
            yield return new(a, man ? $"{name} was so strong {He(a)} once lifted a car off a man with {His(a)} bare hands" : $"{name} could outwork any three men and still have dinner on the table at six",
                $"{name} worked in {occ.Name.ToLowerInvariant()} for most of {His(a)} life, and was tired most evenings, like everybody else.", null, "work", 0.35);
    }

    /// <summary>Digging into a myth about a hidden killing brings it to light.</summary>
    public static void RevealMythSecret(SimContext ctx, PendingEvent pending)
    {
        if (!pending.Words.TryGetValue("myth_secret", out var raw) || !int.TryParse(raw, out var id)) return;
        if (ctx.World.Secrets.FirstOrDefault(s => s.Id == id) is { } secret)
        {
            secret.Revealed = true;
            ctx.World.Feats.Add("myth_true");
        }
    }
}
