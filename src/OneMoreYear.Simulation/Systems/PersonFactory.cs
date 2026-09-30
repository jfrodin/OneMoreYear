using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>Creates new people: newborns, partners, friends and the starting family.</summary>
public static class PersonFactory
{
    /// <summary>A first name that no living family member already has, so stories stay easy to follow.</summary>
    public static string RandomFirstName(SimContext ctx, Sex sex)
    {
        var names = sex == Sex.Male ? ctx.Country.MaleNames : ctx.Country.FemaleNames;
        var taken = ctx.World.People.Where(p => p.IsAlive && p.InFamily).Select(p => p.FirstName).ToHashSet();
        for (int i = 0; i < 12; i++)
        {
            var name = ctx.Rng.Pick(names);
            if (!taken.Contains(name)) return name;
        }
        return ctx.Rng.Pick(names);
    }

    /// <summary>Creates an adult (or child) with no parents in the world.</summary>
    public static Person CreateStranger(SimContext ctx, Sex sex, int age, string? lastName = null)
    {
        var rng = ctx.Rng;
        var p = new Person
        {
            Sex = sex,
            FirstName = RandomFirstName(ctx, sex),
            LastName = lastName ?? rng.Pick(ctx.Country.LastNames),
            BirthYear = ctx.Year - age,
            AttractedToSameSex = rng.Chance(ctx.Country.SameSexCoupleChance),
            Health = Math.Clamp(rng.Gaussian(92 - Math.Max(0, age - 30) * 0.6, 6), 20, 100),
            Happiness = rng.Range(45, 75),
        };
        // Strangers mostly live where the player lives – that is where the player meets them.
        p.CityId = ctx.World.TryGet(ctx.World.PlayerId) is { } pl && rng.Chance(0.8) ? pl.CityId : HousingSystem.RandomCityId(ctx);
        p.LivesWithParents = age < 20;
        p.BirthLastName = p.LastName;
        AssignTraits(ctx, p, Array.Empty<Person>());
        Appearance.Generate(ctx, p, Array.Empty<Person>());
        if (age >= ctx.Country.AdultAge) RollAdultTraits(ctx, p);
        p.Grades = Math.Clamp(ctx.Rng.Gaussian(45 + (p.Smarts - 50) * 0.6, 12), 5, 100);
        ctx.World.AddPerson(p);
        SetUpLifeStage(ctx, p);
        return p;
    }

    /// <summary>A child born this year to the given legal parents.</summary>
    public static Person CreateBaby(SimContext ctx, Person parentA, Person? parentB, int? biologicalFatherId = null)
    {
        var rng = ctx.Rng;
        var sex = rng.Chance(0.51) ? Sex.Male : Sex.Female;
        var father = parentA.Sex == Sex.Male ? parentA : parentB?.Sex == Sex.Male ? parentB : null;
        var lastName = (father ?? parentA).LastName;

        var p = new Person
        {
            Sex = sex,
            FirstName = RandomFirstName(ctx, sex),
            LastName = lastName,
            BirthLastName = lastName,
            BirthYear = ctx.Year,
            AttractedToSameSex = rng.Chance(ctx.Country.SameSexCoupleChance),
            Health = Math.Clamp(rng.Gaussian(94, 5), 30, 100),
            Happiness = 70,
            Activity = Activity.Child,
            InFamily = parentA.InFamily || (parentB?.InFamily ?? false),
            IsBlood = parentA.IsBlood || (parentB?.IsBlood ?? false),
            Generation = Math.Max(parentA.Generation, parentB?.Generation ?? 0) + 1,
            BiologicalFatherId = biologicalFatherId,
            CityId = parentA.CityId,
            LivesWithParents = true,
        };
        var geneticParents = new List<Person> { parentA };
        if (parentB != null) geneticParents.Add(parentB);
        if (biologicalFatherId is { } bio)
        {
            geneticParents.RemoveAll(x => x.Sex == Sex.Male);
            geneticParents.Add(ctx.World.Get(bio));
        }
        AssignTraits(ctx, p, geneticParents);
        Appearance.Generate(ctx, p, geneticParents);
        p.Grades = Math.Clamp(45 + (p.Smarts - 50) * 0.6 + ctx.Rng.Gaussian(0, 10), 5, 100);
        ctx.World.AddPerson(p);

        foreach (var parent in new[] { parentA, parentB })
        {
            if (parent == null) continue;
            p.ParentIds.Add(parent.Id);
            parent.ChildIds.Add(p.Id);
            SetBond(ctx, parent, p, closeness: 80, trust: 70);
            SetBond(ctx, p, parent, closeness: 85, trust: 85);
        }
        foreach (var sib in Kinship.Siblings(ctx.World, p).Where(s => s.IsAlive))
        {
            SetBond(ctx, p, sib, closeness: 55, trust: 60);
            SetBond(ctx, sib, p, closeness: 55, trust: 60);
        }
        return p;
    }

    public static void SetBond(SimContext ctx, Person from, Person to, double closeness, double trust, double attraction = 0)
    {
        var r = ctx.World.Rel(from.Id, to.Id);
        r.Closeness = Math.Clamp(closeness + ctx.Rng.Gaussian(0, 8), 0, 100);
        r.Trust = Math.Clamp(trust + ctx.Rng.Gaussian(0, 8), 0, 100);
        r.Respect = Math.Clamp(50 + ctx.Rng.Gaussian(0, 10), 0, 100);
        r.Attraction = attraction;
        r.LastContactYear = ctx.Year;
    }

    /// <summary>Gives 1–4 traits, partly inherited from the given parents. Opposites never combine.</summary>
    public static void AssignTraits(SimContext ctx, Person p, IReadOnlyList<Person> parents)
    {
        var rng = ctx.Rng;
        // Most people have two or three traits; some are simple, some complicated.
        int count = rng.PickWeighted(new[] { 1, 2, 3, 4 }, n => n switch { 1 => 0.15, 2 => 0.4, 3 => 0.35, _ => 0.1 });
        var pool = ctx.Content.Traits.Values.Where(t => !t.AdultOnly).OrderBy(t => t.Id, StringComparer.Ordinal).ToList();

        foreach (var parent in parents)
            foreach (var t in parent.Traits)
                if (p.Traits.Count < count && ctx.Content.Traits.TryGetValue(t, out var def) && !def.AdultOnly && rng.Chance(def.Inherit))
                    TryAddTrait(ctx, p, t);

        int guard = 0;
        while (p.Traits.Count < count && guard++ < 50) TryAddTrait(ctx, p, rng.PickWeighted(pool, t => t.Weight)!.Id);
    }

    /// <summary>Traits that only show in adulthood, like partner preferences. Rolled once, at 18.</summary>
    public static void RollAdultTraits(SimContext ctx, Person p)
    {
        foreach (var def in ctx.Content.Traits.Values.Where(t => t.AdultOnly).OrderBy(t => t.Id, StringComparer.Ordinal))
        {
            double sexWeight = p.Sex == Sex.Male ? def.MaleWeight : def.FemaleWeight;
            if (ctx.Rng.Chance(def.Weight * sexWeight * 0.12)) TryAddTrait(ctx, p, def.Id);
        }
    }

    public static bool TryAddTrait(SimContext ctx, Person p, string trait)
    {
        if (p.Traits.Contains(trait) || !ctx.Content.Traits.TryGetValue(trait, out var def)) return false;
        if (def.Opposite != null && p.Traits.Contains(def.Opposite)) return false;
        if (ctx.Content.Traits.Values.Any(t => t.Opposite == trait && p.Traits.Contains(t.Id))) return false;
        p.Traits.Add(trait);
        return true;
    }

    /// <summary>Sets education, job and savings so a generated person fits their age.</summary>
    public static void SetUpLifeStage(SimContext ctx, Person p)
    {
        var rng = ctx.Rng;
        int age = p.Age(ctx.Year);
        if (age < 7) { p.Activity = Activity.Child; return; }
        if (age < 16) { p.Activity = Activity.School; return; }
        p.Education = EducationLevel.Primary;
        if (age < 19)
        {
            if (CareerSystem.ChooseProgramme(ctx, p, EducationLevel.Secondary) is { } teenProg)
            {
                CareerSystem.StartStudies(ctx, p, teenProg.Id);
                p.StudyYearsLeft = 19 - age;
            }
            else p.Activity = Activity.School;
            return;
        }

        // Higher education became much more common during the 1900s.
        double uniChance = 0.08 + Math.Clamp((p.BirthYear - 1930) * 0.005, 0, 0.35) + ctx.Mod(p, "career") * 0.2
                           + (p.Grades - 50) / 200;
        if (rng.Chance(0.8) && CareerSystem.ChooseProgramme(ctx, p, EducationLevel.Secondary) is { } sec)
        {
            p.Education = EducationLevel.Secondary;
            p.Degrees.Add(sec.Id);
            if (rng.Chance(uniChance) && CareerSystem.ChooseProgramme(ctx, p, EducationLevel.University) is { } major)
            {
                if (age < 19 + major.Years)
                {
                    CareerSystem.StartStudies(ctx, p, major.Id);
                    p.StudyYearsLeft = 19 + major.Years - age;
                }
                else
                {
                    p.Education = EducationLevel.University;
                    p.Degrees.Add(major.Id);
                }
            }
        }

        if (p.Activity == Activity.Studying) { }
        else if (age >= ctx.Country.PensionAge)
        {
            p.Activity = Activity.Retired;
            p.Income = Math.Max(ctx.Country.MinimumPension, 300000 * ctx.Country.PensionRate);
        }
        else if (rng.Chance(0.9) && CareerSystem.Hire(ctx, p))
        {
            // Experienced people have usually climbed a bit.
            var occ = ctx.Content.Occupation(p.OccupationId)!;
            int climbs = rng.Next(1 + (age - 20) / 8);
            for (int i = 0; i < climbs; i++)
            {
                if (p.OccupationLevel + 1 >= occ.Levels.Count) break;
                if (!CareerSystem.QualifiesFor(p, occ.Levels[p.OccupationLevel + 1])) break;
                p.OccupationLevel++;
            }
            p.Income = occ.Levels[p.OccupationLevel].Salary;
            p.YearsInJob = rng.Range(0, Math.Max(0, age - 20));
        }
        else
        {
            p.Activity = Activity.Unemployed;
            p.Income = ctx.Country.UnemploymentIncome;
        }

        double yearsOfSaving = Math.Max(0, age - 22);
        p.Money = ctx.Nominal(rng.Range(-0.05, 0.25) * yearsOfSaving * 40000);
        if (age > 30 && rng.Chance(0.55))
        {
            // Bought some years ago; part of the loan is paid off.
            double price = HousingSystem.HomePrice(ctx, p);
            EconomySystem.GiveHome(p, price, price * (1 - ctx.Country.DownPayment) * Math.Max(0.2, 1 - (age - 30) * 0.03));
        }
    }
}
