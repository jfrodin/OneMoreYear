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
            Console.WriteLine($"seed {seed,4}: {p.FullName} ({p.Sex}), {p.Age(s.Year)}, gen {p.Generation}, origin={(origin != null ? $"{child!.FirstName} ({child.Sex}, {child.Age(s.Year)}), legal father alive={w.TryGet(origin.OtherId)?.IsAlive}" : "-")}, partner={w.TryGet(p.PartnerId)?.FirstName ?? "-"} " +
                              $"kids={kids} grandkids={grandkids} grandparents={grandparents} job={p.Activity} prison={p.CriminalRecord.Count}");
        }
    }
}
