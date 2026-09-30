using OneMoreYear.Simulation;
using OneMoreYear.Simulation.Systems;

/// <summary>--wealth: how wealth is spread at the end of a run, in 2020-kronor (for balancing).</summary>
static class WealthReport
{
    public static void Run(GameSession s)
    {
        var ctx = s.Ctx;
        var adults = s.World.People.Where(p => p.IsAlive && p.Age(s.Year) >= 25).ToList();
        if (adults.Count == 0) return;
        double Real(double v) => ctx.Real(v) / 1e6;
        var worth = adults.Select(p => Real(EconomySystem.NetWorth(ctx, p))).OrderBy(v => v).ToList();
        Console.WriteLine($"Wealth {s.Year} (million 2020-kr, {adults.Count} adults): median {worth[worth.Count / 2]:0.0}, " +
                          $"90th {worth[(int)(worth.Count * 0.9)]:0.0}, max {worth[^1]:0.0}");
        foreach (var p in adults.OrderByDescending(p => EconomySystem.NetWorth(ctx, p)).Take(5))
            Console.WriteLine($"  {p.FullName,-24} {p.Age(s.Year),3}  cash {Real(p.Money),7:0.0}  funds {Real(p.Funds),7:0.0}  shares {Real(p.Stocks),7:0.0}  " +
                              $"home {Real(p.HomeValue),6:0.0}  loan {Real(p.Mortgage),6:0.0}  income {p.Income / 1e3:0}k  {CareerSystem.ActivityText(ctx, p)}");
        var peak = s.World.People.OrderByDescending(p => p.PeakNetWorth).First();
        Console.WriteLine($"  Highest ever: {peak.FullName} {EconomySystem.Format(ctx, peak.PeakNetWorth)} (nominal)");
    }
}
