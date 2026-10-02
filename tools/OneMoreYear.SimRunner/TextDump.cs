using OneMoreYear.Simulation;

/// <summary>
/// --textdump=games: plays long games in both countries and prints every text the player would read
/// (events, choices, outcomes, the chronicle, the epilogue), one per line, for proofreading.
/// </summary>
static class TextDump
{
    public static void Run(int games)
    {
        var seen = new HashSet<string>();
        for (int g = 0; g < games; g++)
        {
            string country = g % 2 == 0 ? "sweden" : "usa";
            var s = GameSession.NewGame(new NewGameOptions { Seed = (ulong)(900 + g), StartYear = 1950 + (g % 5) * 10, CountryId = country });
            var bot = new AutoPlayer((ulong)g);
            bot.OnText = t => { if (!string.IsNullOrWhiteSpace(t) && seen.Add(t)) Console.WriteLine($"[{s.Year} {s.Country.Id}] {t}"); };
            for (int i = 0; i < 200 && bot.PlayYear(s); i++) { }
            foreach (var line in s.Chronicle(1))
            {
                string t = s.Annotate(line.Text, line.PersonIds);
                if (seen.Add(t)) Console.WriteLine($"[{line.Year} chronicle] {t}");
            }
            Console.WriteLine($"[epilogue] {s.Epilogue().Words}");
        }
    }
}
