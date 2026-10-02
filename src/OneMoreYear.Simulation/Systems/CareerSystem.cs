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
        if (p.Activity == Activity.Prison) return; // see CrimeSystem
        int age = p.Age(ctx.Year);
        bool isPlayer = p.Id == ctx.World.PlayerId;

        if (p.Activity == Activity.Child && age >= ctx.Country.SchoolStartAge)
        {
            p.Activity = Activity.School;
            return;
        }

        if (p.Activity is Activity.School or Activity.Studying) UpdateGrades(ctx, p);

        if (p.Activity == Activity.School && age >= ctx.Country.SecondaryAge)
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

        if (p.Activity == Activity.Unemployed && age >= ctx.Country.SecondaryAge)
        {
            if (isPlayer) { QueueJobOffers(ctx, p); return; }
            double chance = 0.55 + ctx.Mod(p, "career") * 0.3 + (int)p.Education * 0.05;
            if (rng.Chance(chance)) Hire(ctx, p);
            return;
        }

        if (p.Activity == Activity.Working) UpdateJob(ctx, p, extraJobLossChance);
    }

    // --- School and studies -----------------------------------------------------------------

    /// <summary>Where grades are heading, and why: the parts that do not depend on luck.</summary>
    public static IReadOnlyList<(string Label, double Points)> GradeFactors(SimContext ctx, Person p)
    {
        var list = new List<(string, double)>();
        if (p.Effort != 0) list.Add((p.Effort > 0 ? "Studying hard" : "Taking it easy", p.Effort * 8));
        double career = ctx.Mod(p, "career") * 20;
        if (Math.Abs(career) >= 1) list.Add((career > 0 ? "Ambition" : "Lack of drive", career));
        double smarts = (p.Smarts - 50) * 0.7;
        if (Math.Abs(smarts) >= 1) list.Add(("Smarts", smarts));
        if (p.Happiness < 30) list.Add(("Unhappy", -8));
        if (p.Flags.Contains(PartTimeFlag)) list.Add(("Part-time job", -6));
        return list;
    }

    private static void UpdateGrades(SimContext ctx, Person p)
    {
        double target = 50 + GradeFactors(ctx, p).Sum(f => f.Points);
        p.Grades = Math.Clamp(p.Grades * 0.7 + target * 0.3 + ctx.Rng.Gaussian(0, 4), 0, 100);
        EffortToll(ctx, p);
    }

    /// <summary>Working or studying hard costs something; taking it easy gives a little back.</summary>
    private static void EffortToll(SimContext ctx, Person p)
    {
        if (p.Effort > 0)
        {
            p.Happiness = Math.Max(0, p.Happiness - 3);
            p.Health = Math.Max(1, p.Health - 1);
            if (p.Age(ctx.Year) >= 18 && ctx.Rng.Chance(0.04)) AilmentSystem.Begin(ctx, p, "burnout");
        }
        else if (p.Effort < 0)
            p.Happiness = Math.Min(100, p.Happiness + 3);
    }

    /// <summary>Where performance at work is heading, and why: the parts that do not depend on luck.</summary>
    public static IReadOnlyList<(string Label, double Points)> PerformanceFactors(SimContext ctx, Person p)
    {
        var list = new List<(string, double)>();
        if (p.Effort != 0) list.Add((p.Effort > 0 ? "Working hard" : "Taking it easy", p.Effort * 12));
        double career = ctx.Mod(p, "career") * 30;
        if (Math.Abs(career) >= 1) list.Add((career > 0 ? "Ambition" : "Lack of drive", career));
        double smarts = (p.Smarts - 50) * 0.3;
        if (Math.Abs(smarts) >= 1) list.Add(("Smarts", smarts));
        if (p.Health < 40) list.Add(("Poor health", -15));
        return list;
    }

    /// <summary>Can this person get into the programme? University needs a diploma and good enough grades.</summary>
    public static bool CanEnter(SimContext ctx, Person p, ProgrammeDef prog)
    {
        if (p.Degrees.Contains(prog.Id) || p.Age(ctx.Year) < prog.MinAge || ctx.Year < prog.MinYear) return false;
        if (prog.Countries.Count > 0 && !prog.Countries.Contains(ctx.Country.Id)) return false;
        if (prog.Level == EducationLevel.Secondary) return p.Education >= EducationLevel.Primary;
        if (p.Education < EducationLevel.Secondary) return false;
        return p.Grades >= RequiredGrades(ctx, p, prog);
    }

    /// <summary>Why this person cannot start the programme, in words for the player; null if they can.</summary>
    public static string? WhyNot(SimContext ctx, Person p, ProgrammeDef prog)
    {
        if (p.Degrees.Contains(prog.Id)) return "You already have this.";
        if (prog.Countries.Count > 0 && !prog.Countries.Contains(ctx.Country.Id)) return "Not offered in this country.";
        if (ctx.Year < prog.MinYear) return $"Not offered yet. It starts around {prog.MinYear}.";
        if (p.Age(ctx.Year) < prog.MinAge) return $"For adults, from {prog.MinAge}.";
        if (prog.Level == EducationLevel.Secondary) return p.Education >= EducationLevel.Primary ? null : "You need to finish primary school first.";
        if (p.Education < EducationLevel.Secondary)
            return $"You need to finish {ctx.Country.SecondarySchool} first." + (ctx.Country.AdultEducation is { } adult ? $" {adult} can give you one." : "");
        double need = RequiredGrades(ctx, p, prog);
        return p.Grades >= need ? null : $"Needs grades {need:0}, yours are {p.Grades:0}." + (ctx.Country.AdultEducation is { } a ? $" Evening classes at {a} can raise them." : "");
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
        // Student aid at university, and for adults back at school (Komvux, vocational courses).
        p.Income = prog.Level == EducationLevel.University || p.Age(ctx.Year) >= 20 ? ctx.Country.StudentIncome : 0;
    }

    private static void Graduate(SimContext ctx, Person p)
    {
        // A course never lowers what you already have (a vocational course after university).
        if (p.StudyingFor is { } level && level > p.Education) p.Education = level;
        p.StudyingFor = null;
        var prog = ctx.Content.Programme(p.ProgrammeId);
        if (prog != null && !p.Degrees.Contains(prog.Id)) p.Degrees.Add(prog.Id);
        p.ProgrammeId = null;
        p.Flags.Remove(PartTimeFlag);
        if (p.InFamily)
            ctx.World.Log(prog != null
                    ? $"{p.FirstName} graduated from the {prog.NameIn(ctx.Country.Id).ToLowerInvariant()}{(prog.Level == EducationLevel.University ? " programme at university" : "")}."
                    : $"{p.FirstName} graduated from {EducationName(ctx, p.Education)}.",
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

    /// <summary>The median salary in the content (Swedish kronor of 2020); the country's income spread pivots around it.</summary>
    public const double MedianContentSalary = 480_000;

    /// <summary>
    /// A job level's yearly salary in this country's 2020 money. The content is in Swedish kronor; incomeSpread
    /// above 1 stretches the ladder (the USA: low pay lower, top pay much higher).
    /// </summary>
    public static double Salary(SimContext ctx, OccupationLevelDef level) =>
        ctx.Ref(MedianContentSalary * Math.Pow(level.Salary / MedianContentSalary, ctx.Country.IncomeSpread));

    /// <summary>Finds a job for the person. Returns false if nothing fits their education.</summary>
    public static bool Hire(SimContext ctx, Person p, string? occupationId = null, int? level = null, string? employer = null)
    {
        var options = ctx.Content.Occupations
            .Where(o => occupationId == null || o.Id == occupationId)
            .Where(o => o.MinYear <= ctx.Year || occupationId != null)
            .Where(o => EntryLevel(p, o) >= 0 || level != null)
            .ToList();
        if (options.Count == 0) return false;
        var occ = occupationId != null ? options[0] : ctx.Rng.PickWeighted(options, o => OccupationWeight(ctx, p, o))!;
        int lvl = Math.Clamp(level ?? EntryLevel(p, occ), 0, occ.Levels.Count - 1);

        p.Activity = Activity.Working;
        p.OccupationId = occ.Id;
        p.OccupationLevel = lvl;
        p.Employer = employer ?? Employers.Name(ctx, p, occ.Id, lvl);
        p.YearsInJob = 0;
        p.Performance = Math.Clamp(ctx.Rng.Gaussian(50, 10), 20, 80);
        p.Income = Salary(ctx, occ.Levels[lvl]);
        if (p.Id == ctx.World.PlayerId) SocialSystem.OnNewJob(p, ctx.Year);
        if (p.InFamily && p.Age(ctx.Year) >= 16)
            ctx.World.Log($"{p.FirstName} got a job as {Article(Title(ctx, p))}{(p.Employer != null ? $" at {p.Employer}" : "")}.", ctx.Importance(false, p), "career", p.Id);
        return true;
    }

    /// <summary>Job offers for the player to choose between, as "occupationId:level".</summary>
    public static List<string> GenerateOffers(SimContext ctx, Person p)
    {
        var rng = ctx.Rng;
        int max = 1 + (p.Education >= EducationLevel.Secondary ? 1 : 0) + (p.Education == EducationLevel.University ? 1 : 0)
                  + (p.Grades > 70 ? 1 : 0);
        double quality = 0.55 + ctx.Mod(p, "career") * 0.2 + (p.Grades - 50) / 200;
        if (p.CriminalRecord.Count > 0) quality -= 0.25; // employers check
        int count = 0;
        for (int i = 0; i < max; i++) if (rng.Chance(quality)) count++;
        if (count == 0 && rng.Chance(0.5)) count = 1; // something simple usually turns up

        var pool = ctx.Content.Occupations.Where(o => EntryLevel(p, o) >= 0 && o.Id != p.OccupationId && o.MinYear <= ctx.Year).ToList();
        var offers = new List<string>();
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            var occ = rng.PickWeighted(pool, o => OccupationWeight(ctx, p, o))!;
            pool.Remove(occ);
            offers.Add($"{occ.Id}:{EntryLevel(p, occ)}:{Employers.Name(ctx, p, occ.Id, EntryLevel(p, occ), salt: i + 1)}");
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

    /// <summary>"occupationId:level:employer" (the employer is missing in saves from before 0.11).</summary>
    public static (OccupationDef Occ, int Level, string? Employer)? ParseOffer(SimContext ctx, string offer)
    {
        var parts = offer.Split(':', 3);
        if (parts.Length < 2 || ctx.Content.Occupation(parts[0]) is not { } occ || !int.TryParse(parts[1], out var lvl)) return null;
        string? employer = parts.Length == 3 && parts[2].Length > 0 ? parts[2] : null;
        return (occ, Math.Clamp(lvl, 0, occ.Levels.Count - 1), employer);
    }

    private static void UpdateJob(SimContext ctx, Person p, double extraJobLossChance)
    {
        var rng = ctx.Rng;
        var occ = ctx.Content.Occupation(p.OccupationId);
        if (occ == null) { BecomeJobSeeker(p, ctx); return; }

        p.YearsInJob++;
        double target = 50 + PerformanceFactors(ctx, p).Sum(f => f.Points) + rng.Gaussian(0, 15);
        p.Performance = Math.Clamp(p.Performance * 0.7 + target * 0.3, 0, 100);
        EffortToll(ctx, p);

        if (rng.Chance(0.015 * ctx.Mod(p, "dishonesty")))
        {
            if (p.InFamily) ctx.World.Log($"{p.FirstName} was caught stealing at work and fired.", ctx.Importance(false, p), "career", p.Id);
            p.Happiness -= 15;
            BecomeJobSeeker(p, ctx);
            return;
        }
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
        p.Income = Salary(ctx, occ.Levels[p.OccupationLevel]);
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
        p.Income = Salary(ctx, occ.Levels[p.OccupationLevel]);
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

    public static string EducationName(SimContext ctx, EducationLevel level) => level switch
    {
        EducationLevel.Primary => "primary school",
        EducationLevel.Secondary => ctx.Country.SecondarySchool,
        EducationLevel.University => "university",
        _ => "no education"
    };

    /// <summary>A short description of what the person does ("Nurse", "Studying Law", ...).</summary>
    public static string ActivityText(SimContext ctx, Person p) => p.Activity switch
    {
        Activity.Child => "Child",
        Activity.School => p.Age(ctx.Year) < ctx.Country.SecondaryAge ? "In school" : "Deciding what to do next",
        Activity.Studying => ctx.Content.Programme(p.ProgrammeId) is { } prog
            ? (prog.Level == EducationLevel.University ? $"Studying {prog.NameIn(ctx.Country.Id)}" : prog.NameIn(ctx.Country.Id))
            : p.StudyingFor == EducationLevel.University ? "At university" : $"In {ctx.Country.SecondarySchool}",
        Activity.Working => p.Employer != null && p.OccupationId != "crime" ? $"{Title(ctx, p)} at {p.Employer}" : Title(ctx, p),
        Activity.Unemployed => "Looking for work",
        Activity.Retired => "Retired",
        Activity.Prison => $"In prison ({p.PrisonYearsLeft} year{(p.PrisonYearsLeft == 1 ? "" : "s")} left)",
        _ => ""
    };
}
