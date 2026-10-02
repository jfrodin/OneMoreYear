using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Family businesses (docs/sims-inspiration.md): a bakery, a workshop, a software company. The owner
/// draws a living from it; what is left grows its value. A good year depends on the owner's skill, the
/// economy and luck; a run of bad ones can sink it. It passes down the family at death or retirement,
/// which is the point: "Berglund's Bakery, since 1962". Money is in reference kronor of 2020.
/// </summary>
public static class BusinessSystem
{
    public const string InheritedEvent = "business_inherited", FailedEvent = "business_failed", HandoverEvent = "business_handover";

    public static IEnumerable<Business> OwnedBy(World w, Person p) => w.Businesses.Where(b => b.IsOpen && b.OwnerId == p.Id);
    public static bool RunsBusiness(World w, Person p) => OwnedBy(w, p).Any();

    public static Business? Start(SimContext ctx, Person owner, string kind, out string message)
    {
        var w = ctx.World;
        message = "";
        if (!ctx.Content.BusinessKinds.TryGetValue(kind, out var def)) return null;
        double cost = ctx.NominalRef(def.StartCost);
        if (owner.Money < cost)
        {
            message = $"You would need {EconomySystem.Format(ctx, cost)} to start. You do not have it yet.";
            return null;
        }
        owner.Money -= cost;
        EconomySystem.Record(ctx, owner, $"Starting a {def.Name}", -cost);
        string name = ctx.Rng.Pick(def.Names).Replace("{surname}", owner.LastName).Replace("{first}", owner.FirstName)
            .Replace("{city}", HousingSystem.City(ctx, owner).Name);
        var b = new Business
        {
            Id = w.Businesses.Count + 1, Kind = kind, Name = name, OwnerId = owner.Id, FoundedYear = ctx.Year,
            Value = def.StartCost * 0.8, LastProfit = 0, Owners = { owner.Id },
        };
        w.Businesses.Add(b);
        BecomeOwner(ctx, owner, b);
        w.Log($"{owner.FirstName} started {name}.", ctx.Importance(true, owner), "career", owner.Id);
        message = $"{name} opens its doors.";
        return b;
    }

    /// <summary>The owner works in the business: the business track's title, the business as employer.</summary>
    private static void BecomeOwner(SimContext ctx, Person owner, Business b)
    {
        owner.Activity = Activity.Working;
        owner.OccupationId = "business";
        if (!owner.Jobs.Contains("business")) owner.Jobs.Add("business");
        owner.Employer = b.Name;
        owner.ProgrammeId = null;
        owner.StudyingFor = null;
        owner.Flags.Remove(CareerSystem.PartTimeFlag);
        SetPay(ctx, owner, b);
    }

    private static void SetPay(SimContext ctx, Person owner, Business b)
    {
        owner.OccupationLevel = b.Value switch { < 1_000_000 => 0, < 4_000_000 => 1, < 15_000_000 => 2, _ => 3 };
        owner.Income = ctx.Ref(Math.Max(150_000, b.LastProfit * 0.7));
    }

    public static void Update(SimContext ctx)
    {
        var w = ctx.World;
        var rng = ctx.Rng;
        var market = Market.For(ctx, ctx.Year);
        double economy = Math.Clamp(1 + (market.Stocks - market.Inflation) * 0.8, 0.3, 1.8);
        foreach (var b in w.Businesses.Where(b => b.IsOpen).OrderBy(b => b.Id).ToList())
        {
            var owner = w.Get(b.OwnerId);
            if (!owner.IsAlive) { PassOn(ctx, b, owner); continue; }
            if (!ctx.Content.BusinessKinds.TryGetValue(b.Kind, out var def)) continue;
            int skill = def.Skill != null ? SkillSystem.Level(owner, def.Skill) : 3;
            double scale = Math.Clamp(Math.Sqrt(Math.Max(0.1, b.Value / def.StartCost)), 0.5, 4);
            double profit = def.Profit * scale * (0.7 + skill * 0.06) * economy * rng.Gaussian(1, def.Risk)
                            * (1 + ctx.Mod(owner, "career") * 0.15);
            b.LastProfit = profit;
            double drawn = Math.Max(150_000, profit * 0.7);
            b.Value = b.Value * 1.02 + (profit - drawn) * 0.8;
            if (b.Value < 0)
            {
                Fail(ctx, b, owner);
                continue;
            }
            if (owner.OccupationId == "business" && owner.Activity == Activity.Working) SetPay(ctx, owner, b);
            // At seventy the owner hands it on (the player decides for themselves).
            if (owner.Age(ctx.Year) >= 70 && owner.Id != w.PlayerId) PassOn(ctx, b, owner);
            else if (owner.Age(ctx.Year) >= 68 && owner.Id == w.PlayerId && owner.Flags.Add("business_handover_asked"))
                Queue(ctx, HandoverEvent, b, Heir(ctx, owner, b)?.Id);
        }
        // Now and then someone in the family takes the plunge.
        foreach (var p in w.People.Where(p => p.IsAlive && p.InFamily && p.Id != w.PlayerId && p.Abroad == null).OrderBy(p => p.Id).ToList())
        {
            int age = p.Age(ctx.Year);
            if (age < 28 || age > 55 || RunsBusiness(w, p) || p.Activity is not (Activity.Working or Activity.Unemployed)) continue;
            bool drive = p.HasTrait("ambitious") || p.HasTrait("brave") || p.HasTrait("greedy") || p.Skills.Values.Any(v => v >= 6);
            if (!drive || !rng.Chance(0.008)) continue;
            var options = ctx.Content.BusinessKinds.Values.Where(k => k.MinYear <= ctx.Year && p.Money >= ctx.NominalRef(k.StartCost))
                .OrderBy(k => k.Skill != null ? -SkillSystem.Level(p, k.Skill) : 0).ThenBy(k => k.Id, StringComparer.Ordinal).ToList();
            if (options.Count > 0) Start(ctx, p, options[0].Id, out _);
        }
    }

    /// <summary>Who would take the business on: the will's favourite, the eldest grown child, or the partner.</summary>
    public static Person? Heir(SimContext ctx, Person owner, Business b)
    {
        var w = ctx.World;
        bool Fits(Person? x) => x is { IsAlive: true } && x.Age(ctx.Year) >= 20 && x.Age(ctx.Year) < 70 && x.Abroad == owner.Abroad && !owner.Disinherited.Contains(x.Id);
        if (w.TryGet(owner.WillFavoriteId) is { } fav && Fits(fav)) return fav;
        var child = Kinship.Children(w, owner).Where(c => Fits(c)).OrderBy(c => c.BirthYear).FirstOrDefault();
        if (child != null) return child;
        return w.TryGet(owner.PartnerId) is { } partner && Fits(partner) ? partner : null;
    }

    /// <summary>The owner dies or retires: the business goes on in the family, or is sold or closed.</summary>
    public static void PassOn(SimContext ctx, Business b, Person owner)
    {
        var w = ctx.World;
        var heir = Heir(ctx, owner, b);
        if (owner.IsAlive && owner.OccupationId == "business") CareerSystem.Retire(ctx, owner);
        if (heir == null)
        {
            Close(ctx, b, $"{b.Name} closed after {ctx.Year - b.FoundedYear} years. There was nobody to take it on.");
            if (owner.IsAlive) owner.Money += ctx.NominalRef(Math.Max(0, b.Value));
            return;
        }
        b.OwnerId = heir.Id;
        if (!b.Owners.Contains(heir.Id)) b.Owners.Add(heir.Id);
        if (heir.Id == w.PlayerId)
        {
            Queue(ctx, InheritedEvent, b, owner.Id);
            return;
        }
        // Someone else in the family: most take it on, some sell.
        if (heir.Activity is Activity.Unemployed or Activity.Retired || ctx.Rng.Chance(0.65))
        {
            BecomeOwner(ctx, heir, b);
            w.Log($"{heir.FirstName} took over {b.Name} from {owner.FirstName}.", ctx.Importance(true, heir), "career", heir.Id, owner.Id);
        }
        else Sell(ctx, b, heir);
    }

    public static string Sell(SimContext ctx, Business b, Person seller)
    {
        double price = ctx.NominalRef(Math.Max(0, b.Value));
        seller.Money += price;
        EconomySystem.Record(ctx, seller, $"Sold {b.Name}", price);
        Close(ctx, b, $"{seller.FirstName} sold {b.Name} after {ctx.Year - b.FoundedYear} years.");
        if (seller.OccupationId == "business" && seller.Activity == Activity.Working && !RunsBusiness(ctx.World, seller))
            CareerSystem.BecomeJobSeeker(seller, ctx);
        return $"It goes for {EconomySystem.Format(ctx, price)}.";
    }

    private static void Fail(SimContext ctx, Business b, Person owner)
    {
        double debt = ctx.NominalRef(-b.Value);
        owner.Money -= debt;
        EconomySystem.Record(ctx, owner, $"The debts of {b.Name}", -debt);
        Close(ctx, b, $"{b.Name} went bankrupt after {ctx.Year - b.FoundedYear} years.");
        RelationshipSystem.AddMemory(ctx, owner, "bankrupt", $"Losing {b.Name}", -30);
        if (owner.OccupationId == "business") CareerSystem.BecomeJobSeeker(owner, ctx);
        if (owner.Id == ctx.World.PlayerId) Queue(ctx, FailedEvent, b, null);
    }

    private static void Close(SimContext ctx, Business b, string log)
    {
        b.ClosedYear = ctx.Year;
        ctx.World.Log(log, 2, "career", b.OwnerId);
    }

    private static void Queue(SimContext ctx, string eventId, Business b, int? targetId)
    {
        var roles = targetId is { } t ? new Dictionary<string, int> { ["target"] = t } : null;
        if (EventSystem.QueueSituation(ctx, eventId, roles) is { } pending) Fill(ctx, pending, b);
    }

    /// <summary>The words for an event about a business: {business}, {business_value}, {business_profit}, {business_years}.</summary>
    public static void Fill(SimContext ctx, PendingEvent pending, Business b)
    {
        pending.Words["business"] = b.Name;
        pending.Words["business_id"] = b.Id.ToString();
        pending.Words["business_value"] = EconomySystem.Format(ctx, ctx.NominalRef(Math.Max(0, b.Value)));
        pending.Words["business_profit"] = EconomySystem.Format(ctx, ctx.NominalRef(b.LastProfit));
        pending.Words["business_years"] = (ctx.Year - b.FoundedYear).ToString();
        pending.Words["business_kind"] = ctx.Content.BusinessKinds.GetValueOrDefault(b.Kind)?.Name ?? b.Kind;
    }

    public static Business? From(World w, PendingEvent pending) =>
        pending.Words.TryGetValue("business_id", out var raw) && int.TryParse(raw, out var id) ? w.Businesses.FirstOrDefault(b => b.Id == id) : null;

    /// <summary>The "business" effects: run (take it on), sell, value (a change in reference money).</summary>
    public static string Apply(SimContext ctx, PendingEvent pending, string? kind, double amount)
    {
        var w = ctx.World;
        var player = w.Player;
        if (kind == "start") return "";
        if (From(w, pending) is not { IsOpen: true } b) return "";
        switch (kind)
        {
            case "run":
                b.OwnerId = player.Id;
                if (!b.Owners.Contains(player.Id)) b.Owners.Add(player.Id);
                BecomeOwner(ctx, player, b);
                w.Log($"{player.FirstName} took over {b.Name}.", 3, "career", player.Id);
                return "";
            case "sell":
                return Sell(ctx, b, player);
            case "value":
                b.Value = Math.Max(0, b.Value + amount);
                return "";
            case "hand_over" when w.TryGet(pending.Roles.GetValueOrDefault("target")) is { IsAlive: true } heir:
                b.OwnerId = heir.Id;
                if (!b.Owners.Contains(heir.Id)) b.Owners.Add(heir.Id);
                BecomeOwner(ctx, heir, b);
                if (player.OccupationId == "business") CareerSystem.Retire(ctx, player);
                w.Log($"{player.FirstName} handed {b.Name} over to {heir.FirstName}.", 3, "career", player.Id, heir.Id);
                return "";
        }
        return "";
    }
}
