using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Pets (docs/sims-inspiration.md): dogs and cats with a name, an age and a nature. A pet makes its
/// home a little happier, teaches the children in it a little kindness, and one day dies, which the
/// whole family remembers. If the owner dies first, someone in the home takes it on.
/// </summary>
public static class PetSystem
{
    public const string DiedEvent = "pet_died";

    /// <summary>The living pets in a person's home: their own, or their partner's.</summary>
    public static IEnumerable<Pet> InHome(World w, Person p)
    {
        int? partner = p.PartnerStatus is PartnerStatus.Cohabiting or PartnerStatus.Married ? p.PartnerId : null;
        var parents = p.LivesWithParents ? p.ParentIds : new List<int>();
        return w.Pets.Where(x => x.IsAlive && (x.OwnerId == p.Id || x.OwnerId == partner || parents.Contains(x.OwnerId)));
    }

    public static Pet? Adopt(SimContext ctx, Person owner, string kind)
    {
        var w = ctx.World;
        if (!ctx.Content.PetKinds.TryGetValue(kind, out var def) || def.Names.Count == 0) return null;
        var taken = w.Pets.Where(x => x.IsAlive).Select(x => x.Name).ToHashSet();
        var names = def.Names.Where(n => !taken.Contains(n)).ToList();
        var pet = new Pet
        {
            Id = w.Pets.Count + 1, Kind = kind, Name = ctx.Rng.Pick(names.Count > 0 ? names : def.Names),
            BirthYear = ctx.Year - ctx.Rng.Range(0, 3), OwnerId = owner.Id,
            Nature = ctx.Rng.Pick(def.Natures.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList()),
        };
        w.Pets.Add(pet);
        w.Log($"{owner.FirstName} got a {def.Name}: {pet.Name}.", ctx.Importance(false, owner), "family", owner.Id);
        return pet;
    }

    /// <summary>"Bella the dog, 7, who steals socks and brings them back".</summary>
    public static string Describe(SimContext ctx, Pet pet)
    {
        var def = ctx.Content.PetKinds.GetValueOrDefault(pet.Kind);
        string nature = def?.Natures.GetValueOrDefault(pet.Nature) ?? "";
        return $"{pet.Name} the {def?.Name ?? pet.Kind}, {ctx.Year - pet.BirthYear}" + (nature.Length > 0 ? $", who {nature}" : "");
    }

    public static void Update(SimContext ctx)
    {
        var w = ctx.World;
        foreach (var pet in w.Pets.Where(x => x.IsAlive).ToList())
        {
            var owner = w.Get(pet.OwnerId);
            // The owner is gone: someone in the home takes the pet on, or it goes to a new family.
            if (!owner.IsAlive)
            {
                var heir = w.TryGet(owner.PartnerId) is { IsAlive: true } partner ? partner
                    : Kinship.Children(w, owner).Where(c => c.IsAlive && c.Age(ctx.Year) >= 12).OrderByDescending(c => c.LivesWithParents).FirstOrDefault();
                if (heir == null)
                {
                    pet.DeathYear = ctx.Year;
                    continue;
                }
                pet.OwnerId = heir.Id;
                owner = heir;
            }
            int age = ctx.Year - pet.BirthYear;
            int lifespan = ctx.Content.PetKinds.GetValueOrDefault(pet.Kind)?.Lifespan ?? 12;
            double death = age < lifespan - 4 ? 0.01 : 0.08 + (age - (lifespan - 4)) * 0.07;
            if (ctx.Rng.Chance(death))
            {
                Die(ctx, pet, owner);
                continue;
            }
            // A pet in the home: a little happier, and children grow a little kinder.
            owner.Happiness = Math.Min(100, owner.Happiness + 1.5);
            foreach (var child in Kinship.Children(w, owner).Where(c => c.IsAlive && c.LivesWithParents && c.Age(ctx.Year) < 18))
                child.Empathy = Math.Min(50, child.Empathy + 0.5);
        }
    }

    private static void Die(SimContext ctx, Pet pet, Person owner)
    {
        var w = ctx.World;
        pet.DeathYear = ctx.Year;
        int age = ctx.Year - pet.BirthYear;
        string kind = ctx.Content.PetKinds.GetValueOrDefault(pet.Kind)?.Name ?? pet.Kind;
        w.Log($"{pet.Name}, {Kinship.Genitive(owner.FirstName)} {kind}, died at {age}.", ctx.Importance(false, owner), "family", owner.Id);
        var family = new List<Person> { owner };
        family.AddRange(Kinship.Children(w, owner).Where(c => c.IsAlive && c.LivesWithParents));
        if (w.TryGet(owner.PartnerId) is { IsAlive: true } partner) family.Add(partner);
        foreach (var x in family.Where(x => x.Age(ctx.Year) >= 4))
        {
            RelationshipSystem.AddMemory(ctx, x, "pet", $"{pet.Name} the {kind}", 12);
            x.Happiness = Math.Max(0, x.Happiness - 4);
        }
        if (family.Any(x => x.Id == w.PlayerId) && EventSystem.QueueSituation(ctx, DiedEvent) is { } pending)
            Fill(ctx, pending, pet);
    }

    /// <summary>Gives an event the words for a pet: {pet}, {pet_kind}, {pet_age}, {pet_nature}.</summary>
    public static void Fill(SimContext ctx, PendingEvent pending, Pet pet)
    {
        var def = ctx.Content.PetKinds.GetValueOrDefault(pet.Kind);
        pending.Words["pet"] = pet.Name;
        pending.Words["pet_kind"] = def?.Name ?? pet.Kind;
        pending.Words["pet_age"] = (ctx.Year - pet.BirthYear).ToString();
        pending.Words["pet_nature"] = def?.Natures.GetValueOrDefault(pet.Nature) ?? "";
        pending.Words["pet_id"] = pet.Id.ToString();
    }

    /// <summary>For the "pet" condition: the pet an event is about, if the player has one of that kind.</summary>
    public static Pet? Matching(SimContext ctx, Person p, string kind) =>
        InHome(ctx.World, p).Where(x => kind == "any" || x.Kind == kind).OrderBy(x => x.Id).FirstOrDefault();
}
