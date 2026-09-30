using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Yearly income, costs, savings and debt. Deliberately simple – it exists to create choices – but
/// every change to the player's money is recorded in <see cref="World.Ledger"/> so the UI can show why.
/// </summary>
public static class EconomySystem
{
    /// <summary>Adds a line to the player's money breakdown. Ignored for everyone else.</summary>
    public static void Record(SimContext ctx, Person p, string label, double nominal)
    {
        if (p.Id != ctx.World.PlayerId || Math.Abs(nominal) < 0.5) return;
        ctx.World.Ledger.Add(new LedgerLine { Year = ctx.Year, Label = label, Amount = nominal });
    }

    /// <summary>Gross yearly income in 2020-kronor, including a part-time job while studying.</summary>
    public static double GrossIncome(SimContext ctx, Person p) =>
        p.Income + (p.Activity == Activity.Studying && p.Flags.Contains(CareerSystem.PartTimeFlag) ? ctx.Country.PartTimeIncome : 0);

    public static string IncomeLabel(Person p) => p.Activity switch
    {
        Activity.Working => "Salary",
        Activity.Studying => "Student aid",
        Activity.Unemployed => "Unemployment benefit",
        Activity.Retired => "Pension",
        _ => "Income"
    };

    public static void Update(SimContext ctx, Person p, double savingsFactor)
    {
        int age = p.Age(ctx.Year);
        var c = ctx.Country;

        // Interest first, so this year's money is not affected by a crash twice.
        double before = p.Money;
        if (p.Money > 0) p.Money *= (1 + c.SavingsReturn) * savingsFactor;
        else p.Money *= 1 + c.DebtInterest;
        if (before > 0) Record(ctx, p, savingsFactor < 1 ? "Savings (market crash)" : "Return on savings", p.Money - before);
        else Record(ctx, p, "Interest on debt", p.Money - before);

        if (p.Activity == Activity.Prison) return; // the state pays for board and lodging
        if (age < 18 && p.Activity != Activity.Working) return;

        double gross = GrossIncome(ctx, p);
        double tax = gross * c.TaxRate;
        double net = gross - tax;
        if (gross > 0)
        {
            Record(ctx, p, IncomeLabel(p), ctx.Nominal(p.Income));
            if (gross > p.Income) Record(ctx, p, "Part-time job", ctx.Nominal(gross - p.Income));
            Record(ctx, p, "Tax", -ctx.Nominal(tax));
        }

        bool livesAtHome = p.LivesWithParents;
        double saved;
        if (livesAtHome)
        {
            saved = net * 0.5;
            Record(ctx, p, "Spending (living at home)", -ctx.Nominal(net - saved));
        }
        else
        {
            // Everyone needs a basic standard of living. Above it, people save part of the surplus
            // (depending on personality); below it, welfare covers half the gap and the rest becomes debt.
            bool sharesHome = p.PartnerId != null && p.PartnerStatus is PartnerStatus.Cohabiting or PartnerStatus.Married;
            double kids = Kinship.Children(ctx.World, p).Count(k => k.IsAlive && k.Age(ctx.Year) < 18);
            // Food and the rest (60 %) cost the same everywhere; housing (40 %) depends on the city and how you live.
            double homeFactor = p.OwnsHome ? 0.7 : p.SharesFlat && !sharesHome ? 0.55 : 1.0;
            double living = c.LivingCostAdult * (0.6 + 0.4 * HousingSystem.City(ctx, p).PriceFactor * homeFactor) * (sharesHome ? 0.8 : 1);
            double children = kids * c.LivingCostChild * (sharesHome ? 0.5 : 1);
            Record(ctx, p, $"Living costs in {HousingSystem.City(ctx, p).Name}" + (sharesHome ? " (your share)" : ""), -ctx.Nominal(living));
            if (children > 0) Record(ctx, p, kids == 1 ? "Your child" : $"Your {kids} children", -ctx.Nominal(children));

            double surplus = net - living - children;
            double saveRate = SaveRate(ctx, p);
            if (surplus > 0)
            {
                saved = surplus * saveRate;
                Record(ctx, p, "Everything else you spent", -ctx.Nominal(surplus - saved));
            }
            else if (p.Money > 0)
            {
                // Savings pay for the gap before anyone else does.
                saved = surplus;
            }
            else
            {
                saved = surplus * 0.5;
                Record(ctx, p, "Covered by welfare", ctx.Nominal(-surplus * 0.5));
            }
        }
        p.Money += ctx.Nominal(saved);

        if (p.Money < -ctx.Nominal(150000)) p.Happiness -= 4;
    }

    /// <summary>Share of what is left after living costs that this person saves.</summary>
    public static double SaveRate(SimContext ctx, Person p) => Math.Clamp(0.3 - ctx.Mod(p, "spending"), 0.05, 0.6);

    /// <summary>Net worth in nominal kronor, including home equity.</summary>
    public static double NetWorth(SimContext ctx, Person p) =>
        p.Money + (p.OwnsHome ? HomeEquity(ctx, p) : 0);

    public static double HomeEquity(SimContext ctx, Person p) => HousingSystem.HomePrice(ctx, p) * 0.5;

    public static bool CanBuyHome(SimContext ctx, Person p) =>
        !p.OwnsHome && !p.LivesWithParents && p.Money >= HousingSystem.HomePrice(ctx, p) * 0.15;

    public static void BuyHome(SimContext ctx, Person p)
    {
        double deposit = HousingSystem.HomePrice(ctx, p) * 0.15;
        p.Money -= deposit;
        Record(ctx, p, "Deposit on your home", -deposit);
        p.OwnsHome = true;
        p.SharesFlat = false;
        if (p.PartnerId is { } pid && p.PartnerStatus is PartnerStatus.Cohabiting or PartnerStatus.Married)
            ctx.World.Get(pid).OwnsHome = true;
        if (p.InFamily)
            ctx.World.Log($"{p.FirstName} bought a home.", ctx.Importance(false, p), "economy", p.Id);
    }

    /// <summary>Pay (2020-kronor per year) the way people talk about it in the country: "32,000 kr / month".</summary>
    public static string FormatPay(SimContext ctx, double yearly2020) =>
        ctx.Country.MonthlyPay
            ? Format(ctx, ctx.Nominal(yearly2020) / 12) + " / month"
            : Format(ctx, ctx.Nominal(yearly2020)) + " / year";

    /// <summary>Formats a nominal amount the way the UI shows money: "12,500 kr".</summary>
    public static string Format(SimContext ctx, double nominal)
    {
        double rounded = Math.Abs(nominal) >= 10000 ? Math.Round(nominal / 1000) * 1000 : Math.Round(nominal / 100) * 100;
        var s = rounded.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);
        return $"{s} {ctx.Country.CurrencySymbol}";
    }
}
