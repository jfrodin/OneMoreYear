using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// The family seat (docs/endgame.md): a big house in the country, bought once, that is meant to stay
/// in the family. It costs a lot to keep, falls apart without upkeep, draws the family together in
/// the summers, and passes to one heir, who has to want it. Selling it is a decision the whole family
/// will have opinions about. Money in reference kronor of 2020.
/// </summary>
public static class EstateSystem
{
    public const string InheritedEvent = "estate_inherited";
    private const double Upkeep = 0.012;

    public static FamilyEstate? Held(World w) => w.Estate is { IsHeld: true } e ? e : null;
    public static bool Owns(World w, Person p) => Held(w)?.OwnerId == p.Id;

    /// <summary>What a seat costs in the person's country, reference money.</summary>
    public static double Price(SimContext ctx) => ctx.Country.HomePrice * 4 / ctx.Country.ContentMoneyScale;

    public static string Name(SimContext ctx, Person p) => ctx.Country.Id == "sweden" ? $"{p.LastName}gården" : $"The {p.LastName} Place";

    public static string? CannotBuy(SimContext ctx, Person p)
    {
        if (Held(ctx.World) != null) return "The family already has a seat.";
        double cost = ctx.NominalRef(Price(ctx) * 1.03);
        return p.Money < cost ? $"It would take about {EconomySystem.Format(ctx, cost)} in the bank, all of it at once." : null;
    }

    public static string Buy(SimContext ctx, Person p)
    {
        if (CannotBuy(ctx, p) is { } reason) return reason;
        var w = ctx.World;
        double price = Price(ctx);
        p.Money -= ctx.NominalRef(price * 1.03);
        EconomySystem.Record(ctx, p, "A house in the country", -ctx.NominalRef(price * 1.03));
        w.Estate = new FamilyEstate
        {
            Name = Name(ctx, p), City = HousingSystem.City(ctx, p).Name, BoughtYear = ctx.Year, OwnerId = p.Id, Value = price, Owners = { p.Id },
        };
        w.Log($"{p.FirstName} bought an old house in the country and called it {w.Estate.Name}.", 3, "family", p.Id);
        RelationshipSystem.AddMemory(ctx, p, "estate", $"The first summer at {w.Estate.Name}", 25);
        return $"An old house with a long drive, too many rooms and a roof that will need doing. {w.Estate.Name} is yours, and the family's.";
    }

    /// <summary>The year: upkeep or decay, the value, and summers that bring the family together.</summary>
    public static void Update(SimContext ctx)
    {
        var w = ctx.World;
        if (Held(w) is not { } e)
        {
            // A rich relative may buy one for the family.
            if (w.Estate == null && ctx.Rng.Chance(0.02)
                && w.People.Where(x => x.IsAlive && x.InFamily && x.Id != w.PlayerId && x.Abroad == null && x.Age(ctx.Year) >= 40)
                    .FirstOrDefault(x => x.Money >= ctx.NominalRef(Price(ctx) * 3)) is { } buyer)
                Buy(ctx, buyer);
            return;
        }
        var owner = w.Get(e.OwnerId);
        if (!owner.IsAlive) return; // handled at death
        var market = Market.For(ctx, ctx.Year);
        double upkeep = ctx.NominalRef(e.Value * Upkeep);
        if (owner.Money >= upkeep)
        {
            owner.Money -= upkeep;
            EconomySystem.Record(ctx, owner, $"Keeping up {e.Name}", -upkeep);
            e.Condition = Math.Min(100, e.Condition + 2);
        }
        else e.Condition = Math.Max(0, e.Condition - 7);
        e.Value *= (1 + market.Housing) / (1 + market.Inflation) * (e.Condition < 40 ? 0.97 : 1);
        // Summers there: the grown children and grandchildren come, and come closer.
        if (e.Condition >= 40)
            foreach (var kin in Kinship.Children(w, owner).Concat(Kinship.Children(w, owner).SelectMany(c => Kinship.Children(w, c)))
                         .Where(k => k.IsAlive && k.Abroad == owner.Abroad).ToList())
                RelationshipSystem.Change(ctx, kin.Id, owner.Id, RelDim.Closeness, 1.2, mutual: true);
    }

    public static Person? Heir(SimContext ctx, Person owner)
    {
        var w = ctx.World;
        bool Fits(Person? x) => x is { IsAlive: true } && x.Age(ctx.Year) >= 18 && !owner.Disinherited.Contains(x.Id);
        if (w.TryGet(owner.WillFavoriteId) is { } fav && Fits(fav)) return fav;
        var child = Kinship.Children(w, owner).Where(c => Fits(c)).OrderBy(c => c.BirthYear).FirstOrDefault();
        if (child != null) return child;
        return w.TryGet(owner.PartnerId) is { } partner && Fits(partner) ? partner : null;
    }

    /// <summary>At death, before the estate is shared: the seat goes whole to one heir, or is sold into the estate.</summary>
    public static void OnDeath(SimContext ctx, Person dead)
    {
        var w = ctx.World;
        if (Held(w) is not { } e || e.OwnerId != dead.Id) return;
        var heir = Heir(ctx, dead);
        if (heir == null)
        {
            dead.Money += ctx.NominalRef(e.Value);
            Close(ctx, e, $"{e.Name} was sold after {ctx.Year - e.BoughtYear} years. There was nobody left to take it on.");
            return;
        }
        e.OwnerId = heir.Id;
        if (!e.Owners.Contains(heir.Id)) e.Owners.Add(heir.Id);
        if (heir.Id == w.PlayerId)
        {
            if (EventSystem.QueueSituation(ctx, InheritedEvent, new() { ["target"] = dead.Id }) is { } pending) Fill(ctx, pending, e);
        }
        else if (ctx.Rng.Chance(0.25))
            Sell(ctx, heir);
        else
            w.Log($"{heir.FirstName} took over {e.Name} from {dead.FirstName}.", 2, "family", heir.Id, dead.Id);
    }

    public static string Sell(SimContext ctx, Person seller)
    {
        if (Held(ctx.World) is not { } e || e.OwnerId != seller.Id) return "";
        double price = ctx.NominalRef(e.Value * (0.6 + e.Condition / 250.0));
        seller.Money += price;
        EconomySystem.Record(ctx, seller, $"Sold {e.Name}", price);
        Close(ctx, e, $"{seller.FirstName} sold {e.Name} after {ctx.Year - e.BoughtYear} years in the family.");
        foreach (var kin in Kinship.Children(ctx.World, seller).Concat(Kinship.Siblings(ctx.World, seller)).Where(k => k.IsAlive))
            RelationshipSystem.Change(ctx, kin.Id, seller.Id, RelDim.Bitterness, 6);
        return $"It goes for {EconomySystem.Format(ctx, price)}. Strangers will spend the summers there now.";
    }

    private static void Close(SimContext ctx, FamilyEstate e, string log)
    {
        e.SoldYear = ctx.Year;
        ctx.World.Log(log, 3, "family", e.OwnerId);
    }

    /// <summary>{estate}, {estate_years} and {estate_condition} for events about the seat.</summary>
    public static void Fill(SimContext ctx, PendingEvent pending, FamilyEstate e)
    {
        pending.Words["estate"] = e.Name;
        pending.Words["estate_years"] = (ctx.Year - e.BoughtYear).ToString();
    }

    /// <summary>The "estate" effects: repair (a share of the value, condition up), sell, give (to the target).</summary>
    public static string Apply(SimContext ctx, PendingEvent pending, string? kind, double amount)
    {
        var w = ctx.World;
        if (Held(w) is not { } e || e.OwnerId != w.PlayerId) return "";
        switch (kind)
        {
            case "repair":
                e.Condition = Math.Min(100, e.Condition + (int)(amount == 0 ? 30 : amount));
                return "";
            case "sell":
                return Sell(ctx, w.Player);
            case "give" when w.TryGet(pending.Roles.GetValueOrDefault("target")) is { IsAlive: true } heir:
                e.OwnerId = heir.Id;
                if (!e.Owners.Contains(heir.Id)) e.Owners.Add(heir.Id);
                w.Log($"{w.Player.FirstName} handed {e.Name} over to {heir.FirstName}.", 2, "family", w.Player.Id, heir.Id);
                return "";
        }
        return "";
    }

    public static double Equity(SimContext ctx, Person p) => Held(ctx.World) is { } e && e.OwnerId == p.Id ? ctx.NominalRef(e.Value) : 0;
}
