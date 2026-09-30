using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>School, grades, programmes and degrees, jobs, promotions and retirement.</summary>
public static class CareerSystem
{
    public const string PartTimeFlag = "part_time_job";

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

        if (p.Activity is Activity.School or Activity.Studying) UpdateGrades(ctx, p);

        if (p.Activity == Activity.School && age >= 16)
        {
            p.Education = EducationLevel.Primary;
            if (isPlayer)
            {
                EventSystem.QueueSituation(ctx, "after_primary");
                return;
            }
            double chance = 0.55 + Math.Clamp((ctx.Year - 1950) * 0.008, 0, 0.35) + ctx.Mod(p, "career") * 0.2;
            if (rng.Chance(chance) && ChooseProgramme(ctx, p, EducationLevel.Secondary) is { } prog) StartStudies(ctx, p, prog.Id);
            else BecomeJobSeeker(p, ctx);
            return;
        }

        if (p.Activity == Activity.Studying)
        {
            p.StudyYearsLeft--;
            if (p.StudyYearsLeft > 0) return;
            Graduate(ctx, p);

            if (p.Education == EducationLevel.Secondary)
            {
                if (isPlayer)
                {
                    BecomeJobSeeker(p, ctx);
                    EventSystem.QueueSituation(ctx, "after_secondary");
                    return;
                }
                double uni = 0.1 + Math.Clamp((ctx.Year - 1950) * 0.006, 0, 0.3) + ctx.Mod(p, "career") * 0.25
                             + (Kinship.Parents(ctx.World, p).Any(x => x.Education == EducationLevel.University) ? 0.15 : 0)
                             + (p.Grades - 50) / 150;
                if (rng.Chance(uni) && ChooseProgramme(ctx, p, EducationLevel.University) is { } major)
                {
                    StartStudies(ctx, p, major.Id);
                    return;
                }
            }
            BecomeJobSeeker(p, ctx);
            if (isPlayer) { QueueJobOffers(ctx, p); return; }
        }

        if (age >= ctx.Country.PensionAge && p.Activity is Activity.Working or Activity.Unemployed)
        {
            Retire(ctx, p);
            return;
        }

        if (p.Activity == Activity.Unemployed && age >= 16)
        {
            if (isPlayer) { QueueJobOffers(ctx, p); return; }
            double chance = 0.55 + ctx.Mod(p, "career") * 0.3 + (int)p.Education * 0.05;
            if (rng.Chance(chance)) Hire(ctx, p);
            return;
        }

        if (p.Activity == Activity.Working) UpdateJob(ctx, p, extraJobLossChance);
    }

    // --- School and studies -----------------------------------------------------------------

    private static void UpdateGrades(SimContext ctx, Person p)
    {
        double target = 50 + ctx.Mod(p, "career") * 20 + (p.Smarts - 50) * 0.7
                        + (p.Happiness < 30 ? -8 : 0) + (p.Flags.Contains(PartTimeFlag) ? -6 : 0);
        p.Grades = Math.Clamp(p.Grades * 0.7 + target * 0.3 + ctx.Rng.Gaussian(0, 4), 0, 100);
    }

    /// <summary>Can this person get into the programme? University needs a diploma and good enough grades.</summary>
    public static bool CanEnter(SimContext ctx, Person p, ProgrammeDef prog)
    {
        if (p.Degrees.Contains(prog.Id)) return false;
        if (prog.Level == EducationLevel.Secondary) return p.Education >= EducationLevel.Primary;
        if (p.Education < EducationLevel.Secondary) return false;
        return p.Grades >= RequiredGrades(ctx, p, prog);
    }

    /// <summary>Vocational secondary programmes need 10 extra grade points for university.</summary>
    public static double RequiredGrades(SimContext ctx, Person p, ProgrammeDef prog)
    {
        if (prog.Level != EducationLevel.University) return 0;
        bool academic = p.Degrees.Select(ctx.Content.Programme)
            .Any(d => d is { Academic: true } || d?.Level == EducationLevel.University);
        return prog.MinGrades + (academic || p.Degrees.Count == 0 ? 0 : 10);
    }

    public static ProgrammeDef? ChooseProgramme(SimContext ctx, Person p, EducationLevel level)
    {
        var options = ctx.Content.Programmes.Values
            .Where(pr => pr.Level == level && CanEnter(ctx, p, pr))
            .OrderBy(pr => pr.Id, StringComparer.Ordinal)
            .ToList();
        return ctx.Rng.PickWeighted(options, pr =>
        {
            double w = 1;
            foreach (var t in p.Traits)
                if (pr.TraitAffinity.TryGetValue(t, out var m)) w *= m;
            if (pr.Academic) w *= Math.Pow(Math.Max(0.2, p.Grades / 50), 2) * (0.6 + Math.Clamp((ctx.Year - 1950) * 0.02, 0, 1.4));
            return w;
        });
    }

    public static void StartStudies(SimContext ctx, Person p, string programmeId)
    {
        var prog = ctx.Content.Programme(programmeId) ?? throw new InvalidDataException($"Unknown programme {programmeId}");
        p.Activity = Activity.Studying;
        p.StudyingFor = prog.Level;
        p.ProgrammeId = prog.Id;
        p.StudyYearsLeft = prog.Years;
        p.OccupationId = null;
        p.Income = prog.Level == EducationLevel.University ? ctx.Country.StudentIncome : 0;
    }

    private static void Graduate(SimContext ctx, Person p)
    {
        p.Education = p.StudyingFor ?? p.Education;
        p.StudyingFor = null;
        var prog = ctx.Content.Programme(p.ProgrammeId);
        if (prog != null && !p.Degrees.Contains(prog.Id)) p.Degrees.Add(prog.Id);
        p.ProgrammeId = null;
        p.Flags.Remove(PartTimeFlag);
        if (p.InFamily)
            ctx.World.Log(prog != null
                    ? $"{p.FirstName} graduated from the {prog.Name.ToLowerInvariant()}{(prog.Level == EducationLevel.University ? " programme at university" : "")}."
                    : $"{p.FirstName} graduated from {EducationName(p.Education)}.",
                ctx.Importance(false, p), "education", p.Id);
    }

    // --- Jobs ---------------------------------------------------------------------------------

    public static bool HasDegreeFor(Person p, OccupationLevelDef level) =>
        level.RequiresDegree is not { Count: > 0 } req || req.Any(p.Degrees.Contains);

    public static bool QualifiesFor(Person p, OccupationLevelDef level) =>
        level.MinEducation <= p.Education && HasDegreeFor(p, level);

    /// <summary>The best level someone can be hired into directly, or -1.</summary>
    public static int EntryLevel(Person p, OccupationDef occ)
    {
        int best = -1;
        for (int i = 0; i < occ.Levels.Count; i++)
            if (occ.Levels[i].Entry && QualifiesFor(p, occ.Levels[i])) best = i;
        return best;
    }

    /// <summary>Whether the person's degrees point to this field.</summary>
    public static bool FitsDegree(SimContext ctx, Person p, OccupationDef occ) =>
        p.Degrees.Any(d => ctx.Content.Programme(d)?.LeadsTo.Contains(occ.Id) == true);

    private static double OccupationWeight(SimContext ctx, Person p, OccupationDef o)
    {
        double w = o.Weight;
        foreach (var t in p.Traits)
            if (o.TraitAffinity.TryGetValue(t, out var m)) w *= m;
        // People mostly take jobs that use their education; a degree rarely ends in a job that needs none.
        int level = EntryLevel(p, o);
        int unused = (int)p.Education - (int)o.Levels[level].MinEducation;
        w *= unused switch { 0 => 4, 1 => 0.4, 2 => 0.1, _ => 0.03 };
        if (FitsDegree(ctx, p, o)) w *= 12;
        return w;
    }

    /// <summary>Finds a job for the person. Returns false if nothing fits their education.</summary>
    public static bool Hire(SimContext ctx, Person p, string? occupationId = null, int? level = null)
    {
        var options = ctx.Content.Occupations
            .Where(o => occupationId == null || o.Id == occupationId)
            .Where(o => EntryLevel(p, o) >= 0 || level != null)
            .ToList();
        if (options.Count == 0) return false;
        var occ = occupationId != null ? options[0] : ctx.Rng.PickWeighted(options, o => OccupationWeight(ctx, p, o))!;
        int lvl = Math.Clamp(level ?? EntryLevel(p, occ), 0, occ.Levels.Count - 1);

        p.Activity = Activity.Working;
        p.OccupationId = occ.Id;
        p.OccupationLevel = lvl;
        p.YearsInJob = 0;
        p.Performance = Math.Clamp(ctx.Rng.Gaussian(50, 10), 20, 80);
        p.Income = occ.Levels[lvl].Salary;
        if (p.Id == ctx.World.PlayerId) SocialSystem.OnNewJob(p, ctx.Year);
        if (p.InFamily && p.Age(ctx.Year) >= 16)
            ctx.World.Log($"{p.FirstName} got a job as {Article(Title(ctx, p))}.", ctx.Importance(false, p), "career", p.Id);
        return true;
    }

    /// <summary>Job offers for the player to choose between, as "occupationId:level".</summary>
    public static List<string> GenerateOffers(SimContext ctx, Person p)
    {
        var rng = ctx.Rng;
        int max = 1 + (p.Education >= EducationLevel.Secondary ? 1 : 0) + (p.Education == EducationLevel.University ? 1 : 0)
                  + (p.Grades > 70 ? 1 : 0);
        double quality = 0.55 + ctx.Mod(p, "career") * 0.2 + (p.Grades - 50) / 200;
        int count = 0;
        for (int i = 0; i < max; i++) if (rng.Chance(quality)) count++;
        if (count == 0 && rng.Chance(0.5)) count = 1; // something simple usually turns up

        var pool = ctx.Content.Occupations.Where(o => EntryLevel(p, o) >= 0 && o.Id != p.OccupationId).ToList();
        var offers = new List<string>();
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            var occ = rng.PickWeighted(pool, o => OccupationWeight(ctx, p, o))!;
            pool.Remove(occ);
            offers.Add($"{occ.Id}:{EntryLevel(p, occ)}");
        }
        return offers;
    }

    /// <summary>The player applies for jobs: offers become an event to choose from, or a note that nobody answered.</summary>
    public static void QueueJobOffers(SimContext ctx, Person p)
    {
        if (ctx.World.PendingEvents.Any(e => e.EventId == "job_offers" && !e.Resolved)) return;
        var offers = GenerateOffers(ctx, p);
        if (offers.Count == 0)
        {
            ctx.World.Log($"{p.FirstName} applied for jobs but got no offers this time.", ctx.Importance(false, p), "career", p.Id);
            return;
        }
        var pending = EventSystem.QueueSituation(ctx, "job_offers");
        if (pending != null) pending.Options = offers;
    }

    public static (OccupationDef Occ, int Level)? ParseOffer(SimContext ctx, string offer)
    {
        var parts = offer.Split(':');
        if (parts.Length != 2 || ctx.Content.Occupation(parts[0]) is not { } occ || !int.TryParse(parts[1], out var lvl)) return null;
        return (occ, Math.Clamp(lvl, 0, occ.Levels.Count - 1));
    }

    private static void UpdateJob(SimContext ctx, Person p, double extraJobLossChance)
    {
        var rng = ctx.Rng;
        var occ = ctx.Content.Occupation(p.OccupationId);
        if (occ == null) { BecomeJobSeeker(p, ctx); return; }

        p.YearsInJob++;
        double target = 50 + ctx.Mod(p, "career") * 30 + (p.Smarts - 50) * 0.3 + (p.Health < 40 ? -15 : 0) + rng.Gaussian(0, 15);
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

        if (rng.Chance(PromotionChance(ctx, p)))
        {
            Promote(ctx, p);
            return;
        }
        p.Income = occ.Levels[p.OccupationLevel].Salary;
    }

    /// <summary>This year's chance of a promotion (0 if the next level needs a degree you don't have).</summary>
    public static double PromotionChance(SimContext ctx, Person p)
    {
        var occ = ctx.Content.Occupation(p.OccupationId);
        if (occ == null || p.Activity != Activity.Working || p.OccupationLevel + 1 >= occ.Levels.Count || p.YearsInJob < 2) return 0;
        if (!QualifiesFor(p, occ.Levels[p.OccupationLevel + 1])) return 0;
        double chance = occ.Levels[p.OccupationLevel].PromotionChance * Math.Pow(p.Performance / 50.0, 2);
        // A boss who likes you helps; one who doesn't, holds you back.
        if (p.Acquaintances.FirstOrDefault(a => a.Current && a.Kind == "boss") is { } boss)
            chance *= Math.Clamp(1 + ctx.World.Opinion(boss.Id, p.Id) / 80, 0.3, 1.8);
        return chance;
    }

    public static void Promote(SimContext ctx, Person p)
    {
        var occ = ctx.Content.Occupation(p.OccupationId);
        if (occ == null || p.OccupationLevel + 1 >= occ.Levels.Count) return;
        if (!QualifiesFor(p, occ.Levels[p.OccupationLevel + 1])) return;
        p.OccupationLevel++;
        p.YearsInJob = 0;
        p.Income = occ.Levels[p.OccupationLevel].Salary;
        p.Happiness += 8;
        if (p.InFamily)
            ctx.World.Log($"{p.FirstName} was promoted to {JobName(Title(ctx, p))}.", ctx.Importance(false, p), "career", p.Id);
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

    // --- Text ---------------------------------------------------------------------------------

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
        string.Join(' ', title.Split(' ').Select(w => w.Skip(1).Any(char.IsUpper) ? w : w.ToLowerInvariant()));

    public static string EducationName(EducationLevel level) => level switch
    {
        EducationLevel.Primary => "primary school",
        EducationLevel.Secondary => "upper secondary school",
        EducationLevel.University => "university",
        _ => "no education"
    };

    /// <summary>A short description of what the person does ("Nurse", "Studying Law", ...).</summary>
    public static string ActivityText(SimContext ctx, Person p) => p.Activity switch
    {
        Activity.Child => "Child",
        Activity.School => p.Age(ctx.Year) < 16 ? "In school" : "Deciding what to do next",
        Activity.Studying => ctx.Content.Programme(p.ProgrammeId) is { } prog
            ? (prog.Level == EducationLevel.University ? $"Studying {prog.Name}" : prog.Name)
            : p.StudyingFor == EducationLevel.University ? "At university" : "In upper secondary school",
        Activity.Working => Title(ctx, p),
        Activity.Unemployed => "Looking for work",
        Activity.Retired => "Retired",
        _ => ""
    };
}
