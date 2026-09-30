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

    /// <summary>Price of a home in the person's city, nominal kronor.</summary>
    public static double HomePrice(SimContext ctx, Person p) => ctx.Nominal(ctx.Country.HomePrice) * City(ctx, p).PriceFactor;

    /// <summary>"Rents a flat in Malmö", "Lives with parents in Umeå" ...</summary>
    public static string Describe(SimContext ctx, Person p)
    {
        string city = City(ctx, p).Name;
        if (p.Flags.Contains(CareHomeFlag)) return $"Lives in a care home in {city}";
        if (p.LivesWithParents) return $"Lives with parents in {city}";
        if (p.OwnsHome) return $"Owns a home in {city}";
        if (p.PartnerId != null && p.PartnerStatus is PartnerStatus.Cohabiting or PartnerStatus.Married) return $"Rents a home with their partner in {city}";
        return p.SharesFlat ? $"Shares a flat in {city}" : $"Rents a flat in {city}";
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
