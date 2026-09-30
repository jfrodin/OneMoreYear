using OneMoreYear.Simulation;
using OneMoreYear.Simulation.Systems;

/// <summary>Debug aid: --load=save.json --who=Name prints everything about matching people.</summary>
static class Inspect
{
    public static void Run(string path, string who)
    {
        var s = GameSession.Load(File.ReadAllText(path));
        var w = s.World;
        Console.WriteLine($"Year {s.Year}, player {s.Player.FullName} (#{s.Player.Id})");
        foreach (var p in w.People.Where(p => p.FullName.Contains(who, StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine($"#{p.Id} {p.FullName} b.{p.BirthYear} alive={p.IsAlive} inFamily={p.InFamily} blood={p.IsBlood} " +
                              $"label={Kinship.Label(w, s.Player, p)} partner={w.TryGet(p.PartnerId)?.FullName} status={p.PartnerStatus} " +
                              $"exes=[{string.Join(", ", p.ExPartnerIds.Select(id => w.Get(id).FullName))}]");
            foreach (var l in w.Chronicle.Where(l => l.PersonIds.Contains(p.Id)).TakeLast(8)) Console.WriteLine($"   {l.Year} {l.Text}");
            foreach (var sec in w.Secrets.Where(x => x.SubjectId == p.Id || x.OtherId == p.Id))
                Console.WriteLine($"   secret {sec.Kind} {w.Get(sec.SubjectId).FullName} + {w.TryGet(sec.OtherId)?.FullName} active={sec.Active} revealed={sec.Revealed}");
        }
    }
}
