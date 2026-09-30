using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>Yearly income, costs, savings and debt. Deliberately simple: it exists to create choices.</summary>
public static class EconomySystem
{
    public static void Update(SimContext ctx, Person p, double savingsFactor)
    {
        int age = p.Age(ctx.Year);
        var c = ctx.Country;

        // Interest first, so this year's money is not affected by the crash twice.
        if (p.Money > 0) p.Money *= (1 + c.SavingsReturn) * savingsFactor;
        else p.Money *= 1 + c.DebtInterest;

        if (age < 18 && p.Activity != Activity.Working) return;

        bool livesAtHome = age < 20 && p.Activity is Activity.School or Activity.Studying or Activity.Unemployed;
        double net = p.Income * (1 - c.TaxRate);
        double saved;
        if (livesAtHome)
        {
            saved = net * 0.5;
        }
        else
        {
            // Everyone needs a basic standard of living. Above it, people save part of the surplus
            // (depending on personality); below it, welfare covers half the gap and the rest becomes debt.
            bool sharesHome = p.PartnerId != null && p.PartnerStatus is PartnerStatus.Cohabiting or PartnerStatus.Married;
            double kids = Kinship.Children(ctx.World, p).Count(k => k.IsAlive && k.Age(ctx.Year) < 18);
            double baseline = c.LivingCostAdult * (sharesHome ? 0.8 : 1) - (p.OwnsHome ? c.LivingCostAdult * 0.12 : 0)
                              + kids * c.LivingCostChild * (sharesHome ? 0.5 : 1);
            double surplus = net - baseline;
            double saveRate = Math.Clamp(0.3 - ctx.Mod(p, "spending"), 0.05, 0.6);
            saved = surplus > 0 ? surplus * saveRate : surplus * 0.5;
        }
        p.Money += ctx.Nominal(saved);

        if (p.Money < -ctx.Nominal(150000)) p.Happiness -= 4;
    }

    /// <summary>Net worth in nominal kronor, including home equity.</summary>
    public static double NetWorth(SimContext ctx, Person p) =>
        p.Money + (p.OwnsHome ? ctx.Nominal(ctx.Country.HomePrice) * 0.5 : 0);

    public static bool CanBuyHome(SimContext ctx, Person p) =>
        !p.OwnsHome && p.Money >= ctx.Nominal(ctx.Country.HomePrice) * 0.15;

    public static void BuyHome(SimContext ctx, Person p)
    {
        p.Money -= ctx.Nominal(ctx.Country.HomePrice) * 0.15;
        p.OwnsHome = true;
        if (p.PartnerId is { } pid && p.PartnerStatus is PartnerStatus.Cohabiting or PartnerStatus.Married)
            ctx.World.Get(pid).OwnsHome = true;
        if (p.InFamily)
            ctx.World.Log($"{p.FirstName} bought a home.", ctx.Importance(false, p), "economy", p.Id);
    }

    /// <summary>Formats a nominal amount the way the UI shows money: "12,500 kr".</summary>
    public static string Format(SimContext ctx, double nominal)
    {
        double rounded = Math.Abs(nominal) >= 10000 ? Math.Round(nominal / 1000) * 1000 : Math.Round(nominal / 100) * 100;
        var s = rounded.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);
        return $"{s} {ctx.Country.CurrencySymbol}";
    }
}
