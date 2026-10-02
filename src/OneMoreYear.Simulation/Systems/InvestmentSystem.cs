using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// The player's investments: one holding per fund or company (country content). Every asset's yearly
/// return comes from its own random generator, seeded by the world and the year, so the same seed
/// always has the same market and buying or selling never disturbs the rest of the world.
/// Other people keep the simpler Funds and Stocks totals in <see cref="EconomySystem"/>.
/// </summary>
public static class InvestmentSystem
{
    /// <summary>The youngest age that can invest (a first fund with the parents' help).</summary>
    public const int MinAge = 15;

    public static AssetDef? Asset(SimContext ctx, string id) => ctx.Country.Investments.FirstOrDefault(a => a.Id == id);

    private static SimRandom Rng(SimContext ctx, AssetDef a, int year, ulong salt) =>
        new(ctx.World.Seed ^ StableHash.Of(a.Id) * 0x9E3779B97F4A7C15UL ^ (ulong)year * 0xBF58476D1CE4E5B9UL ^ salt);

    /// <summary>Did the company go bankrupt in this year? Funds never do.</summary>
    public static bool BankruptIn(SimContext ctx, AssetDef a, int year) =>
        a.Bankruptcy > 0 && Rng(ctx, a, year, 0x51UL).Chance(a.Bankruptcy);

    /// <summary>A company that has gone bankrupt (this year or before) is gone for good.</summary>
    public static bool IsGone(SimContext ctx, AssetDef a, int year)
    {
        for (int y = Math.Max(a.MinYear, ctx.World.StartYear - 60); y <= year; y++)
            if (BankruptIn(ctx, a, y)) return true;
        return false;
    }

    public static bool Available(SimContext ctx, AssetDef a) => a.MinYear <= ctx.Year && !IsGone(ctx, a, ctx.Year);

    public static IEnumerable<AssetDef> AvailableAssets(SimContext ctx) => ctx.Country.Investments.Where(a => Available(ctx, a));

    /// <summary>The asset's return in a year (0.1 = +10 %): the bank, plus beta times the market's excess, plus its own luck.</summary>
    public static double Return(SimContext ctx, AssetDef a, int year)
    {
        if (BankruptIn(ctx, a, year)) return -1;
        var m = Market.For(ctx, year);
        var rng = Rng(ctx, a, year, 0x7AUL);
        double luck = rng.Gaussian(0, a.Spread);
        double r = a.Shocks.TryGetValue(year, out var shock)
            ? shock + luck * 0.3
            : m.Bank + a.Beta * (m.Stocks - m.Bank) + luck;
        return Math.Max(-0.95, r);
    }

    /// <summary>Total return over the last <paramref name="years"/> years, or null if the asset is younger.</summary>
    public static double? Trailing(SimContext ctx, AssetDef a, int years)
    {
        if (ctx.Year - years < a.MinYear) return null;
        double total = 1;
        for (int y = ctx.Year - years + 1; y <= ctx.Year; y++) total *= 1 + Return(ctx, a, y);
        return total - 1;
    }

    /// <summary>"Low", "Medium", "High" or "Very high", from how much the asset swings.</summary>
    public static string Risk(AssetDef a)
    {
        double swing = Math.Abs(a.Beta) * 0.16 + a.Spread + a.Bankruptcy * 2 + (a.Shocks.Count > 0 ? 0.05 : 0);
        return swing switch { < 0.08 => "Low", < 0.22 => "Medium", < 0.4 => "High", _ => "Very high" };
    }

    public static double Value(Person p) => p.Holdings.Sum(h => h.Value);

    /// <summary>The year's returns, dividends and bankruptcies for one person's holdings.</summary>
    public static void Update(SimContext ctx, Person p)
    {
        foreach (var h in p.Holdings.ToList())
        {
            var a = Asset(ctx, h.AssetId);
            if (a == null)
            {
                // The asset no longer exists in the content: it is paid out.
                p.Money += h.Value;
                p.Holdings.Remove(h);
                continue;
            }
            double r = Return(ctx, a, ctx.Year);
            h.LastReturn = r;
            h.Value *= 1 + r;
            h.LastDividend = 0;
            if (r <= -1)
            {
                p.Holdings.Remove(h);
                if (p.Id == ctx.World.PlayerId || p.InFamily)
                    ctx.World.Log($"{a.Name} went bankrupt. {p.FirstName} lost {EconomySystem.Format(ctx, h.Invested)}.", ctx.Importance(true, p), "economy", p.Id);
                continue;
            }
            if (a.Kind == "shares" && a.Dividend > 0)
            {
                double dividend = h.Value * a.Dividend;
                h.LastDividend = dividend;
                p.Money += dividend;
                EconomySystem.Record(ctx, p, $"Dividend from {a.Name}", dividend);
            }
        }
    }

    /// <summary>Buys for a nominal amount of the player's savings. Returns what happened, for the UI.</summary>
    public static string Buy(SimContext ctx, Person p, string assetId, double amount)
    {
        var a = Asset(ctx, assetId);
        if (a == null || !Available(ctx, a)) return "That is not for sale.";
        if (p.Age(ctx.Year) < MinAge) return $"You have to be {MinAge} to invest.";
        amount = Math.Min(amount, Math.Max(0, p.Money));
        if (amount < 1) return "You have no savings to invest.";
        p.Money -= amount;
        var h = p.Holdings.FirstOrDefault(x => x.AssetId == assetId);
        if (h == null) p.Holdings.Add(h = new Holding { AssetId = assetId, SinceYear = ctx.Year });
        h.Invested += amount;
        h.Value += amount;
        EconomySystem.Record(ctx, p, $"Bought {a.Name}", -amount);
        return $"You put {EconomySystem.Format(ctx, amount)} into {a.Name}.";
    }

    /// <summary>Sells a share (0–1) of one holding. Returns the amount.</summary>
    public static double Sell(SimContext ctx, Person p, string assetId, double share)
    {
        var h = p.Holdings.FirstOrDefault(x => x.AssetId == assetId);
        if (h == null) return 0;
        share = Math.Clamp(share, 0, 1);
        double amount = h.Value * share;
        h.Invested *= 1 - share;
        h.Value -= amount;
        if (h.Value < 1 || share >= 1) p.Holdings.Remove(h);
        p.Money += amount;
        EconomySystem.Record(ctx, p, $"Sold {Asset(ctx, assetId)?.Name ?? "investments"}", amount);
        return amount;
    }

    /// <summary>Sells evenly from all holdings until <paramref name="need"/> is raised (or nothing is left).</summary>
    public static double Raise(SimContext ctx, Person p, double need)
    {
        double total = Value(p);
        if (total < 1 || need <= 0) return 0;
        double share = Math.Min(1, need / total);
        double raised = 0;
        foreach (var h in p.Holdings.ToList())
        {
            double amount = h.Value * share;
            h.Invested *= 1 - share;
            h.Value -= amount;
            raised += amount;
            if (h.Value < 1) p.Holdings.Remove(h);
        }
        p.Money += raised;
        return raised;
    }

    /// <summary>The fund (or company) the simple "invest" effect and old saves use for a kind.</summary>
    public static AssetDef? DefaultFor(SimContext ctx, string kind)
    {
        var available = AvailableAssets(ctx).ToList();
        return kind is "stocks" or "shares"
            ? available.FirstOrDefault(a => a.Kind == "shares")
            : available.FirstOrDefault(a => a.Kind == "fund") ?? available.FirstOrDefault();
    }

    /// <summary>A player who still has the old simple funds and shares (an old save, an heir) gets real holdings for them.</summary>
    public static void ConvertSimple(SimContext ctx, Person p)
    {
        foreach (var (kind, amount) in new[] { ("fund", p.Funds), ("shares", p.Stocks) })
        {
            if (amount < 1 || DefaultFor(ctx, kind) is not { } a) continue;
            var h = p.Holdings.FirstOrDefault(x => x.AssetId == a.Id);
            if (h == null) p.Holdings.Add(h = new Holding { AssetId = a.Id, SinceYear = ctx.Year });
            h.Invested += amount;
            h.Value += amount;
            if (kind == "fund") p.Funds = 0; else p.Stocks = 0;
        }
    }
}
