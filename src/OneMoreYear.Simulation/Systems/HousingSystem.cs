using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>Where people live: which city, with their parents, in a shared flat, renting or owning – and moving.</summary>
public static class HousingSystem
{
    public static CityDef City(SimContext ctx, Person p) =>
        ctx.Country.Cities.FirstOrDefault(c => c.Id == p.CityId) ?? ctx.Country.Cities.FirstOrDefault()
        ?? new CityDef { Id = "", Name = ctx.Country.Name };

    public static string RandomCityId(SimContext ctx, string? except = null)
    {
        var options = ctx.Country.Cities.Where(c => c.Id != except).ToList();
        return options.Count == 0 ? "" : ctx.Rng.PickWeighted(options, c => c.Weight)!.Id;
    }

    /// <summary>Price of a home in the person's city, nominal kronor: their kind of home, or the one given.</summary>
    public static double HomePrice(SimContext ctx, Person p, string? typeId = null)
    {
        var type = ctx.Country.HomeTypes.FirstOrDefault(t => t.Id == (typeId ?? p.HomeType));
        double factor = type is { PriceFactor: > 0 } ? type.PriceFactor : 1;
        return ctx.Nominal(ctx.Country.HomePrice) * City(ctx, p).PriceFactor * factor;
    }

    public static HomeTypeDef? HomeTypeOf(SimContext ctx, Person p) =>
        p.HomeType == null ? null : ctx.Country.HomeTypes.FirstOrDefault(t => t.Id == p.HomeType);

    /// <summary>The people a home is for: the person and a partner they live with.</summary>
    private static List<Person> Household(SimContext ctx, Person p)
    {
        var list = new List<Person> { p };
        if (ctx.World.TryGet(p.PartnerId) is { } partner && p.PartnerStatus is PartnerStatus.Cohabiting or PartnerStatus.Married) list.Add(partner);
        return list;
    }

    /// <summary>Rent for a kind of home in the person's city, nominal per month (the housing part of living costs).</summary>
    public static double MonthlyRent(SimContext ctx, Person p, HomeTypeDef type) =>
        ctx.Nominal(ctx.Country.LivingCostAdult * 0.4 * City(ctx, p).PriceFactor * type.RentFactor) / 12;

    /// <summary>Roughly what owning costs per month: interest and paying off the loan, plus running costs.</summary>
    public static double MonthlyOwnerCost(SimContext ctx, Person p, HomeTypeDef type)
    {
        double price = HomePrice(ctx, p, type.Id);
        var m = Market.For(ctx, ctx.Year);
        double loan = price * (1 - ctx.Country.DownPayment);
        double running = ctx.Nominal(ctx.Country.LivingCostAdult * 0.4 * City(ctx, p).PriceFactor * 0.45 * type.PriceFactor);
        return (loan * (m.MortgageRate + ctx.Country.Amortization) + running) / 12;
    }

    /// <summary>Why the person cannot buy this kind of home, or null if they can (their household's savings and incomes count).</summary>
    public static string? CannotBuy(SimContext ctx, Person p, HomeTypeDef type)
    {
        if (type.PriceFactor <= 0) return "Not for sale.";
        if (p.Age(ctx.Year) < ctx.Country.AdultAge) return "You are too young to buy a home.";
        var household = Household(ctx, p);
        double price = HomePrice(ctx, p, type.Id);
        double deposit = price * ctx.Country.DownPayment;
        // What the current home would sell for counts towards the down payment.
        double cash = household.Sum(x => Math.Max(0, x.Money) + (x.HomeValue > 0 ? x.HomeValue - x.Mortgage : 0));
        double income = household.Sum(x => ctx.Nominal(EconomySystem.GrossIncome(ctx, x)));
        var missing = new List<string>();
        if (cash < deposit) missing.Add($"{EconomySystem.Format(ctx, deposit)} for the down payment (you have {EconomySystem.Format(ctx, cash)})");
        if (price - deposit > Math.Max(income, 1) * 5.5)
            missing.Add($"an income of {EconomySystem.Format(ctx, (price - deposit) / 5.5)} a year for the bank to lend you the rest");
        return missing.Count == 0 ? null : "You need " + string.Join(", and ", missing) + ".";
    }

    /// <summary>Leaves the current home: sells it if the household owns it, or moves out of the parents' home.</summary>
    private static void LeaveCurrentHome(SimContext ctx, Person p)
    {
        foreach (var x in Household(ctx, p))
        {
            if (x.HomeValue > 0) EconomySystem.SellHome(ctx, x);
            x.OwnsHome = false;
            x.LivesWithParents = false;
            x.SharesFlat = false;
        }
    }

    /// <summary>The player rents a kind of home in their city.</summary>
    public static string Rent(SimContext ctx, Person p, HomeTypeDef type)
    {
        if (type.RentFactor <= 0) return "That kind of home is not for rent.";
        LeaveCurrentHome(ctx, p);
        foreach (var x in Household(ctx, p))
        {
            x.HomeType = type.Id;
            x.SharesFlat = type.Id == "room";
        }
        ctx.World.Log($"{p.FirstName} moved into {type.Name.ToLowerInvariant()} in {City(ctx, p).Name}.", ctx.Importance(false, p), "home", p.Id);
        return $"You move into {type.Name.ToLowerInvariant()}. The rent is about {EconomySystem.Format(ctx, MonthlyRent(ctx, p, type))} a month.";
    }

    /// <summary>The player buys a kind of home in their city (selling the current one first).</summary>
    public static string Buy(SimContext ctx, Person p, HomeTypeDef type)
    {
        if (CannotBuy(ctx, p, type) is { } reason) return reason;
        // A partner's savings make up what is missing for the down payment.
        LeaveCurrentHome(ctx, p);
        double deposit = HomePrice(ctx, p, type.Id) * ctx.Country.DownPayment;
        foreach (var x in Household(ctx, p).Where(x => x != p && x.Money > 0))
        {
            double take = Math.Min(x.Money, Math.Max(0, deposit - p.Money));
            p.Money += take;
            x.Money -= take;
        }
        foreach (var x in Household(ctx, p)) x.HomeType = type.Id;
        EconomySystem.BuyHome(ctx, p);
        return $"The keys are yours: {type.Name.ToLowerInvariant()} in {City(ctx, p).Name}.";
    }

    /// <summary>"Rents a flat in Malmö", "Lives with parents in Umeå" ...</summary>
    public static string Describe(SimContext ctx, Person p)
    {
        string city = City(ctx, p).Name;
        if (p.Flags.Contains(CareHomeFlag)) return $"Lives in a care home in {city}";
        if (p.Flags.Contains(Hardship.HomelessFlag)) return $"Homeless in {city}";
        if (p.LivesWithParents) return $"Lives with parents in {city}";
        // "Owns a terraced house in Umeå", "Rents a one-room flat in Malmö".
        string? kind = HomeTypeOf(ctx, p) is { } t && t.Id != "room" ? t.Name.ToLowerInvariant() : null;
        if (p.OwnsHome) return $"Owns {kind ?? "a home"} in {city}";
        if (p.SharesFlat) return $"Shares {TextFormatter.A(ctx.Country.Flat)} in {city}";
        if (p.PartnerId != null && p.PartnerStatus is PartnerStatus.Cohabiting or PartnerStatus.Married) return $"Rents {kind ?? "a home"} with their partner in {city}";
        return $"Rents {kind ?? TextFormatter.A(ctx.Country.Flat)} in {city}";
    }

    /// <summary>The same as <see cref="Describe"/>, told to the player: "You rent a three-room flat with Anna in Umeå."</summary>
    public static string DescribeForPlayer(SimContext ctx, Person p)
    {
        string city = City(ctx, p).Name;
        if (p.Flags.Contains(CareHomeFlag)) return $"You live in a care home in {city}";
        if (p.Flags.Contains(Hardship.HomelessFlag)) return $"You have no home, in {city}";
        if (p.LivesWithParents) return $"You live with your parents in {city}";
        string? kind = HomeTypeOf(ctx, p) is { } t && t.Id != "room" ? t.Name.ToLowerInvariant() : null;
        string with = ctx.World.TryGet(p.PartnerId) is { } partner && p.PartnerStatus is PartnerStatus.Cohabiting or PartnerStatus.Married
            ? $" with {partner.FirstName}" : "";
        if (p.OwnsHome) return p.HomeValue > 0 || with == "" ? $"You own {kind ?? "your home"}{with} in {city}" : $"You live in {Kinship.Genitive(ctx.World.Get(p.PartnerId!.Value).FirstName)} home in {city}";
        if (p.SharesFlat) return $"You share {TextFormatter.A(ctx.Country.Flat)} in {city}";
        return $"You rent {kind ?? TextFormatter.A(ctx.Country.Flat)}{with} in {city}";
    }

    /// <summary>Young adults move out of their parents' home – the player decides through an event.</summary>
    public const string CareHomeFlag = "care_home";

    public static void Update(SimContext ctx, Person p)
    {
        // Very old or ill people who live alone move into a care home (the player decides through events).
        if (p.Id != ctx.World.PlayerId && !p.Flags.Contains(CareHomeFlag) && p.Age(ctx.Year) >= 80 && p.PartnerId == null
            && (p.Ailments.ContainsKey("dementia") || p.Health < 35) && ctx.Rng.Chance(0.25))
            MoveToCareHome(ctx, p);
        if (!p.LivesWithParents) return;
        int age = p.Age(ctx.Year);
        if (age < ctx.Country.AdultAge) return;
        if (Kinship.Parents(ctx.World, p).All(x => !x.IsAlive)) { MoveOut(ctx, p, share: false); return; }

        if (p.Id == ctx.World.PlayerId)
        {
            bool busy = p.Activity is Activity.Working or Activity.Studying;
            if (busy && !p.Flags.Contains($"asked_move_out_{ctx.Year - 1}") && !p.Flags.Contains($"asked_move_out_{ctx.Year - 2}"))
            {
                p.Flags.Add($"asked_move_out_{ctx.Year}");
                EventSystem.QueueSituation(ctx, "moving_out");
            }
            return;
        }
        double chance = age switch { < 20 => 0.2, < 25 => 0.4, _ => 0.25 };
        if (p.Activity == Activity.Unemployed) chance *= 0.5;
        if (ctx.Rng.Chance(chance)) MoveOut(ctx, p, share: age < 25 && ctx.Rng.Chance(0.4));
    }

    public static void MoveToCareHome(SimContext ctx, Person p)
    {
        p.Flags.Add(CareHomeFlag);
        if (p.HomeValue > 0) EconomySystem.SellHome(ctx, p, log: false);
        p.OwnsHome = false;
        if (p.InFamily || p.Id == ctx.World.PlayerId)
            ctx.World.Log($"{p.FirstName} moved into a care home.", ctx.Importance(false, p), "home", p.Id);
    }

    public static void MoveOut(SimContext ctx, Person p, bool share)
    {
        p.LivesWithParents = false;
        p.SharesFlat = share;
        if (p.InFamily || p.Id == ctx.World.PlayerId)
            ctx.World.Log(share ? $"{p.FirstName} moved out and into a shared flat." : $"{p.FirstName} moved out and got a place of their own.",
                ctx.Importance(false, p), "home", p.Id);
    }

    public static void MoveBackHome(SimContext ctx, Person p)
    {
        var parent = Kinship.Parents(ctx.World, p).FirstOrDefault(x => x.IsAlive);
        if (parent == null) return;
        p.LivesWithParents = true;
        p.SharesFlat = false;
        p.CityId = parent.CityId;
        ctx.World.Log($"{p.FirstName} moved back in with {parent.FirstName}.", ctx.Importance(false, p), "home", p.Id, parent.Id);
    }

    /// <summary>Moves someone to another city; a partner they live with and their children at home come along.</summary>
    public static void MoveTo(SimContext ctx, Person p, string? cityId = null)
    {
        var w = ctx.World;
        string target = cityId ?? RandomCityId(ctx, p.CityId);
        if (target == p.CityId) return;
        var movers = new List<Person> { p };
        if (w.TryGet(p.PartnerId) is { } partner && p.PartnerStatus is PartnerStatus.Cohabiting or PartnerStatus.Married) movers.Add(partner);
        movers.AddRange(movers.SelectMany(m => Kinship.Children(w, m)).Where(k => k.IsAlive && k.LivesWithParents).Distinct().ToList());
        foreach (var m in movers.Distinct()) m.CityId = target;
        p.LivesWithParents = false;
        var cityName = ctx.Country.Cities.FirstOrDefault(c => c.Id == target)?.Name ?? target;
        w.Log(movers.Count > 1 ? $"{p.FirstName} and the family moved to {cityName}." : $"{p.FirstName} moved to {cityName}.",
            ctx.Importance(false, p), "home", movers.Select(m => m.Id).ToArray());
    }

    /// <summary>When two people move in together they end up in the same city (the one with the job stays).</summary>
    public static void MoveInTogether(SimContext ctx, Person a, Person b)
    {
        a.LivesWithParents = b.LivesWithParents = false;
        a.SharesFlat = b.SharesFlat = false;
        if (a.CityId == b.CityId) return;
        var (stays, moves) = b.Activity == Activity.Working && a.Activity != Activity.Working ? (b, a) : (a, b);
        moves.CityId = stays.CityId;
    }
}
