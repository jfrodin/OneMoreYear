using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>School, studies, jobs, promotions and retirement.</summary>
public static class CareerSystem
{
    public static void Update(SimContext ctx, Person p, double extraJobLossChance)
    {
        var rng = ctx.Rng;
        int age = p.Age(ctx.Year);
        bool isPlayer = p.Id == ctx.World.PlayerId;

        if (p.Activity == Activity.Child && age >= 7)
        {
            p.Activity = Activity.School;
            return;
        }

        if (p.Activity == Activity.School && age >= 16)
        {
            p.Education = EducationLevel.Primary;
            if (isPlayer)
            {
                EventSystem.QueueSituation(ctx, "after_primary");
                return;
            }
            double chance = 0.55 + Math.Clamp((ctx.Year - 1950) * 0.008, 0, 0.35) + ctx.Mod(p, "career") * 0.2;
            if (rng.Chance(chance)) StartStudies(ctx, p, EducationLevel.Secondary);
            else BecomeJobSeeker(p, ctx);
            return;
        }

        if (p.Activity == Activity.Studying)
        {
            p.StudyYearsLeft--;
            if (p.StudyYearsLeft > 0) return;
            p.Education = p.StudyingFor ?? p.Education;
            p.StudyingFor = null;
            if (p.InFamily)
                ctx.World.Log($"{p.FirstName} graduated from {EducationName(p.Education)}.", ctx.Importance(false, p), "education", p.Id);

            if (p.Education == EducationLevel.Secondary)
            {
                if (isPlayer)
                {
                    BecomeJobSeeker(p, ctx);
                    EventSystem.QueueSituation(ctx, "after_secondary");
                    return;
                }
                double uni = 0.1 + Math.Clamp((ctx.Year - 1950) * 0.006, 0, 0.3) + ctx.Mod(p, "career") * 0.25
                             + (Kinship.Parents(ctx.World, p).Any(x => x.Education == EducationLevel.University) ? 0.15 : 0);
                if (rng.Chance(uni)) { StartStudies(ctx, p, EducationLevel.University); return; }
            }
            BecomeJobSeeker(p, ctx);
        }

        if (age >= ctx.Country.PensionAge && p.Activity is Activity.Working or Activity.Unemployed)
        {
            Retire(ctx, p);
            return;
        }

        if (p.Activity == Activity.Unemployed && age >= 16)
        {
            double chance = 0.55 + ctx.Mod(p, "career") * 0.3 + (int)p.Education * 0.05;
            if (rng.Chance(chance)) Hire(ctx, p);
            return;
        }

        if (p.Activity == Activity.Working) UpdateJob(ctx, p, extraJobLossChance);
    }

    private static void UpdateJob(SimContext ctx, Person p, double extraJobLossChance)
    {
        var rng = ctx.Rng;
        var occ = ctx.Content.Occupation(p.OccupationId);
        if (occ == null) { BecomeJobSeeker(p, ctx); return; }
        bool isPlayer = p.Id == ctx.World.PlayerId;

        p.YearsInJob++;
        double target = 50 + ctx.Mod(p, "career") * 30 + (p.Health < 40 ? -15 : 0) + rng.Gaussian(0, 15);
        p.Performance = Math.Clamp(p.Performance * 0.7 + target * 0.3, 0, 100);

        double fireChance = 0.015 + (p.Performance < 30 ? 0.08 : 0) + extraJobLossChance;
        if (rng.Chance(fireChance))
        {
            if (p.InFamily)
                ctx.World.Log($"{p.FirstName} lost {(p.Sex == Sex.Male ? "his" : "her")} job as {Article(Title(ctx, p))}.", ctx.Importance(false, p), "career", p.Id);
            p.Happiness -= 15;
            BecomeJobSeeker(p, ctx);
            return;
        }

        if (p.OccupationLevel + 1 < occ.Levels.Count && p.YearsInJob >= 2)
        {
            var next = occ.Levels[p.OccupationLevel + 1];
            var level = occ.Levels[p.OccupationLevel];
            double chance = level.PromotionChance * Math.Pow(p.Performance / 50.0, 2);
            if (next.MinEducation <= p.Education && rng.Chance(chance))
            {
                Promote(ctx, p);
                return;
            }
        }
        p.Income = occ.Levels[p.OccupationLevel].Salary;
    }

    public static void Promote(SimContext ctx, Person p)
    {
        var occ = ctx.Content.Occupation(p.OccupationId);
        if (occ == null || p.OccupationLevel + 1 >= occ.Levels.Count) return;
        p.OccupationLevel++;
        p.YearsInJob = 0;
        p.Income = occ.Levels[p.OccupationLevel].Salary;
        p.Happiness += 8;
        if (p.InFamily)
            ctx.World.Log($"{p.FirstName} was promoted to {JobName(Title(ctx, p))}.", ctx.Importance(false, p), "career", p.Id);
    }

    public static void StartStudies(SimContext ctx, Person p, EducationLevel level)
    {
        p.Activity = Activity.Studying;
        p.StudyingFor = level;
        p.StudyYearsLeft = level == EducationLevel.University ? ctx.Rng.Range(3, 5) : 3;
        p.OccupationId = null;
        p.Income = level == EducationLevel.University ? ctx.Country.StudentIncome : 0;
    }

    public static void BecomeJobSeeker(Person p, SimContext ctx)
    {
        p.Activity = Activity.Unemployed;
        p.OccupationId = null;
        p.OccupationLevel = 0;
        p.YearsInJob = 0;
        p.Income = ctx.Country.UnemploymentIncome;
    }

    public static void Retire(SimContext ctx, Person p)
    {
        double last = p.Activity == Activity.Working ? p.Income : ctx.Country.UnemploymentIncome;
        p.Activity = Activity.Retired;
        p.OccupationId = null;
        p.Income = Math.Max(ctx.Country.MinimumPension, last * ctx.Country.PensionRate);
        if (p.InFamily)
            ctx.World.Log($"{p.FirstName} retired.", ctx.Importance(false, p), "career", p.Id);
    }

    /// <summary>Finds a job for the person. Returns false if nothing fits their education.</summary>
    public static bool Hire(SimContext ctx, Person p, string? occupationId = null)
    {
        var options = ctx.Content.Occupations
            .Where(o => occupationId == null || o.Id == occupationId)
            .Where(o => o.Levels.Any(l => l.Entry && l.MinEducation <= p.Education))
            .ToList();
        if (options.Count == 0) return false;

        var occ = ctx.Rng.PickWeighted(options, o =>
        {
            double w = o.Weight;
            foreach (var t in p.Traits)
                if (o.TraitAffinity.TryGetValue(t, out var m)) w *= m;
            // People mostly take jobs that use their education; a degree rarely ends in a job that needs none.
            var best = o.Levels.Where(l => l.Entry && l.MinEducation <= p.Education).Max(l => l.MinEducation);
            int unused = (int)p.Education - (int)best;
            w *= unused switch { 0 => 4, 1 => 0.4, 2 => 0.1, _ => 0.03 };
            return w;
        })!;

        int level = 0;
        for (int i = 0; i < occ.Levels.Count; i++)
            if (occ.Levels[i].Entry && occ.Levels[i].MinEducation <= p.Education) level = i;

        p.Activity = Activity.Working;
        p.OccupationId = occ.Id;
        p.OccupationLevel = level;
        p.YearsInJob = 0;
        p.Performance = Math.Clamp(ctx.Rng.Gaussian(50, 10), 20, 80);
        p.Income = occ.Levels[level].Salary;
        if (p.InFamily && p.Age(ctx.Year) >= 16)
            ctx.World.Log($"{p.FirstName} got a job as {Article(Title(ctx, p))}.", ctx.Importance(false, p), "career", p.Id);
        return true;
    }

    public static string Title(SimContext ctx, Person p)
    {
        var occ = ctx.Content.Occupation(p.OccupationId);
        return occ == null ? "" : occ.Levels[Math.Min(p.OccupationLevel, occ.Levels.Count - 1)].Title;
    }

    /// <summary>"a nurse", "an engineer" – lower-cased job title with the right article.</summary>
    public static string Article(string title)
    {
        var t = JobName(title);
        return ("aeiouAEIOU".Contains(t.Length > 0 ? t[0] : 'x') ? "an " : "a ") + t;
    }

    /// <summary>Lower-cases a job title for use mid-sentence, but keeps acronyms like "CTO".</summary>
    public static string JobName(string title) =>
        title.Length > 1 && char.IsUpper(title[1]) ? title : title.ToLowerInvariant();

    public static string EducationName(EducationLevel level) => level switch
    {
        EducationLevel.Primary => "primary school",
        EducationLevel.Secondary => "upper secondary school",
        EducationLevel.University => "university",
        _ => "no education"
    };

    /// <summary>A short description of what the person does ("Nurse", "Studying", ...).</summary>
    public static string ActivityText(SimContext ctx, Person p) => p.Activity switch
    {
        Activity.Child => "Child",
        Activity.School => p.Age(ctx.Year) < 16 ? "In school" : "Deciding what to do next",
        Activity.Studying => p.StudyingFor == EducationLevel.University ? "At university" : "In upper secondary school",
        Activity.Working => Title(ctx, p),
        Activity.Unemployed => "Looking for work",
        Activity.Retired => "Retired",
        _ => ""
    };
}
