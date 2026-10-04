using OneMoreYear.Simulation;
using OneMoreYear.Simulation.Content;

/// <summary>
/// --repeats[=games]: how much of the second and third life in a family is moments the player has
/// already read in an earlier life (random events only). The release goal is three generations
/// without feeling that things repeat.
/// </summary>
static class RepeatReport
{
    public static void Run(int games)
    {
        var content = ContentDb.Embedded;
        var shareByLife = new List<double>[4] { new(), new(), new(), new() };
        var repeated = new Dictionary<string, int>();
        int[] starts = { 1950, 1960, 1970, 1980, 1990, 2000 };
        for (int g = 0; g < games; g++)
        {
            var s = GameSession.NewGame(new NewGameOptions { Seed = (ulong)(5000 + g), StartYear = starts[g % starts.Length] });
            var bot = new AutoPlayer((ulong)g);
            var lives = new List<List<string>> { new() };
            int playerId = s.Player.Id;
            for (int year = 0; year < 200 && lives.Count <= 4; year++)
            {
                if (s.Player.Id != playerId) { lives.Add(new()); playerId = s.Player.Id; }
                foreach (var p in s.World.PendingEvents)
                    if (content.Events.TryGetValue(p.EventId, out var def) && def.Trigger == "random") lives[^1].Add(p.EventId);
                if (!bot.PlayYear(s)) break;
            }
            var before = new HashSet<string>();
            for (int i = 0; i < lives.Count && i < 4; i++)
            {
                var life = lives[i];
                if (i > 0 && life.Count >= 20)
                {
                    shareByLife[i].Add(life.Count(before.Contains) / (double)life.Count);
                    foreach (var id in life.Where(before.Contains).Distinct()) repeated[id] = repeated.GetValueOrDefault(id) + 1;
                }
                before.UnionWith(life);
            }
            Console.Error.Write($"\r{g + 1}/{games} games");
        }
        Console.Error.WriteLine();
        for (int i = 1; i < 4; i++)
            if (shareByLife[i].Count > 0)
                Console.WriteLine($"Life {i + 1}: {shareByLife[i].Average() * 100:0}% of its events were already read in an earlier life ({shareByLife[i].Count} lives).");
        Console.WriteLine("\nMost often read again in a later life:");
        foreach (var (id, n) in repeated.OrderByDescending(kv => kv.Value).Take(40))
            Console.WriteLine($"  {id,-36} {n}");
    }
}
