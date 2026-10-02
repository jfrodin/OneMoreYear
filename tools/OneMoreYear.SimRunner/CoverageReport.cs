using OneMoreYear.Simulation;
using OneMoreYear.Simulation.Content;

/// <summary>
/// --coverage[=games]: plays many games from different decades and counts how often each event reaches
/// the player. Events that never appear are wasted writing; events that come back in almost every life
/// get tiresome. Prints both lists, and the events per year of life.
/// </summary>
static class CoverageReport
{
    public static void Run(int games, string countryId = "sweden")
    {
        var content = ContentDb.Embedded;
        var seen = content.Events.Keys.ToDictionary(k => k, _ => 0);
        var livesWith = content.Events.Keys.ToDictionary(k => k, _ => 0);
        int lives = 0, playerYears = 0, eventsShown = 0;
        var bandEvents = new int[10];
        var bandYears = new int[10];
        int[] starts = { 1950, 1960, 1970, 1980, 1990, 2000 };

        for (int g = 0; g < games; g++)
        {
            var s = GameSession.NewGame(new NewGameOptions { Seed = (ulong)(1000 + g), StartYear = starts[g % starts.Length], CountryId = countryId });
            var bot = new AutoPlayer((ulong)g);
            var thisLife = new HashSet<string>();
            int playerId = s.Player.Id;
            for (int year = 0; year < 110; year++)
            {
                if (s.Player.Id != playerId)
                {
                    foreach (var id in thisLife) livesWith[id]++;
                    thisLife.Clear();
                    playerId = s.Player.Id;
                    lives++;
                }
                foreach (var p in s.World.PendingEvents)
                {
                    seen[p.EventId]++;
                    thisLife.Add(p.EventId);
                    eventsShown++;
                    bandEvents[Math.Min(s.Player.Age(s.Year) / 10, 9)]++;
                }
                playerYears++;
                bandYears[Math.Min(s.Player.Age(s.Year) / 10, 9)]++;
                if (!bot.PlayYear(s)) break;
            }
            foreach (var id in thisLife) livesWith[id]++;
            lives++;
            Console.Error.Write($"\r{g + 1}/{games} games");
        }
        Console.Error.WriteLine();

        Console.WriteLine($"{games} games, {lives} lives, {playerYears} player years, {eventsShown} events shown ({(double)eventsShown / playerYears:0.00} per year).");
        Console.WriteLine("Events per year by age: " + string.Join(", ", Enumerable.Range(0, 10).Where(b => bandYears[b] > 0).Select(b => $"{b * 10}s {(double)bandEvents[b] / bandYears[b]:0.0}")));
        Console.WriteLine("Random events open by age (pool size): " + string.Join(", ", new[] { 3, 8, 13, 17, 22, 27, 35, 45, 55, 65, 75, 85 }.Select(a => $"{a}: {content.Events.Values.Count(e => e.Trigger == "random" && (e.Countries.Count == 0 || e.Countries.Contains(countryId)) && (e.Conditions?.MinAge ?? 0) <= a && (e.Conditions?.MaxAge ?? 200) >= a)}")));
        Console.WriteLine();
        var never = content.Events.Values.Where(e => seen[e.Id] == 0 && e.Trigger is "random").OrderBy(e => e.Id).ToList();
        Console.WriteLine($"Random events that never appeared ({never.Count}):");
        foreach (var e in never) Console.WriteLine($"  {e.Id}  {Conditions(e)}");
        var neverSituation = content.Events.Values.Where(e => seen[e.Id] == 0 && e.Trigger == "situation").OrderBy(e => e.Id).ToList();
        Console.WriteLine($"Situations that never appeared ({neverSituation.Count}): {string.Join(", ", neverSituation.Select(e => e.Id))}");
        Console.WriteLine();
        var local = content.Events.Values.Where(e => e.Countries.Contains(countryId)).OrderBy(e => e.Id).ToList();
        if (local.Count > 0)
        {
            Console.WriteLine($"Events only in {countryId} (lives that saw it, and total):");
            foreach (var e in local) Console.WriteLine($"  {e.Id,-34} {livesWith[e.Id] * 100 / Math.Max(1, lives),3}% of lives, {seen[e.Id]} times");
            Console.WriteLine();
        }
        Console.WriteLine("Most common (share of lives that saw it at least once, and total):");
        foreach (var (id, n) in livesWith.Where(kv => content.Events[kv.Key].Trigger == "random").OrderByDescending(kv => kv.Value).Take(25))
            Console.WriteLine($"  {id,-34} {n * 100 / Math.Max(1, lives),3}% of lives, {seen[id]} times");
    }

    private static string Conditions(EventDef e)
    {
        var c = e.Conditions;
        var parts = new List<string>();
        if (c?.MinAge != null || c?.MaxAge != null) parts.Add($"age {c.MinAge}-{c.MaxAge}");
        if (c?.MinYear != null || c?.MaxYear != null) parts.Add($"year {c.MinYear}-{c.MaxYear}");
        if (c?.Flags is { Count: > 0 }) parts.Add("flags " + string.Join(",", c.Flags));
        if (c?.JobTags is { Count: > 0 }) parts.Add("jobs " + string.Join(",", c.JobTags));
        if (c?.Activity != null) parts.Add($"activity {c.Activity}");
        if (e.Target != null) parts.Add($"target {e.Target.Role}");
        if (e.Content.Count > 0) parts.Add("content " + string.Join(",", e.Content));
        parts.Add($"w{e.Weight} cd{e.Cooldown}");
        return string.Join("; ", parts);
    }
}
