using OneMoreYear.Simulation;

/// <summary>
/// --achievements[=games]: plays long games with the bot and counts how many families earn each
/// achievement, for tuning: common ones should come to most families, legendary ones almost never by chance.
/// </summary>
static class AchievementReport
{
    private static bool IsDescendant(GameSession s, OneMoreYear.Simulation.Model.Person p, int ancestorId) =>
        p.ParentIds.Any(id => id == ancestorId || IsDescendant(s, s.World.Get(id), ancestorId));

    public static void Run(int games)
    {
        var all = GameSession.AllAchievements();
        var count = all.ToDictionary(a => a.Id, _ => 0);
        int[] starts = { 1950, 1960, 1970, 1980 };
        int maxDesc = 0, maxAge = 0, maxKids = 0, gens = 0, anyOldest = 0, over80 = 0, over90 = 0, died = 0; double maxWorth = 0; var byDecade = new int[12];
        var legacyCount = new Dictionary<string, int>();
        var examples = new List<string>();
        var traitEver = new Dictionary<string, int>();
        var meterSum = new Dictionary<string, double>();
        for (int g = 0; g < games; g++)
        {
            var s = GameSession.NewGame(new NewGameOptions { Seed = (ulong)(5000 + g), StartYear = starts[g % starts.Length] });
            var bot = new AutoPlayer((ulong)g);
            var have = new HashSet<string>();
            for (int year = 0; year < 220; year++)
            {
                foreach (var a in s.NewAchievements(have)) { have.Add(a.Id); count[a.Id]++; }
                foreach (var e in s.World.PendingEvents.Where(e => e.EventId.StartsWith("legacy_") || e.EventId == "family_myth"))
                {
                    legacyCount[e.EventId] = legacyCount.GetValueOrDefault(e.EventId) + 1;
                    if (examples.Count(x => x.StartsWith(e.EventId)) < 2)
                    {
                        var view = s.DescribeEvent(e);
                        examples.Add($"{e.EventId} ({s.Year}): {view.Text}");
                    }
                }
                if (!bot.PlayYear(s)) break;
            }
            foreach (var a in s.NewAchievements(have)) { have.Add(a.Id); count[a.Id]++; }
            var played = s.World.PlayedIds.Select(s.World.Get).ToList();
            maxDesc = Math.Max(maxDesc, s.World.People.Count(p => p.IsAlive && IsDescendant(s, p, s.World.PlayedIds[0])));
            maxWorth = Math.Max(maxWorth, played.Max(p => p.PeakRefWorth));
            maxAge = Math.Max(maxAge, played.Max(p => (p.DeathYear ?? s.Year) - p.BirthYear));
            var deadOld = s.World.People.Where(p => !p.IsAlive && p.BirthYear >= s.World.StartYear - 20 && p.BirthYear <= s.World.StartYear + 60).ToList();
            foreach (var p in deadOld) if (p.DeathYear - p.BirthYear >= 20) byDecade[Math.Min((p.DeathYear!.Value - p.BirthYear) / 10, 11)]++;
            if (deadOld.Count > 0) { anyOldest = Math.Max(anyOldest, deadOld.Max(p => p.DeathYear!.Value - p.BirthYear)); over80 += deadOld.Count(p => p.DeathYear - p.BirthYear >= 80); over90 += deadOld.Count(p => p.DeathYear - p.BirthYear >= 90); died += deadOld.Count(p => p.DeathYear - p.BirthYear >= 20); }
            maxKids = Math.Max(maxKids, played.Max(p => p.ChildIds.Count));
            gens = Math.Max(gens, played.Max(p => p.Generation) - played.Min(p => p.Generation) + 1);
            foreach (var t in s.Content.FamilyTraits.Values) if (s.World.Chronicle.Any(l => l.Text.Contains($"has become {t.Name.ToLowerInvariant()}"))) traitEver[t.Id] = traitEver.GetValueOrDefault(t.Id) + 1;
            foreach (var (m, v) in OneMoreYear.Simulation.Systems.ReputationSystem.Meters(s.Ctx)) meterSum[m] = meterSum.GetValueOrDefault(m) + v;
            Console.Error.Write($"\r{g + 1}/{games} families");
        }
        Console.Error.WriteLine();
        Console.WriteLine($"Achievements over {games} families played by the bot (up to 220 years):");
        Console.WriteLine($"Best seen: {maxDesc} living descendants of the founder, {gens} generations played, richest {maxWorth / 1e6:0.0} M ref, oldest {maxAge}, most children {maxKids}; anyone oldest {anyOldest}, of adults who died: {over80 * 100 / Math.Max(1, died)}% reached 80, {over90 * 100.0 / Math.Max(1, died):0.0}% reached 90");
        Console.WriteLine("Adult deaths by age (cohorts born from start minus 20 to start plus 60): " + string.Join(", ", Enumerable.Range(2, 10).Select(d => $"{d * 10}s {byDecade[d] * 100.0 / Math.Max(1, byDecade.Sum()):0}%")));
        foreach (var a in all) Console.WriteLine($"  {a.Tier,-9} {a.Id,-24} {count[a.Id] * 100 / games,3}%");
        Console.WriteLine("Long-play events: " + string.Join(", ", legacyCount.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}")));
        foreach (var x in examples) Console.WriteLine("  " + x);
        Console.WriteLine("Family meters at the end (average): " + string.Join(", ", meterSum.Select(kv => $"{kv.Key} {kv.Value / games:0.0}")));
        Console.WriteLine("Families that ever got a trait: " + string.Join(", ", traitEver.Select(kv => $"{kv.Key} {kv.Value * 100 / games}%")));
    }
}
