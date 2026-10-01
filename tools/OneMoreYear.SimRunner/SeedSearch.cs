using OneMoreYear.Simulation;
using OneMoreYear.Simulation.Systems;

/// <summary>--find-seed=scenarioId: lists seeds where the scenario starts with the family it promises.</summary>
static class SeedSearch
{
    public static void Run(string scenarioId, int count)
    {
        for (ulong seed = 1; seed <= (ulong)count; seed++)
        {
            var s = GameSession.NewGame(new NewGameOptions { ScenarioId = scenarioId, Seed = seed });
            var w = s.World;
            var p = s.Player;
            int kids = Kinship.Children(w, p).Count(k => k.IsAlive);
            int grandkids = Kinship.Grandchildren(w, p).Count(k => k.IsAlive);
            var grandparents = Kinship.Grandparents(w, p).Count(g => g.IsAlive);
            var origin = w.Secrets.FirstOrDefault(x => x.Kind == "origin");
            var child = w.TryGet(origin?.ChildId);
            var mum = w.TryGet(origin?.VictimId);
            int older = child == null || mum == null ? 0 : Kinship.Children(w, mum).Count(k => k.BirthYear < child.BirthYear);
            string story = mum == null ? "" : $"mother {mum.Age(s.Year)} (had her at {child!.BirthYear - mum.BirthYear}), older siblings={older}, ";
            Console.WriteLine($"seed {seed,4}: {story}{p.FullName} ({p.Sex}), {p.Age(s.Year)}, gen {p.Generation}, origin={(origin != null ? $"{child!.FirstName} ({child.Sex}, {child.Age(s.Year)}), legal father alive={w.TryGet(origin.OtherId)?.IsAlive}" : "-")}, partner={w.TryGet(p.PartnerId)?.FirstName ?? "-"} " +
                              $"kids={kids} grandkids={grandkids} grandparents={grandparents} job={p.Activity} prison={p.CriminalRecord.Count}");
        }
    }
}

/// <summary>--dating: how often a player who only answers events has a partner and children at 35.</summary>
static class DatingReport
{
    public static void Run()
    {
        Console.WriteLine("Random answers:");
        Run(null);
        Console.WriteLine("Says yes to love (first choice in love events):");
        string[] love = { "A proposal", "Your place or mine?", "Baby talk", "A spark", "Saturday dance", "A dinner party", "Stuck", "A match", "Someone likes you", "After work" };
        Run(ev => love.Contains(ev.Title) ? ev.Choices[0] : null);
    }

    private static void Run(Func<EventView, ChoiceView?>? prefer)
    {
        int partner = 0, married = 0, kids = 0, n = 40, everDated = 0, cohabit = 0;
        var seen = new Dictionary<string, int>();
        var opinions = new List<double>();
        var parts = new List<(double C, double T, double R, double B, double EF)>();
        int partnerYears = 0, singleAdultYears = 0, breakups = 0;
        string[] watch = { "A proposal", "Your place or mine?", "A difficult conversation", "Baby talk" };
        for (ulong seed = 1; seed <= (ulong)n; seed++)
        {
            var s = GameSession.NewGame(new NewGameOptions { Seed = seed, StartYear = 1960 });
            var bot = new AutoPlayer(seed, useActions: false);
            bot.Prefer = prefer;
            bot.OnText = t => { if (watch.Contains(t)) seen[t] = seen.GetValueOrDefault(t) + 1; };
            int playerId = s.Player.Id;
            int? lastPartner = null;
            while (s.Player.Id == playerId && s.Player.Age(s.Year) < 35 && bot.PlayYear(s))
            {
                var pl = s.World.Get(playerId);
                if (pl.Age(s.Year) < 18) continue;
                if (pl.PartnerId is { } pid)
                {
                    partnerYears++;
                    opinions.Add(s.World.Opinion(pid, playerId));
                    if (s.World.FindRel(pid, playerId) is { } r) parts.Add((r.Closeness, r.Trust, r.Respect, r.Bitterness, r.Envy + r.Fear));
                }
                else singleAdultYears++;
                if (lastPartner != null && pl.PartnerId != lastPartner) breakups++;
                lastPartner = pl.PartnerId;
            }
            var p = s.World.Get(playerId);
            if (p.PartnerId != null) partner++;
            if (p.PartnerStatus == OneMoreYear.Simulation.Model.PartnerStatus.Married) married++;
            if (p.PartnerStatus == OneMoreYear.Simulation.Model.PartnerStatus.Cohabiting) cohabit++;
            if (p.ChildIds.Count > 0) kids++;
            if (p.PartnerId != null || p.ExPartnerIds.Count > 0) everDated++;
        }
        Console.WriteLine($"At 35 (of {n}): partner {partner} (cohabiting {cohabit}, married {married}), children {kids}, ever had a partner {everDated}");
        Console.WriteLine($"Adult years 18-35: with partner {partnerYears}, single {singleAdultYears}, partner changes {breakups}");
        if (opinions.Count > 0)
            Console.WriteLine($"Partner's opinion of player: avg {opinions.Average():F0}, >30 in {opinions.Count(o => o > 30) * 100 / opinions.Count}% of years, <20 in {opinions.Count(o => o < 20) * 100 / opinions.Count}%");
        if (parts.Count > 0) Console.WriteLine($"  closeness {parts.Average(x => x.C):F0}, trust {parts.Average(x => x.T):F0}, respect {parts.Average(x => x.R):F0}, bitterness {parts.Average(x => x.B):F0}, envy+fear {parts.Average(x => x.EF):F0}");
        foreach (var t in watch) Console.WriteLine($"  seen '{t}': {seen.GetValueOrDefault(t)}");
    }
}
