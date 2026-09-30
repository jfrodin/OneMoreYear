using OneMoreYear.Simulation.Core;

namespace OneMoreYear.Simulation.Systems;

/// <summary>One year on the markets, as nominal rates (0.07 = +7 %).</summary>
public sealed record MarketYear(int Year, double Inflation, double Bank, double Debt, double MortgageRate, double Stocks, double Housing);

/// <summary>
/// The economy everyone shares: inflation from the country's price index, and stock and housing
/// markets that go up most years and crash in the historical crises. Each year is drawn from a
/// random generator seeded by the world seed and the year, so it never disturbs the simulation's
/// own random numbers – and the same seed always has the same market.
/// </summary>
public static class Market
{
    public static MarketYear For(SimContext ctx, int year)
    {
        var c = ctx.Country;
        double inflation = ctx.MoneyIndex(year) / ctx.MoneyIndex(year - 1) - 1;
        var rng = new SimRandom(ctx.World.Seed * 0xD6E8FEB86659FD93UL ^ (ulong)year * 0x9E3779B97F4A7C15UL);
        double stocksReal = rng.Gaussian(c.MarketRealReturn, c.MarketVolatility);
        double housingReal = rng.Gaussian(c.HousingRealGrowth, c.HousingVolatility);
        foreach (var crisis in c.HistoricalEvents.Where(h => h.Year == year && h.SavingsFactor < 1))
        {
            stocksReal = Math.Min(stocksReal, (crisis.SavingsFactor - 1) * 1.8);
            housingReal = Math.Min(housingReal, (crisis.SavingsFactor - 1) * 0.8);
        }
        double Nominal(double real) => (1 + inflation) * (1 + real) - 1;
        return new MarketYear(year, inflation,
            Bank: Math.Max(0.002, Nominal(c.SavingsRealReturn)),
            Debt: Nominal(c.DebtRealInterest),
            MortgageRate: Math.Max(0.01, Nominal(c.MortgageRealRate)),
            Stocks: Math.Max(-0.6, Nominal(stocksReal)),
            Housing: Nominal(housingReal));
    }

    /// <summary>A single company's shares: the market plus a lot of luck of its own – and sometimes bankruptcy.</summary>
    public static double StockReturn(SimContext ctx, MarketYear m, int personId)
    {
        var rng = new SimRandom(ctx.World.Seed ^ (ulong)m.Year * 0xBF58476D1CE4E5B9UL ^ (ulong)personId * 0x94D049BB133111EBUL);
        if (rng.Chance(0.03)) return -1;
        return Math.Max(-0.9, m.Stocks + rng.Gaussian(0, 0.3));
    }
}
