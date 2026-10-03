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
        Console.WriteLine($"Wealth {s.Year} (million, 2020 money, {adults.Count} adults): median {worth[worth.Count / 2]:0.0}, " +
                          $"90th {worth[(int)(worth.Count * 0.9)]:0.0}, max {worth[^1]:0.0}");
        var lets = s.World.Rentals.Where(r => r.IsHeld).ToList();
        Console.WriteLine($"  Homes let: {lets.Count} held, {s.World.Rentals.Count - lets.Count} sold; value {lets.Sum(r => r.Value) / 1e6:0.0} M, loans {lets.Sum(r => r.Loan) / 1e6:0.0} M (2020 kr); last year net {lets.Sum(r => r.LastNet) / 1e3:0} k; passed down {lets.Count(r => r.Owners.Count > 1)}");
        var incomes = adults.Where(p => p.Activity == OneMoreYear.Simulation.Model.Activity.Working).Select(p => p.Income / 1e3).OrderBy(v => v).ToList();
        if (incomes.Count > 0)
        {
            Console.WriteLine("  Jobs: " + string.Join(", ", adults.Where(p => p.Activity == OneMoreYear.Simulation.Model.Activity.Working).GroupBy(p => p.OccupationId).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")));
            Console.WriteLine($"  Working incomes (thousand, 2020 money): 10th {incomes[(int)(incomes.Count * 0.1)]:0}, median {incomes[incomes.Count / 2]:0}, " +
                              $"90th {incomes[(int)(incomes.Count * 0.9)]:0}, max {incomes[^1]:0}  · in debt: {adults.Count(p => EconomySystem.NetWorth(ctx, p) < 0)} of {adults.Count}");
        }
        foreach (var p in adults.OrderByDescending(p => EconomySystem.NetWorth(ctx, p)).Take(5))
            Console.WriteLine($"  {p.FullName,-24} {p.Age(s.Year),3}  cash {Real(p.Money),7:0.0}  funds {Real(p.Funds),7:0.0}  shares {Real(p.Stocks),7:0.0}  " +
                              $"home {Real(p.HomeValue),6:0.0}  loan {Real(p.Mortgage),6:0.0}  income {p.Income / 1e3:0}k  {CareerSystem.ActivityText(ctx, p)}");
        Console.WriteLine("  Secrets: " + string.Join(", ", s.World.Secrets.GroupBy(x => x.Kind).Select(g => $"{g.Key} {g.Count()}")) +
                          $"  · predatory people: {s.World.People.Count(p => p.Traits.Contains("predatory"))}");
        var fam = s.World.People.Where(x => x.IsAlive && x.InFamily && x.Age(s.Year) >= 30).ToList();
        var pairs = fam.SelectMany(c => Kinship.Parents(s.World, c).Where(pp => pp.IsAlive).Select(pp => s.World.Opinion(pp.Id, c.Id))
            .Concat(Kinship.Siblings(s.World, c).Where(sb => sb.IsAlive).Select(sb => s.World.Opinion(c.Id, sb.Id)))).ToList();
        Console.WriteLine($"  Parents→adult children and siblings ({pairs.Count}): " +
                          string.Join(", ", pairs.GroupBy(RelationshipSystem.OpinionLabel).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}")));
        int years = s.Year - s.World.StartYear, big = s.World.Chronicle.Where(l => YearReport.IsFrontPage(new ChronicleLine(l.Year, l.Text, l.Importance, l.Category, l.PersonIds))).Select(l => l.Year).Distinct().Count();
        Console.WriteLine($"  Front-page years: {big} of {years} ({100 * big / Math.Max(1, years)} %)");
        Console.WriteLine("  Ailments now: " + string.Join(", ", s.World.People.Where(p => p.IsAlive).SelectMany(p => p.Ailments.Keys).GroupBy(k => k).Select(g => $"{g.Key} {g.Count()}"))
                          + $"  · ever (chronicle): depression {s.World.Chronicle.Count(l => l.Category == "health" && l.Text.Contains("depression") && !l.Text.Contains(" out of"))}, dementia {s.World.Chronicle.Count(l => l.Text.Contains("started to forget"))}, burnout {s.World.Chronicle.Count(l => l.Text.Contains("struck by burnout"))}");
        var peak = s.World.People.OrderByDescending(p => p.PeakNetWorth).First();
        Console.WriteLine($"  Highest ever: {peak.FullName} {EconomySystem.Format(ctx, peak.PeakNetWorth)} (nominal)");
    }
}
