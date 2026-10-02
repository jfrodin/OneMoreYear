using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Homes bought to let (The Sims' For Rent, docs/sims-inspiration.md). A flat or a house, bought with
/// a down payment and a loan, that brings in rent every year and follows the housing market. Tenants
/// bring their own events. It stays in the family: at death it goes to the will's favourite, the
/// eldest grown child or the partner. Money is in reference kronor of 2020, so it survives emigration.
/// </summary>
public static class RentalSystem
{
    public const string InheritedEvent = "rental_inherited";

    /// <summary>Rent before costs, a share of the value; running costs and repairs; a year without a tenant.</summary>
    private const double Yield = 0.052, Costs = 0.014, EmptyChance = 0.08;

    public static IEnumerable<Rental> OwnedBy(World w, Person p) => w.Rentals.Where(r => r.IsHeld && r.OwnerId == p.Id);
    public static bool OwnsRental(World w, Person p) => OwnedBy(w, p).Any();

    /// <summary>What the person's rentals are worth after their loans, this year's local money.</summary>
    public static double Equity(SimContext ctx, Person p) => OwnedBy(ctx.World, p).Sum(r => ctx.NominalRef(r.Value - r.Loan));

    /// <summary>The kinds of home one could buy to let in the person's city, with the price in reference money.</summary>
    public static List<(HomeTypeDef Type, double Price)> Options(SimContext ctx, Person p) =>
        ctx.Country.HomeTypes.Where(t => t.PriceFactor > 0 && t.MinYear <= ctx.Year)
            .Select(t => (t, ctx.Country.HomePrice * HousingSystem.City(ctx, p).PriceFactor * t.PriceFactor / ctx.Country.ContentMoneyScale))
            .ToList();

    /// <summary>The cash needed to buy: the down payment (at least a quarter, the bank is careful with landlords) and fees.</summary>
    public static double CashNeeded(SimContext ctx, double price) => price * (Math.Max(0.25, ctx.Country.DownPayment) + 0.03);

    public static Rental? Buy(SimContext ctx, Person p, string typeId, out string message)
    {
        var w = ctx.World;
        message = "";
        var option = Options(ctx, p).FirstOrDefault(o => o.Type.Id == typeId);
        if (option.Type == null) return null;
        double cash = CashNeeded(ctx, option.Price);
        if (p.Money < ctx.NominalRef(cash))
        {
            message = $"You would need {EconomySystem.Format(ctx, ctx.NominalRef(cash))} in the bank for the down payment. You do not have it yet.";
            return null;
        }
        p.Money -= ctx.NominalRef(cash);
        string kind = KindName(option.Type);
        EconomySystem.Record(ctx, p, $"Bought a {kind} to let", -ctx.NominalRef(cash));
        var r = new Rental
        {
            Id = w.Rentals.Count + 1, OwnerId = p.Id, Kind = kind, City = HousingSystem.City(ctx, p).Name, BoughtYear = ctx.Year,
            Value = option.Price, Loan = option.Price * (1 - Math.Max(0.25, ctx.Country.DownPayment)), Owners = { p.Id },
        };
        w.Rentals.Add(r);
        w.Log($"{p.FirstName} bought {r.Name} to let.", ctx.Importance(true, p), "economy", p.Id);
        message = $"The keys to {r.Name} are yours. Now all it needs is a tenant who pays.";
        return r;
    }

    private static string KindName(HomeTypeDef t)
    {
        string name = t.Name.ToLowerInvariant();
        foreach (var article in new[] { "a ", "an " })
            if (name.StartsWith(article)) return name[article.Length..];
        return name;
    }

    /// <summary>The year's rent, costs and interest; the value follows the housing market. Some in the family buy.</summary>
    public static void Update(SimContext ctx)
    {
        var w = ctx.World;
        var rng = ctx.Rng;
        var market = Market.For(ctx, ctx.Year);
        double realHousing = (1 + market.Housing) / (1 + market.Inflation) - 1;
        double realRate = Math.Max(0.005, (1 + market.MortgageRate) / (1 + market.Inflation) - 1);
        foreach (var r in w.Rentals.Where(r => r.IsHeld).OrderBy(r => r.Id).ToList())
        {
            var owner = w.Get(r.OwnerId);
            if (!owner.IsAlive) continue; // handled at death
            double rent = r.Value * Yield * rng.Gaussian(1, 0.08);
            if (rng.Chance(EmptyChance)) rent *= 0.4;
            double net = rent - r.Value * Costs - r.Loan * realRate;
            r.LastNet = net;
            owner.Money += ctx.NominalRef(net);
            EconomySystem.Record(ctx, owner, $"Rent from {r.Name}, after costs", ctx.NominalRef(net));
            r.Value *= 1 + realHousing;
            // The loan is paid off slowly from the owner's money.
            double payoff = Math.Min(r.Loan, r.Value * 0.015);
            if (payoff > 0 && owner.Money > ctx.NominalRef(payoff))
            {
                r.Loan -= payoff;
                owner.Money -= ctx.NominalRef(payoff);
                EconomySystem.Record(ctx, owner, $"Paying off the loan on {r.Name}", -ctx.NominalRef(payoff));
            }
        }
        // Now and then someone in the family with money to spare buys a flat to let.
        foreach (var p in w.People.Where(p => p.IsAlive && p.InFamily && p.Id != w.PlayerId && p.Abroad == null).OrderBy(p => p.Id).ToList())
        {
            int age = p.Age(ctx.Year);
            if (age < 35 || age > 65 || OwnedBy(w, p).Count() >= 2 || !rng.Chance(0.01)) continue;
            var cheapest = Options(ctx, p).OrderBy(o => o.Price).FirstOrDefault();
            if (cheapest.Type != null && p.Money >= ctx.NominalRef(CashNeeded(ctx, cheapest.Price)) * 2) Buy(ctx, p, cheapest.Type.Id, out _);
        }
    }

    /// <summary>Who inherits a rental: the will's favourite, the eldest grown child, the partner, or nobody.</summary>
    public static Person? Heir(SimContext ctx, Person owner)
    {
        var w = ctx.World;
        bool Fits(Person? x) => x is { IsAlive: true } && x.Age(ctx.Year) >= 18 && !owner.Disinherited.Contains(x.Id);
        if (w.TryGet(owner.WillFavoriteId) is { } fav && Fits(fav)) return fav;
        var child = Kinship.Children(w, owner).Where(c => Fits(c)).OrderBy(c => c.BirthYear).FirstOrDefault();
        if (child != null) return child;
        return w.TryGet(owner.PartnerId) is { } partner && Fits(partner) ? partner : null;
    }

    /// <summary>At death, before the estate is shared: each rental goes to an heir, or is sold into the estate.</summary>
    public static void OnDeath(SimContext ctx, Person dead)
    {
        var w = ctx.World;
        foreach (var r in OwnedBy(w, dead).ToList())
        {
            var heir = Heir(ctx, dead);
            if (heir == null)
            {
                dead.Money += ctx.NominalRef(r.Value - r.Loan);
                r.SoldYear = ctx.Year;
                continue;
            }
            r.OwnerId = heir.Id;
            if (!r.Owners.Contains(heir.Id)) r.Owners.Add(heir.Id);
            if (heir.Id == w.PlayerId)
            {
                if (EventSystem.QueueSituation(ctx, InheritedEvent, new() { ["target"] = dead.Id }) is { } pending) Fill(ctx, pending, r);
            }
            else if (heir.InFamily)
                w.Log($"{heir.FirstName} inherited {r.Name} from {dead.FirstName}.", 1, "economy", heir.Id, dead.Id);
        }
    }

    public static string Sell(SimContext ctx, Rental r, Person seller)
    {
        double price = ctx.NominalRef(Math.Max(0, r.Value - r.Loan));
        seller.Money += price;
        EconomySystem.Record(ctx, seller, $"Sold {r.Name}", price);
        r.SoldYear = ctx.Year;
        ctx.World.Log($"{seller.FirstName} sold {r.Name}.", 1, "economy", seller.Id);
        return $"After the loan is paid off, {EconomySystem.Format(ctx, price)} is left.";
    }

    /// <summary>The words for an event about a rental: {rental}, {rental_value}, {rental_years}.</summary>
    public static void Fill(SimContext ctx, PendingEvent pending, Rental r)
    {
        pending.Words["rental"] = r.Name;
        pending.Words["rental_id"] = r.Id.ToString();
        pending.Words["rental_value"] = EconomySystem.Format(ctx, ctx.NominalRef(r.Value));
        pending.Words["rental_years"] = (ctx.Year - r.BoughtYear).ToString();
    }

    public static Rental? From(World w, PendingEvent pending) =>
        pending.Words.TryGetValue("rental_id", out var raw) && int.TryParse(raw, out var id) ? w.Rentals.FirstOrDefault(r => r.Id == id) : null;

    /// <summary>The "rental" effects: sell, value (a change in reference money), give (to the target).</summary>
    public static string Apply(SimContext ctx, PendingEvent pending, string? kind, double amount)
    {
        var w = ctx.World;
        if (From(w, pending) is not { IsHeld: true } r || r.OwnerId != w.PlayerId) return "";
        switch (kind)
        {
            case "sell":
                return Sell(ctx, r, w.Player);
            case "value":
                r.Value = Math.Max(0, r.Value + amount);
                return "";
            case "give" when w.TryGet(pending.Roles.GetValueOrDefault("target")) is { IsAlive: true } heir:
                r.OwnerId = heir.Id;
                if (!r.Owners.Contains(heir.Id)) r.Owners.Add(heir.Id);
                w.Log($"{w.Player.FirstName} gave {r.Name} to {heir.FirstName}.", 2, "economy", w.Player.Id, heir.Id);
                return "";
        }
        return "";
    }
}
