using OneMoreYear.Simulation;
using OneMoreYear.Simulation.Systems;

/// <summary>--scenarios prints where every test scenario starts: the player, the family and the first events.</summary>
static class ScenarioReport
{
    public static void Run()
    {
        foreach (var sc in GameSession.AvailableScenarios())
        {
            var s = GameSession.NewGame(new NewGameOptions { ScenarioId = sc.Id });
            var w = s.World;
            var p = s.Player;
            Console.WriteLine($"== {sc.Name} ({sc.Id}, seed {sc.Seed} → used {s.World.Seed}) – {s.Year}");
            Console.WriteLine($"   Player: {p.FullName}, {p.Age(s.Year)}, [{string.Join(", ", p.Traits)}], money {s.Money().Money}, {p.Activity}");
            foreach (var r in s.Family().Where(r => r.Id != p.Id).Take(12))
            {
                var x = w.Get(r.Id);
                Console.WriteLine($"   {Kinship.Label(w, p, x),-22} {x.FullName,-24} {x.Age(s.Year),3}  [{string.Join(", ", x.Traits)}]" +
                                  $"{(x.Addiction != null ? $" addiction={x.Addiction}" : "")} money={x.Money:0} {x.Activity}");
            }
            foreach (var e in s.CurrentEvents()) Console.WriteLine($"   Event: {e.Title}");
            foreach (var sec in w.Secrets) Console.WriteLine($"   Secret: {sec.Kind} {w.Get(sec.SubjectId).FirstName} -> {w.TryGet(sec.VictimId)?.FirstName ?? w.TryGet(sec.OtherId)?.FirstName}");
            Console.WriteLine();
        }
    }
}
