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
    /// <summary>Set on a parent when a child arrives where leave is unpaid; the next pay is smaller.</summary>
    public const string UnpaidLeaveFlag = "unpaid_leave";
    /// <summary>The player went back to work early: only half the unpaid weeks.</summary>
    public const string ShortLeaveFlag = "short_leave";

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

    public static void Update(SimContext ctx, Person p, MarketYear market)
    {
        int age = p.Age(ctx.Year);
        var c = ctx.Country;

        // Returns first: the bank, debt interest, funds, shares and the home's value.
        double before = p.Money;
        p.Money *= 1 + (p.Money > 0 ? market.Bank : market.Debt);
        Record(ctx, p, before > 0 ? "Interest on your savings" : "Interest on debt", p.Money - before);
        // The player has real holdings; an heir or an old save may still have the simple kind.
        if (p.Id == ctx.World.PlayerId) InvestmentSystem.ConvertSimple(ctx, p);
        if (p.Holdings.Count > 0) InvestmentSystem.Update(ctx, p);
        if (p.CottageValue > 0) p.CottageValue *= 1 + market.Housing;
        if (p.Funds > 0)
        {
            double change = p.Funds * market.Stocks;
            p.Funds += change;
        }
        if (p.Stocks > 0)
        {
            double r = Market.StockReturn(ctx, market, p.Id);
            double change = p.Stocks * r;
            p.Stocks += change;
            if (r <= -1 && p.InFamily) ctx.World.Log($"The company {p.FirstName} had shares in went bankrupt.", ctx.Importance(false, p), "economy", p.Id);
        }
        UpdateHome(ctx, p, market);

        if (p.Activity == Activity.Prison) return; // the state pays for board and lodging
        if (age < 18 && p.Activity != Activity.Working) return;

        double gross = GrossIncome(ctx, p);
        if (p.Flags.Remove(UnpaidLeaveFlag))
        {
            double lost = p.Activity == Activity.Working ? p.Income * c.UnpaidLeave * (p.Flags.Remove(ShortLeaveFlag) ? 0.5 : 1) : 0;
            gross -= lost;
            if (lost > 0) Record(ctx, p, "Unpaid parental leave", -ctx.Nominal(lost));
        }
        // Tuition, where university is not free: it becomes student debt.
        if (ctx.Country.UniversityFee > 0 && p.Activity == Activity.Studying && p.StudyingFor == EducationLevel.University)
        {
            p.Money -= ctx.Nominal(ctx.Country.UniversityFee);
            Record(ctx, p, "Tuition", -ctx.Nominal(ctx.Country.UniversityFee));
        }
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
            // Owners pay running costs here and the mortgage separately.
            var homeType = HousingSystem.HomeTypeOf(ctx, p);
            double homeFactor = p.OwnsHome ? 0.45 * (homeType?.PriceFactor > 0 ? homeType.PriceFactor : 1)
                : p.SharesFlat && !sharesHome ? 0.55
                : homeType?.RentFactor > 0 ? homeType.RentFactor : 1.0;
            double living = c.LivingCostAdult * (0.6 + 0.4 * HousingSystem.City(ctx, p).PriceFactor * homeFactor) * (sharesHome ? 0.8 : 1);
            double children = kids * c.LivingCostChild * (sharesHome ? 0.5 : 1);
            // Daycare, where it is not part of what a child costs anyway: only when nobody is home.
            var partnerAtHome = sharesHome ? ctx.World.TryGet(p.PartnerId) : null;
            int little = Kinship.Children(ctx.World, p).Count(k => k.IsAlive && k.Age(ctx.Year) is >= 1 and < 6);
            double daycare = c.ChildcareCost > 0 && little > 0 && p.Activity == Activity.Working && (partnerAtHome == null || partnerAtHome.Activity == Activity.Working)
                ? little * c.ChildcareCost * (sharesHome ? 0.5 : 1) : 0;
            if (daycare > 0) Record(ctx, p, "Daycare", -ctx.Nominal(daycare));
            children += daycare;
            Record(ctx, p, $"Living costs in {HousingSystem.City(ctx, p).Name}" + (sharesHome ? " (your share)" : ""), -ctx.Nominal(living));
            if (children > 0) Record(ctx, p, kids == 1 ? "Your child" : $"Your {kids} children", -ctx.Nominal(children));
            double mortgage = ctx.Real(MortgageCost(ctx, p, market));

            double surplus = net - living - children - mortgage;
            double saveRate = SaveRate(ctx, p);
            if (surplus > 0)
            {
                saved = surplus * saveRate;
                Record(ctx, p, "Everything else you spent", -ctx.Nominal(surplus - saved));
            }
            else if (p.Money + Investments(p) > 0)
            {
                // Savings pay for the gap before anyone else does.
                saved = surplus;
            }
            else
            {
                saved = surplus * (1 - c.WelfareShare);
                Record(ctx, p, "Covered by welfare", ctx.Nominal(-surplus * c.WelfareShare));
            }
        }
        p.Money += ctx.Nominal(saved);

        if (!livesAtHome) SpendFromWealth(ctx, p);
        CoverDebtWithInvestments(ctx, p);
        if (p.Id != ctx.World.PlayerId)
        {
            NpcInvest(ctx, p);
            // Others buy a home when they can afford it (the player decides for themselves).
            if (age >= 26 && CanBuyHome(ctx, p) && ctx.Rng.Chance(0.2)) BuyHome(ctx, p);
        }
        if (p.Money < -ctx.NominalRef(150000)) p.Happiness -= 4;
    }

    // --- Homes and mortgages --------------------------------------------------------------------

    /// <summary>The home follows the housing market; the mortgage is paid down a little every year.</summary>
    private static void UpdateHome(SimContext ctx, Person p, MarketYear market)
    {
        if (!p.OwnsHome) return;
        if (p.HomeValue <= 0)
        {
            // Living in a partner's home is fine; otherwise the home went with the partner.
            bool partnersHome = ctx.World.TryGet(p.PartnerId) is { HomeValue: > 0 }
                                && p.PartnerStatus is PartnerStatus.Cohabiting or PartnerStatus.Married;
            if (!partnersHome) p.OwnsHome = false;
            return;
        }
        // A couple that ended up with two homes (from before they met) keeps one.
        if (ctx.World.TryGet(p.PartnerId) is { HomeValue: > 0 } partner && p.PartnerStatus is PartnerStatus.Cohabiting or PartnerStatus.Married)
        {
            MergeHomes(ctx, p, partner);
            if (p.HomeValue <= 0) return;
        }
        double change = p.HomeValue * market.Housing;
        p.HomeValue += change;
    }

    /// <summary>Interest and amortization on the mortgage this year (nominal). Also pays the loan down.</summary>
    private static double MortgageCost(SimContext ctx, Person p, MarketYear market)
    {
        if (p.Mortgage <= 0) return 0;
        double interest = p.Mortgage * market.MortgageRate;
        double amortize = Math.Min(p.Mortgage, Math.Max(p.MortgageStart, p.Mortgage) * ctx.Country.Amortization);
        p.Mortgage -= amortize;
        Record(ctx, p, "Mortgage interest", -interest);
        Record(ctx, p, "Paying off the mortgage", -amortize);
        return interest + amortize;
    }

    /// <summary>
    /// People with money live on it: a few percent of their wealth above a buffer goes to a better
    /// life every year (more for spenders). Keeps fortunes from growing without end over generations.
    /// </summary>
    private static void SpendFromWealth(SimContext ctx, Person p)
    {
        double buffer = ctx.Nominal(ctx.Country.LivingCostAdult * 2);
        double wealth = p.Money + Investments(p);
        if (wealth <= buffer) return;
        double spend = (wealth - buffer) * Math.Clamp(0.04 + ctx.Mod(p, "spending") * 0.03, 0.02, 0.1);
        if (p.Money < spend)
        {
            double sell = Math.Min(spend - Math.Max(0, p.Money), p.Funds + p.Stocks);
            double fromFunds = Math.Min(sell, p.Funds);
            p.Funds -= fromFunds;
            p.Stocks -= sell - fromFunds;
            p.Money += sell;
            if (p.Money < spend) sell += InvestmentSystem.Raise(ctx, p, spend - Math.Max(0, p.Money));
            Record(ctx, p, "Sold investments to pay for your lifestyle", sell);
        }
        p.Money -= spend;
        Record(ctx, p, "Living well on your wealth", -spend);
    }

    // --- Investments --------------------------------------------------------------------------

    /// <summary>Debt is paid with funds and shares before it grows.</summary>
    private static void CoverDebtWithInvestments(SimContext ctx, Person p)
    {
        if (p.Money >= 0 || Investments(p) <= 0) return;
        double need = -p.Money;
        double fromFunds = Math.Min(need, p.Funds);
        double fromStocks = Math.Min(need - fromFunds, p.Stocks);
        p.Funds -= fromFunds;
        p.Stocks -= fromStocks;
        p.Money += fromFunds + fromStocks;
        double fromHoldings = p.Money < 0 ? InvestmentSystem.Raise(ctx, p, -p.Money) : 0;
        Record(ctx, p, "Sold investments to cover debt", fromFunds + fromStocks + fromHoldings);
    }

    /// <summary>How much of their spare money a person likes to invest – careful people less, risk-takers more.</summary>
    public static double InvestShare(SimContext ctx, Person p) =>
        Math.Clamp(0.3 + ctx.Mod(p, "risk") * 0.12 + ctx.Mod(p, "career") * 0.08 - ctx.Mod(p, "spending") * 0.2, 0, 0.7);

    /// <summary>Other people move money above a buffer into funds (and risk-takers into shares).</summary>
    private static void NpcInvest(SimContext ctx, Person p)
    {
        double buffer = ctx.Nominal(ctx.Country.LivingCostAdult);
        if (p.Money <= buffer || p.Age(ctx.Year) < 20) return;
        double amount = (p.Money - buffer) * InvestShare(ctx, p) * 0.5;
        if (amount <= 0) return;
        p.Money -= amount;
        if (ctx.Mod(p, "risk") > 0.5) { p.Stocks += amount * 0.4; p.Funds += amount * 0.6; }
        else p.Funds += amount;
    }

    /// <summary>The player moves a share of their savings into funds or shares.</summary>
    public static double Invest(SimContext ctx, Person p, string kind, double share)
    {
        double amount = Math.Max(0, p.Money) * Math.Clamp(share, 0, 1);
        if (amount < 1) return 0;
        // The player buys a real fund or company.
        if (p.Id == ctx.World.PlayerId && p.Age(ctx.Year) < InvestmentSystem.MinAge) return 0;
        if (p.Id == ctx.World.PlayerId && InvestmentSystem.DefaultFor(ctx, kind) is { } asset)
        {
            InvestmentSystem.Buy(ctx, p, asset.Id, amount);
            return amount;
        }
        p.Money -= amount;
        if (kind == "stocks") p.Stocks += amount; else p.Funds += amount;
        Record(ctx, p, kind == "stocks" ? "Bought shares" : "Put money in funds", -amount);
        return amount;
    }

    /// <summary>Sells all funds and shares. Returns the amount.</summary>
    public static double SellInvestments(SimContext ctx, Person p)
    {
        double amount = p.Funds + p.Stocks + InvestmentSystem.Value(p);
        p.Money += amount;
        p.Funds = p.Stocks = 0;
        p.Holdings.Clear();
        Record(ctx, p, "Sold your investments", amount);
        return amount;
    }

    /// <summary>Share of what is left after living costs that this person saves.</summary>
    public static double SaveRate(SimContext ctx, Person p) => Math.Clamp(0.3 - ctx.Mod(p, "spending"), 0.05, 0.6);

    /// <summary>Net worth in nominal kronor, including home equity.</summary>
    public static double NetWorth(SimContext ctx, Person p) => p.Money + Investments(p) + HomeEquity(p) + p.CottageValue + RentalSystem.Equity(ctx, p);

    /// <summary>Everything invested: the simple funds and shares, and the player'"'"'s own holdings.</summary>
    public static double Investments(Person p) => p.Funds + p.Stocks + InvestmentSystem.Value(p);

    /// <summary>What the home is worth minus what is left of the loan (0 for someone living in a partner's home).</summary>
    public static double HomeEquity(Person p) => p.OwnsHome ? p.HomeValue - p.Mortgage : 0;

    /// <summary>You can buy when you have the down payment and the bank believes you can carry the loan.</summary>
    public static bool CanBuyHome(SimContext ctx, Person p)
    {
        if (p.OwnsHome || p.LivesWithParents) return false;
        double price = HousingSystem.HomePrice(ctx, p);
        double income = ctx.Nominal(GrossIncome(ctx, p));
        return p.Money >= price * ctx.Country.DownPayment && price * (1 - ctx.Country.DownPayment) <= Math.Max(income, 1) * 5.5;
    }

    public static void BuyHome(SimContext ctx, Person p)
    {
        double price = HousingSystem.HomePrice(ctx, p);
        double deposit = price * ctx.Country.DownPayment;
        p.Money -= deposit;
        Record(ctx, p, "Down payment on your home", -deposit);
        GiveHome(p, price, price - deposit);
        p.SharesFlat = false;
        if (p.PartnerId is { } pid && p.PartnerStatus is PartnerStatus.Cohabiting or PartnerStatus.Married)
            ctx.World.Get(pid).OwnsHome = true;
        if (p.InFamily)
            ctx.World.Log($"{p.FirstName} bought a home for {Format(ctx, price)}.", ctx.Importance(false, p), "economy", p.Id);
    }

    public static void GiveHome(Person p, double value, double mortgage)
    {
        p.OwnsHome = true;
        p.HomeValue = value;
        p.Mortgage = p.MortgageStart = Math.Max(0, mortgage);
    }

    /// <summary>Sells the home this person holds; the partner living there moves out with them.</summary>
    public static double SellHome(SimContext ctx, Person p, bool log = true)
    {
        if (p.HomeValue <= 0) return 0;
        double equity = p.HomeValue - p.Mortgage;
        p.Money += equity;
        Record(ctx, p, "Sold your home (after paying off the loan)", equity);
        if (log && p.InFamily)
            ctx.World.Log($"{p.FirstName} sold {(p.Sex == Sex.Male ? "his" : "her")} home for {Format(ctx, p.HomeValue)}.", ctx.Importance(false, p), "economy", p.Id);
        p.HomeValue = p.Mortgage = p.MortgageStart = 0;
        p.OwnsHome = false;
        if (ctx.World.TryGet(p.PartnerId) is { HomeValue: <= 0 } partner) partner.OwnsHome = false;
        return equity;
    }

    /// <summary>Sells the summer cottage. Returns the amount.</summary>
    public static double SellCottage(SimContext ctx, Person p)
    {
        double amount = p.CottageValue;
        if (amount <= 0) return 0;
        p.Money += amount;
        p.CottageValue = 0;
        p.Flags.Remove("summer_cottage");
        Record(ctx, p, "Sold the summer cottage", amount);
        if (p.InFamily) ctx.World.Log($"{p.FirstName} sold the summer cottage.", 1, "economy", p.Id);
        return amount;
    }

    /// <summary>Pays off part of the mortgage with savings. Returns the amount.</summary>
    public static double RepayMortgage(SimContext ctx, Person p, double share)
    {
        double amount = Math.Min(p.Mortgage, Math.Max(0, p.Money) * Math.Clamp(share, 0, 1));
        if (amount < 1) return 0;
        p.Money -= amount;
        p.Mortgage -= amount;
        Record(ctx, p, "Extra payment on the mortgage", -amount);
        return amount;
    }

    /// <summary>
    /// A couple living together keeps one home – the more valuable one – and sells the other.
    /// </summary>
    public static void MergeHomes(SimContext ctx, Person a, Person b)
    {
        if (a.HomeValue > 0 && b.HomeValue > 0) SellHome(ctx, a.HomeValue < b.HomeValue ? a : b);
        bool owned = a.HomeValue > 0 || b.HomeValue > 0;
        a.OwnsHome = b.OwnsHome = owned;
    }

    /// <summary>Pay (2020-kronor per year) the way people talk about it in the country: "32,000 kr / month".</summary>
    public static string FormatPay(SimContext ctx, double yearly2020) =>
        ctx.Country.MonthlyPay
            ? Format(ctx, ctx.Nominal(yearly2020) / 12) + " / month"
            : Format(ctx, ctx.Nominal(yearly2020)) + " / year";

    /// <summary>Formats a nominal amount the way the UI shows money: "12,500 kr".</summary>
    public static string Format(SimContext ctx, double nominal)
    {
        // Round like people talk about money: 7 kr, 340 kr, 4,500 kr, 120,000 kr.
        double abs = Math.Abs(nominal);
        double step = abs >= 10000 ? 1000 : abs >= 1000 ? 100 : abs >= 100 ? 10 : 1;
        double rounded = Math.Round(nominal / step) * step;
        var s = Math.Abs(rounded).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);
        string sign = rounded < 0 ? "-" : "";
        return ctx.Country.CurrencyBefore ? $"{sign}{ctx.Country.CurrencySymbol}{s}" : $"{sign}{s} {ctx.Country.CurrencySymbol}";
    }
}
