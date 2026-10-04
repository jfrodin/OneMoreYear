using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// What money is for, once there is enough of it (docs/endgame.md): giving something away for good.
/// A foundation, a scholarship or a summer camp keeps helping people every year, long after the
/// donor is gone; a library, a hospital wing, a park or a concert hall puts the family name on the
/// town. Gifts appear in the chronicle, come back as events generations later, warm the family's
/// reputation, and end up on the last page. Costs are in reference kronor of 2020.
/// </summary>
public static class GiftSystem
{
    public sealed record Kind(string Id, string Name, double Cost, string Title, bool Ongoing, string Given, string[] Years);

    public static readonly Kind[] Kinds =
    {
        new("scholarship", "A scholarship", 2_000_000, "The {surname} Scholarship", true,
            "{first} set up {name}, for young people who cannot pay for their studies.",
            new[] { "{name} went to a girl from {city} who wants to be a doctor.", "{name} went to a boy whose father drives a bus, and who wants to build bridges.",
                    "{name} paid for a year of university for someone who would never otherwise have gone." }),
        new("summer_camp", "A summer camp", 2_500_000, "The {surname} Summer Camp", true,
            "{first} started {name}, where children who never go anywhere get a week by a lake.",
            new[] { "Forty children had a week by the lake at {name}.", "A boy learned to swim at {name}. He was eleven.", "{name} ran out of beds, and put up tents." }),
        new("foundation", "A foundation for research", 3_000_000, "The {surname} Foundation", true,
            "{first} set up {name}, to pay for research into the illnesses that take people too early.",
            new[] { "{name} paid for a year of cancer research in {city}.", "A study paid for by {name} was published, and a treatment changed.", "{name} gave a young researcher her first lab." }),
        new("park", "A park", 6_000_000, "{surname} Park", false,
            "{first} gave {city} a park: {name}, with an oak for every grandchild.", Array.Empty<string>()),
        new("library", "A library", 12_000_000, "The {surname} Library", false,
            "{first} gave {city} a library: {name}, open every day of the year.", Array.Empty<string>()),
        new("hospital_wing", "A hospital wing", 25_000_000, "The {surname} Wing", false,
            "{first} paid for a new children's wing at the hospital in {city}: {name}.", Array.Empty<string>()),
        new("concert_hall", "A concert hall", 40_000_000, "The {surname} Concert Hall", false,
            "{first} gave {city} a concert hall: {name}. The first concert sold out in an hour.", Array.Empty<string>()),
    };

    public static Kind? Find(string? id) => Kinds.FirstOrDefault(k => k.Id == id);

    public static string Give(SimContext ctx, Person donor, string kindId)
    {
        var w = ctx.World;
        if (Find(kindId) is not { } kind) return "";
        double cost = ctx.NominalRef(kind.Cost);
        if (donor.Money < cost) return $"It would take {EconomySystem.Format(ctx, cost)} in the bank. You do not have that much.";
        // One of each kind from each person: a second library is just showing off.
        if (w.Gifts.Any(g => g.DonorId == donor.Id && g.Kind == kindId)) return "";
        donor.Money -= cost;
        EconomySystem.Record(ctx, donor, kind.Name, -cost);
        string city = HousingSystem.City(ctx, donor).Name;
        var gift = new Gift
        {
            Id = w.Gifts.Count + 1, Kind = kindId, Name = kind.Title.Replace("{surname}", donor.LastName), City = city,
            Year = ctx.Year, DonorId = donor.Id, Amount = kind.Cost,
        };
        w.Gifts.Add(gift);
        w.Log(Fill(kind.Given, gift, donor), 3, "family", donor.Id);
        RelationshipSystem.AddMemory(ctx, donor, "gift", $"Giving {gift.Name}", 25);
        donor.Happiness = Math.Min(100, donor.Happiness + 8);
        return $"{gift.Name}. It will be there long after you.";
    }

    /// <summary>"the Berglund Library" in the middle of a sentence; the sentence start is capitalised anyway.</summary>
    public static string Mid(Gift g) => g.Name.StartsWith("The ") ? "the " + g.Name[4..] : g.Name;

    private static string Fill(string text, Gift g, Person donor) => TextFormatter.Capitalize(
        text.Replace("{name}", Mid(g)).Replace("{city}", g.City).Replace("{first}", donor.FirstName));

    /// <summary>Every year the ongoing gifts help someone; now and then the player hears about it.</summary>
    public static void Update(SimContext ctx)
    {
        var w = ctx.World;
        foreach (var g in w.Gifts.OrderBy(g => g.Id))
        {
            if (Find(g.Kind) is not { Ongoing: true } kind) continue;
            g.Helped += ctx.Rng.Range(3, 40);
            if (ctx.Rng.Chance(0.25))
                w.Log(Fill(ctx.Rng.Pick(kind.Years), g, w.Get(g.DonorId)), 1, "family", g.DonorId);
        }
        var p = w.Player;
        if (!p.IsAlive || p.Age(ctx.Year) < 16 || w.Gifts.Count == 0) return;
        // Decades later: a letter from someone it helped, or the family name on a wall.
        foreach (var g in w.Gifts.Where(g => ctx.Year - g.Year >= 5).OrderBy(g => g.Id))
        {
            if (!ctx.Rng.Chance(0.04)) continue;
            string situation = Find(g.Kind)!.Ongoing ? "gift_letter" : "gift_name_on_the_wall";
            var roles = new Dictionary<string, int> { ["target"] = g.DonorId };
            if (EventSystem.QueueSituation(ctx, situation, roles) is { } pending) Words(ctx, pending, g);
            break;
        }
    }

    /// <summary>{gift}, {gift_city}, {gift_years}, {gift_helped} for events about a gift.</summary>
    public static void Words(SimContext ctx, PendingEvent pending, Gift g)
    {
        pending.Words["gift"] = Mid(g);
        pending.Words["gift_city"] = g.City;
        pending.Words["gift_years"] = (ctx.Year - g.Year).ToString();
        pending.Words["gift_helped"] = g.Helped.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>A sentence for the last page, about the gift that has lasted longest.</summary>
    public static string? Epilogue(SimContext ctx)
    {
        var w = ctx.World;
        var played = w.PlayedIds.ToHashSet();
        var g = w.Gifts.Where(x => played.Contains(x.DonorId)).OrderBy(x => x.Year).FirstOrDefault();
        if (g == null) return null;
        return Find(g.Kind)!.Ongoing
            ? $"{g.Name} has helped {g.Helped.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} people since {g.Year}, and still does."
            : $"In {g.City} there is still {Mid(g)}, and people still ask who they were.";
    }
}
