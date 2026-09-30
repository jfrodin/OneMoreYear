using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Builds the family a new game starts with: two sets of grandparents, parents, maybe older
/// siblings, aunts, uncles and cousins – and the player, born this year.
/// </summary>
public static class StartingFamily
{
    public static void Create(SimContext ctx)
    {
        var w = ctx.World;
        var rng = ctx.Rng;

        var father = PersonFactory.CreateStranger(ctx, Sex.Male, rng.Range(25, 35));
        var mother = PersonFactory.CreateStranger(ctx, Sex.Female, Math.Max(20, father.Age(ctx.Year) + rng.Range(-5, 2)));
        father.AttractedToSameSex = mother.AttractedToSameSex = false;
        w.FamilyName = father.LastName;
        mother.CityId = father.CityId;

        foreach (var parent in new[] { father, mother })
        {
            MarkFamily(parent, generation: 1, blood: true);
            CreateGrandparentsAndSiblings(ctx, parent);
        }

        // The parents' relationship.
        FamilySystem.StartDating(ctx, father, mother);
        father.PartnerSinceYear = mother.PartnerSinceYear = ctx.Year - rng.Range(2, 8);
        FamilySystem.MoveIn(ctx, father, mother);
        if (rng.Chance(ctx.Year < 1975 ? 0.9 : 0.6)) FamilySystem.Marry(ctx, father, mother);

        // Older siblings.
        int siblings = rng.PickWeighted(new[] { 0, 1, 2 }, n => n switch { 0 => 0.4, 1 => 0.4, _ => 0.2 });
        int maxSiblingAge = Math.Max(1, mother.Age(ctx.Year) - 19);
        for (int i = 0; i < siblings; i++)
            CreateChildAged(ctx, father, mother, Math.Min(maxSiblingAge, rng.Range(1 + i * 2, 3 + i * 3)));

        var player = FamilySystem.HaveChild(ctx, father, mother);
        w.PlayerId = player.Id;
        w.PlayedIds.Add(player.Id);

        // Starting a new game should not fill the chronicle or memories with setup noise.
        w.Chronicle.Clear();
        foreach (var p in w.People) p.Memories.Clear();
        w.Log($"{player.FullName} was born in {ctx.Year} to {father.FullName} and {mother.FullName}.", 3, "family",
            player.Id, father.Id, mother.Id);
    }

    private static void MarkFamily(Person p, int generation, bool blood)
    {
        p.InFamily = true;
        p.IsBlood = blood;
        p.Generation = generation;
    }

    private static void CreateGrandparentsAndSiblings(SimContext ctx, Person parent)
    {
        var rng = ctx.Rng;
        int parentAge = parent.Age(ctx.Year);
        var gf = PersonFactory.CreateStranger(ctx, Sex.Male, parentAge + rng.Range(22, 34), parent.BirthLastName);
        var gm = PersonFactory.CreateStranger(ctx, Sex.Female, Math.Max(parentAge + 18, gf.Age(ctx.Year) + rng.Range(-5, 1)), parent.BirthLastName);
        gf.AttractedToSameSex = gm.AttractedToSameSex = false;
        gm.BirthLastName = rng.Pick(ctx.Country.LastNames);
        gf.CityId = gm.CityId = rng.Chance(0.7) ? parent.CityId : HousingSystem.RandomCityId(ctx);
        MarkFamily(gf, 0, true);
        MarkFamily(gm, 0, true);
        FamilySystem.StartDating(ctx, gf, gm);
        FamilySystem.MoveIn(ctx, gf, gm);
        FamilySystem.Marry(ctx, gf, gm);
        gm.LastName = gf.LastName;
        gf.PartnerSinceYear = gm.PartnerSinceYear = parent.BirthYear - rng.Range(1, 5);

        Link(ctx, gf, gm, parent);

        // Aunts and uncles, some with partners and children (cousins).
        int extra = rng.PickWeighted(new[] { 0, 1, 2 }, n => n switch { 0 => 0.3, 1 => 0.45, _ => 0.25 });
        for (int i = 0; i < extra; i++)
        {
            int age = Math.Clamp(parentAge + rng.Range(-7, 7), 16, gm.Age(ctx.Year) - 18);
            var aunt = PersonFactory.CreateStranger(ctx, rng.Chance(0.5) ? Sex.Male : Sex.Female, age, gf.LastName);
            aunt.CityId = rng.Chance(0.6) ? gf.CityId : HousingSystem.RandomCityId(ctx);
            MarkFamily(aunt, 1, true);
            Link(ctx, gf, gm, aunt);
            if (age >= 22 && rng.Chance(0.7))
            {
                var partner = FamilySystem.CreatePartnerFor(ctx, aunt);
                partner.CityId = aunt.CityId;
                FamilySystem.StartDating(ctx, aunt, partner);
                aunt.PartnerSinceYear = partner.PartnerSinceYear = ctx.Year - rng.Range(1, Math.Max(2, age - 20));
                FamilySystem.MoveIn(ctx, aunt, partner);
                if (rng.Chance(0.7)) FamilySystem.Marry(ctx, aunt, partner);
                if (aunt.Sex != partner.Sex)
                {
                    int cousins = rng.Range(0, Math.Min(3, (age - 22) / 3));
                    for (int c = 0; c < cousins; c++) CreateChildAged(ctx, aunt, partner, rng.Range(0, Math.Min(12, age - 21)));
                }
            }
        }
    }

    /// <summary>Makes <paramref name="child"/> the legal child of the given parents.</summary>
    private static void Link(SimContext ctx, Person a, Person b, Person child)
    {
        foreach (var parent in new[] { a, b })
        {
            child.ParentIds.Add(parent.Id);
            parent.ChildIds.Add(child.Id);
            PersonFactory.SetBond(ctx, parent, child, 70, 65);
            PersonFactory.SetBond(ctx, child, parent, 65, 65);
        }
        foreach (var sib in Kinship.Siblings(ctx.World, child))
        {
            PersonFactory.SetBond(ctx, child, sib, 50, 55);
            PersonFactory.SetBond(ctx, sib, child, 50, 55);
        }
    }

    private static Person CreateChildAged(SimContext ctx, Person a, Person b, int age)
    {
        var child = PersonFactory.CreateBaby(ctx, a, b);
        child.BirthYear = ctx.Year - age;
        PersonFactory.SetUpLifeStage(ctx, child);
        return child;
    }
}
