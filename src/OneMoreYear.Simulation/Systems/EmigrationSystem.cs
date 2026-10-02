using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Moving to another country. The story follows the player: the world's country becomes the new one,
/// the player's household comes along, and relatives who stay are marked <see cref="Person.Abroad"/>.
/// They live on as before and show up as living in the old country. All money is changed into the new
/// currency, so every amount on screen stays in one currency.
/// </summary>
public static class EmigrationSystem
{
    public const string EmigrantFlag = "emigrant";

    /// <summary>The player and the household they take along: a partner they live with and children under 18 at home.</summary>
    public static List<Person> Travellers(SimContext ctx, Person p)
    {
        var w = ctx.World;
        var list = new List<Person> { p };
        if (w.TryGet(p.PartnerId) is { IsAlive: true } partner && p.PartnerStatus is PartnerStatus.Cohabiting or PartnerStatus.Married)
            list.Add(partner);
        foreach (var parent in list.ToList())
            foreach (var kid in Kinship.Children(w, parent).Where(k => k.IsAlive && k.Age(ctx.Year) < ctx.Country.AdultAge && k.Abroad == parent.Abroad))
                if (!list.Contains(kid)) list.Add(kid);
        return list;
    }

    /// <summary>The player's household moves to another country: homes and investments are sold, jobs left behind.</summary>
    public static string Emigrate(SimContext ctx, Person player, string countryId)
    {
        var w = ctx.World;
        if (countryId == w.CountryId || !ctx.Content.Countries.TryGetValue(countryId, out var to)) return "";
        var from = ctx.Country;
        var travellers = Travellers(ctx, player);

        // What cannot come along is sold first, in the old currency.
        foreach (var x in travellers)
        {
            foreach (var h in x.Holdings.ToList()) InvestmentSystem.Sell(ctx, x, h.AssetId, 1);
            if (x.HomeValue > 0) EconomySystem.SellHome(ctx, x, log: false);
            if (x.CottageValue > 0) EconomySystem.SellCottage(ctx, x);
            x.OwnsHome = false;
            x.SharesFlat = false;
            x.HomeType = null;
        }

        // Going to the country an ancestor once left: the family has come full circle.
        var ancestors = new List<Person>();
        for (var gen = player.ParentIds.Select(w.Get).ToList(); gen.Count > 0; gen = gen.SelectMany(x => x.ParentIds).Select(w.Get).ToList())
            ancestors.AddRange(gen);
        if (ancestors.Any(a => a.Homeland == countryId)) w.Feats.Add("full_circle");

        Reframe(ctx, countryId, travellers);

        // A new start: a city, a job to find, and the tickets.
        string city = HousingSystem.RandomCityId(ctx);
        foreach (var x in travellers)
        {
            x.CityId = city;
            // Going back home ends the emigrant years; anywhere else, the first country stays home.
            if (x.Homeland == countryId) { x.Homeland = null; x.Flags.Remove(EmigrantFlag); x.Flags.Add("came_home"); }
            else { x.Homeland ??= from.Id; x.Flags.Add(EmigrantFlag); }
            if (x.Activity == Activity.Working) CareerSystem.BecomeJobSeeker(x, ctx);
            if (x.Activity == Activity.Studying && ctx.Content.Programme(x.ProgrammeId) is { } prog && prog.Countries.Count > 0 && !prog.Countries.Contains(countryId))
                CareerSystem.BecomeJobSeeker(x, ctx);
            if (x.Age(ctx.Year) >= 8)
                RelationshipSystem.AddMemory(ctx, x, "emigrated", $"Leaving {from.Name} for {to.Name}", 0);
        }
        double tickets = ctx.NominalRef(15000) * travellers.Count;
        player.Money -= tickets;
        EconomySystem.Record(ctx, player, "The journey", -tickets);

        string who = travellers.Count == 1 ? player.FirstName : $"{player.FirstName} and the family";
        w.Log($"{who} emigrated from {from.Name} to {to.Name}, to {HousingSystem.City(ctx, player).Name}.", 3, "home", travellers.Select(x => x.Id).ToArray());
        return $"You land in {HousingSystem.City(ctx, player).Name} with what fits in {(travellers.Count == 1 ? "two suitcases" : "a few suitcases each")}. Everything else is behind you.";
    }

    /// <summary>
    /// Moves the story to another country without anyone travelling (also used when the next player
    /// lives abroad): money changes currency, and who counts as abroad flips.
    /// </summary>
    public static void Reframe(SimContext ctx, string countryId, IReadOnlyCollection<Person> comingAlong)
    {
        var w = ctx.World;
        var from = ctx.Country;
        var to = ctx.Content.Countries[countryId];
        if (from.Id == to.Id) return;
        // The same real value, in the other currency at this year's prices.
        double real = to.ContentMoneyScale / from.ContentMoneyScale;
        double fromIndex = ctx.MoneyIndex(ctx.Year);
        w.CountryId = to.Id;
        double nominal = real * ctx.MoneyIndex(ctx.Year) / fromIndex;

        foreach (var p in w.People)
        {
            p.Money *= nominal;
            p.PeakNetWorth *= nominal;
            p.HomeValue *= nominal;
            p.Mortgage *= nominal;
            p.MortgageStart *= nominal;
            p.Funds *= nominal;
            p.Stocks *= nominal;
            p.CottageValue *= nominal;
            p.Income *= real;
            foreach (var h in p.Holdings)
            {
                h.Invested *= nominal;
                h.Value *= nominal;
                h.LastDividend *= nominal;
            }

            if (comingAlong.Contains(p) || p.Abroad == to.Id) p.Abroad = null;
            else if (p.Abroad == null) p.Abroad = from.Id;
        }
        foreach (var line in w.Ledger) line.Amount *= nominal;
    }

    /// <summary>Partners and newborns follow the country of the person they live with.</summary>
    public static void Update(SimContext ctx)
    {
        var w = ctx.World;
        foreach (var p in w.People.Where(p => p.IsAlive && p.Abroad != null && p.Id != w.PlayerId))
            if (w.TryGet(p.PartnerId) is { } partner && partner.Id != w.PlayerId && partner.Abroad == null
                && p.PartnerStatus is PartnerStatus.Cohabiting or PartnerStatus.Married)
                partner.Abroad = p.Abroad;
    }

    /// <summary>"in Sweden" for someone abroad, "" otherwise.</summary>
    public static string Where(SimContext ctx, Person p) =>
        p.Abroad is { } c && ctx.Content.Countries.TryGetValue(c, out var def) ? $"in {def.Name}" : "";
}
