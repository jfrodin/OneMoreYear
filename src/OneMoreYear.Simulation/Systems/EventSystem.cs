using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Picks events for the player from content templates, fills in the participants and applies
/// the consequences of the player's choices.
/// </summary>
public static class EventSystem
{
    /// <summary>Queues an event that simulation code detected (a "situation").</summary>
    public static PendingEvent? QueueSituation(SimContext ctx, string eventId,
        Dictionary<string, int>? roles = null, Dictionary<string, double>? vars = null)
    {
        var w = ctx.World;
        if (!ctx.Content.Events.TryGetValue(eventId, out var def) || !Allowed(ctx, def)) return null;
        if (w.PendingEvents.Any(e => e.EventId == eventId && !e.Resolved)) return null;
        var pending = new PendingEvent { Uid = w.NextEventUid++, EventId = eventId, Roles = roles ?? new() };
        foreach (var (name, v) in def.Vars) pending.Vars[name] = ComputeVar(ctx, v, pending);
        if (vars != null) foreach (var (k, v) in vars) pending.Vars[k] = v;
        if (def.DynamicChoices == "cities")
            pending.Options = ctx.Country.Cities.Where(c => c.Id != w.Player.CityId).Select(c => c.Id).ToList();
        w.PendingEvents.Add(pending);
        w.EventHistory[eventId] = w.Year;
        return pending;
    }

    /// <summary>False for events about a dark theme the player has turned down (content settings).</summary>
    public static bool Allowed(SimContext ctx, EventDef e) => e.Content.All(ctx.Shown);

    public static void GenerateRandomEvents(SimContext ctx)
    {
        var w = ctx.World;
        var player = w.Player;
        if (!player.IsAlive) return;
        int age = player.Age(ctx.Year);
        double roll = ctx.Rng.NextDouble();
        int count = age < 4 ? (roll < 0.3 ? 1 : 0) : roll < 0.12 ? 0 : roll < 0.72 ? 1 : 2;
        count -= w.PendingEvents.Count(e => !e.Resolved);
        var distances = Kinship.Distances(w, player, 3);

        for (int i = 0; i < count; i++)
        {
            var candidates = ctx.Content.RandomEvents.Where(e => IsEligible(ctx, e, player, distances)).ToList();
            var def = ctx.Rng.PickWeighted(candidates, e => e.Weight);
            if (def == null) return;
            var pending = CreatePending(ctx, def, player, distances);
            if (pending == null) continue;
            w.PendingEvents.Add(pending);
            w.EventHistory[def.Id] = w.Year;
        }
    }

    private static bool IsEligible(SimContext ctx, EventDef e, Person player, Dictionary<int, int> distances)
    {
        var w = ctx.World;
        if (!Allowed(ctx, e) || w.PendingEvents.Any(p => p.EventId == e.Id)) return false;
        if ((player.Activity == Activity.Prison) != (e.Category == "prison")) return false;
        if (w.EventHistory.TryGetValue(e.Id, out var last))
        {
            if (e.Cooldown == 0) return false;
            if (w.Year - last < e.Cooldown) return false;
        }
        if (!Matches(ctx, e.Conditions, player, player)) return false;
        if (e.Target != null && e.Target.Role != "new_person" && !Candidates(ctx, e.Target, player, distances, e).Any()) return false;
        return true;
    }

    public static IEnumerable<Person> Candidates(SimContext ctx, RoleDef role, Person player, Dictionary<int, int> distances,
        EventDef? oncePer = null)
    {
        var w = ctx.World;
        return Kinship.Circle(w, player)
            .Where(p => Kinship.MatchesRole(w, player, p, role.Role, distances))
            .Where(p => Matches(ctx, role.Conditions, p, player))
            .Where(p => oncePer is not { OncePerTarget: true } || !w.EventHistory.ContainsKey(OnceKey(oncePer.Id, p.Id)));
    }

    private static string OnceKey(string eventId, int targetId) => $"{eventId}#{targetId}";

    public static PendingEvent? CreatePending(SimContext ctx, EventDef def, Person player, Dictionary<int, int> distances,
        int? fixedTarget = null)
    {
        var w = ctx.World;
        var pending = new PendingEvent { Uid = w.NextEventUid++, EventId = def.Id };
        if (def.Target != null)
        {
            var target = fixedTarget is { } ft ? w.Get(ft) : ResolveRole(ctx, def.Target, player, distances, null, def);
            if (target == null) return null;
            pending.Roles["target"] = target.Id;
            if (def.OncePerTarget) w.EventHistory[OnceKey(def.Id, target.Id)] = w.Year;
        }
        if (def.Other != null)
        {
            var other = ResolveRole(ctx, def.Other, player, distances, w.TryGet(pending.Roles.GetValueOrDefault("target")));
            if (other == null) return null;
            pending.Roles["other"] = other.Id;
        }
        foreach (var (name, v) in def.Vars) pending.Vars[name] = ComputeVar(ctx, v, pending);
        return pending;
    }

    private static Person? ResolveRole(SimContext ctx, RoleDef role, Person player, Dictionary<int, int> distances, Person? target,
        EventDef? oncePer = null)
    {
        if (role.Role == "new_person")
        {
            var sex = role.Sex switch
            {
                "same" => player.Sex,
                "opposite" => player.Sex == Sex.Male ? Sex.Female : Sex.Male,
                "attracted" => player.AttractedToSameSex ? player.Sex : (player.Sex == Sex.Male ? Sex.Female : Sex.Male),
                _ => ctx.Rng.Chance(0.5) ? Sex.Male : Sex.Female
            };
            int age = Math.Max(1, player.Age(ctx.Year) + ctx.Rng.Range(role.AgeOffsetMin, role.AgeOffsetMax));
            if (role.Sex == "attracted") age = RomanticAge(ctx, player, age);
            var p = PersonFactory.CreateStranger(ctx, sex, age);
            if (role.Sex == "attracted") p.AttractedToSameSex = player.AttractedToSameSex;
            return p;
        }
        if (role.Role == "partner_of_target")
            return target?.PartnerId is { } pid && ctx.World.Get(pid) is { IsAlive: true } partner && partner.Id != player.Id ? partner : null;

        var list = Candidates(ctx, role, player, distances, oncePer).ToList();
        return list.Count == 0 ? null : ctx.Rng.Pick(list);
    }

    public static double ComputeVar(SimContext ctx, VarDef v, PendingEvent pending)
    {
        var who = v.IncomeOf switch
        {
            "target" => ctx.World.TryGet(pending.Roles.GetValueOrDefault("target")),
            "other" => ctx.World.TryGet(pending.Roles.GetValueOrDefault("other")),
            _ => ctx.World.Player
        };
        double income = who?.Income ?? 0;
        double value = (v.Base + income * v.IncomeFactor) * ctx.Rng.Range(v.RandomMin, v.RandomMax);
        return Math.Round(value / 1000) * 1000;
    }

    // --- Conditions -------------------------------------------------------------------------

    public static bool Matches(SimContext ctx, ConditionDef? c, Person p, Person player)
    {
        if (c == null) return true;
        var w = ctx.World;
        int age = p.Age(ctx.Year);
        if (c.MinAge is { } minAge && age < minAge) return false;
        if (c.MaxAge is { } maxAge && age > maxAge) return false;
        if (c.Sex is { } sex && p.Sex != sex) return false;
        if (c.HasPartner is { } hp && (p.PartnerId != null) != hp) return false;
        if (c.Married is { } m && (p.PartnerStatus == PartnerStatus.Married) != m) return false;
        if (c.PartnerStatus is { } ps && p.PartnerStatus != ps) return false;
        if (c.SameSexAsPlayer is { } ss && (p.Sex == player.Sex) != ss) return false;
        if (c.CompatibleWithPlayer is { } cwp && Compatible(ctx, p, player) != cwp) return false;
        if (c.HasJob is { } hj && (p.Activity == Activity.Working) != hj) return false;
        if (c.Activity is { } act && p.Activity != act) return false;
        if (c.NotActivity is { } notAct && notAct.Contains(p.Activity)) return false;
        if (c.MinEducation is { } minEd && p.Education < minEd) return false;
        if (c.MaxEducation is { } maxEd && p.Education > maxEd) return false;
        if (c.MinGrades is { } minGr && p.Grades < minGr) return false;
        if (c.MaxGrades is { } maxGr && p.Grades > maxGr) return false;
        if (c.MinYear is { } minY && ctx.Year < minY) return false;
        if (c.MaxYear is { } maxY && ctx.Year > maxY) return false;
        if (c.MinPartnerYears is { } minPy && (p.PartnerId == null || ctx.Year - p.PartnerSinceYear < minPy)) return false;
        if (c.AilmentsAny is { Count: > 0 } ail && !ail.Any(p.Ailments.ContainsKey)) return false;
        if (c.NotAilments is { } notAil && notAil.Any(p.Ailments.ContainsKey)) return false;
        if (c.Addicted is { } addicted && (p.Addiction != null) != addicted) return false;
        if (c.JobTags is { Count: > 0 } tags && ctx.Content.Occupation(p.OccupationId)?.Tags.Any(tags.Contains) != true) return false;
        int kids = p.ChildIds.Count(id => w.Get(id).IsAlive);
        if (c.MinChildren is { } minK && kids < minK) return false;
        if (c.MaxChildren is { } maxK && kids > maxK) return false;
        if (c.MinMoney is { } minM && p.Money < ctx.Nominal(minM)) return false;
        if (c.MaxMoney is { } maxM && p.Money > ctx.Nominal(maxM)) return false;
        if (c.MinHealth is { } minH && p.Health < minH) return false;
        if (c.MaxHealth is { } maxH && p.Health > maxH) return false;
        if (c.OwnsHome is { } oh && p.OwnsHome != oh) return false;
        if (c.CanBuyHome is { } cb && EconomySystem.CanBuyHome(ctx, p) != cb) return false;
        if (c.HasInvestments is { } hi && (p.Funds + p.Stocks >= 1) != hi) return false;
        if (c.HasMortgage is { } hm && (p.Mortgage >= 1) != hm) return false;
        if (c.HoldsHome is { } hh && (p.HomeValue > 0) != hh) return false;
        if (c.LivesWithParents is { } lwp && p.LivesWithParents != lwp) return false;
        if (c.TraitsAny is { Count: > 0 } any && !any.Any(p.HasTrait)) return false;
        if (c.TraitsNone is { Count: > 0 } none && none.Any(p.HasTrait)) return false;
        if (c.Flags is { } flags && !flags.All(p.Flags.Contains)) return false;
        if (c.NotFlags is { } notFlags && notFlags.Any(p.Flags.Contains)) return false;
        if (p.Id != player.Id)
        {
            var rel = w.FindRel(p.Id, player.Id);
            double opinion = rel?.Opinion ?? -10;
            if (c.MinOpinion is { } minO && opinion < minO) return false;
            if (c.MaxOpinion is { } maxO && opinion > maxO) return false;
            if (c.RelMin != null && c.RelMin.Any(kv => (rel?[kv.Key] ?? 0) < kv.Value)) return false;
            if (c.RelMax != null && c.RelMax.Any(kv => (rel?[kv.Key] ?? 0) > kv.Value)) return false;
        }
        if (c.HasRole is { Count: > 0 } roles)
        {
            var circle = Kinship.Circle(w, p);
            var dist = Kinship.Distances(w, p, 3);
            foreach (var role in roles)
                if (!circle.Any(x => Kinship.MatchesRole(w, p, x, role, dist))) return false;
        }
        return true;
    }

    /// <summary>Keeps a generated love interest within the ages <see cref="Compatible"/> allows.</summary>
    /// <summary>Like <see cref="RomanticAge(SimContext,int,int)"/>, but adults who like much younger or older partners get them.</summary>
    public static int RomanticAge(SimContext ctx, Person p, int wanted)
    {
        int age = p.Age(ctx.Year);
        if (age >= ctx.Country.AdultAge + 8 && ctx.Mod(p, "prefers_younger") > 0) wanted = age - ctx.Rng.Range(8, 25);
        else if (age >= ctx.Country.AdultAge && ctx.Mod(p, "prefers_older") > 0) wanted = age + ctx.Rng.Range(8, 25);
        return RomanticAge(ctx, age, wanted);
    }

    public static int RomanticAge(SimContext ctx, int playerAge, int wanted)
    {
        var c = ctx.Country;
        if (playerAge < c.AgeOfConsent)
            return Math.Clamp(wanted, Math.Max(12, playerAge - 2), Math.Min(c.AgeOfConsent - 1, playerAge + 2));
        if (playerAge < c.AdultAge)
            return Math.Clamp(wanted, Math.Max(c.AgeOfConsent, playerAge - 3), playerAge + 3);
        return Math.Max(wanted, c.AdultAge);
    }

    /// <summary>
    /// Could these two become a couple? Follows the country's law: real relationships from the age of
    /// consent (with a small age gap while one of them is under age), innocent "going steady" between
    /// kids of almost the same age below it, never between an adult and a child. Orientation must
    /// match and they must not be close family.
    /// </summary>
    public static bool Compatible(SimContext ctx, Person a, Person b)
    {
        if (a.Id == b.Id || !a.IsAlive || !b.IsAlive) return false;
        var c = ctx.Country;
        int ageA = a.Age(ctx.Year), ageB = b.Age(ctx.Year);
        int gap = Math.Abs(ageA - ageB);
        bool bothConsent = ageA >= c.AgeOfConsent && ageB >= c.AgeOfConsent;
        bool bothYoung = ageA is >= 12 && ageB >= 12 && ageA < c.AgeOfConsent && ageB < c.AgeOfConsent;
        if (bothConsent)
        {
            if ((ageA < c.AdultAge || ageB < c.AdultAge) && gap > 3) return false;
        }
        else if (!bothYoung || gap > 2) return false;

        bool aLikes = a.AttractedToSameSex ? a.Sex == b.Sex : a.Sex != b.Sex;
        bool bLikes = b.AttractedToSameSex ? b.Sex == a.Sex : b.Sex != a.Sex;
        if (!aLikes || !bLikes) return false;
        // Family: close relatives never, cousins only where the law allows, step-siblings not if
        // they grew up together (see docs/design-decisions.md).
        return Kinship.Blood(ctx.World, a, b) switch
        {
            BloodTie.Close => false,
            BloodTie.FirstCousins => ctx.Country.CousinMarriageAllowed,
            _ => !Kinship.GrewUpAsStepSiblings(ctx.World, a, b)
        };
    }

    // --- Choices ------------------------------------------------------------------------------

    public static bool IsChoiceAvailable(SimContext ctx, ChoiceDef choice, PendingEvent pending)
    {
        var player = ctx.World.Player;
        if (Meets(ctx, choice, player)) return true;
        // Never leave the player stuck: if nothing is available (the situation changed since the
        // event was created), every choice becomes available.
        var def = ctx.Content.Events[pending.EventId];
        return pending.Options.Count == 0 && !def.Choices.Any(c => Meets(ctx, c, player));
    }

    private static bool Meets(SimContext ctx, ChoiceDef choice, Person player)
    {
        if (!Matches(ctx, choice.Requires, player, player)) return false;
        // Choosing a programme also needs admission (e.g. grades for Medicine).
        return StudyProgramme(ctx, choice) is not { } prog || CareerSystem.CanEnter(ctx, player, prog);
    }

    /// <summary>The programme a choice enrols you in, if any – used to check admission requirements.</summary>
    public static ProgrammeDef? StudyProgramme(SimContext ctx, ChoiceDef choice) =>
        choice.Effects.FirstOrDefault(e => e.Type == "study" && e.Programme != null) is { } eff ? ctx.Content.Programme(eff.Programme) : null;

    public static double SuccessChance(SimContext ctx, ChoiceDef choice, PendingEvent pending)
    {
        if (choice.Chance is not { } chance) return 1;
        var player = ctx.World.Player;
        foreach (var (trait, bonus) in choice.ChanceTraits)
            if (player.HasTrait(trait)) chance += bonus;
        if (choice.ChanceOpinion != 0 && pending.Roles.TryGetValue("target", out var tid))
            chance += ctx.World.Opinion(tid, player.Id) * choice.ChanceOpinion;
        if (choice.ChanceRelation.Count > 0 && pending.Roles.TryGetValue("target", out var relTarget))
        {
            var rel = ctx.World.FindRel(relTarget, player.Id);
            foreach (var (dim, perPoint) in choice.ChanceRelation) chance += ((rel?[dim] ?? 0) - 40) * perPoint;
        }
        foreach (var (attr, perPoint) in choice.ChanceAttributes)
        {
            double value = attr switch
            {
                "smarts" => player.Smarts,
                "looks" => player.Looks,
                "fitness" => player.Fitness,
                "grades" => player.Grades,
                _ => 50
            };
            chance += (value - 50) * perPoint;
        }
        return Math.Clamp(chance, 0.03, 0.97);
    }

    /// <summary>Applies the chosen option. Returns the outcome text shown to the player.</summary>
    public static string Resolve(SimContext ctx, PendingEvent pending, int choiceIndex)
    {
        var def = ctx.Content.Events[pending.EventId];
        var texts = new List<string>();

        // Generated choices (job offers) come first, then the event's own choices.
        if (choiceIndex < pending.Options.Count)
        {
            var player = ctx.World.Player;
            if (def.DynamicChoices == "job_offers" && CareerSystem.ParseOffer(ctx, pending.Options[choiceIndex]) is { } offer)
            {
                var (occ, level, employer) = offer;
                CareerSystem.Hire(ctx, player, occ.Id, level, employer);
                texts.Add($"You accept. You start as {CareerSystem.Article(occ.Levels[level].Title)}{(employer != null ? $" at {employer}" : "")}.");
            }
            else if (def.DynamicChoices == "cities")
            {
                HousingSystem.MoveTo(ctx, player, pending.Options[choiceIndex]);
                texts.Add($"You pack everything and move to {HousingSystem.City(ctx, player).Name}.");
            }
            else if (def.DynamicChoices == "baby_names" && ctx.World.TryGet(pending.Roles.GetValueOrDefault("target")) is { } baby)
            {
                FamilySystem.Rename(ctx, baby, pending.Options[choiceIndex]);
                texts.Add($"Welcome to the world, {baby.FirstName}.");
            }
            pending.Resolved = true;
            pending.ChosenIndex = choiceIndex;
            pending.OutcomeText = string.Join(" ", texts);
            return pending.OutcomeText;
        }
        var choice = def.Choices[choiceIndex - pending.Options.Count];

        foreach (var eff in choice.Effects) EffectApplier.Apply(ctx, eff, pending);
        if (!string.IsNullOrWhiteSpace(choice.Result)) texts.Add(TextFormatter.Format(ctx, choice.Result, pending));

        if (choice.Chance != null)
        {
            bool success = ctx.Rng.Chance(SuccessChance(ctx, choice, pending));
            var outcome = success ? choice.Success : choice.Failure;
            if (outcome != null)
            {
                foreach (var eff in outcome.Effects) EffectApplier.Apply(ctx, eff, pending);
                if (!string.IsNullOrWhiteSpace(outcome.Text)) texts.Add(TextFormatter.Format(ctx, outcome.Text, pending));
            }
        }

        texts.AddRange(pending.ExtraText.Where(t => !string.IsNullOrWhiteSpace(t)));

        if (pending.Vars.TryGetValue("became_affair", out var partnerId) && ctx.World.TryGet((int)partnerId) is { } partner)
            texts.Add($"But you're still with {partner.FirstName} – this is an affair now. Nobody can find out.");

        pending.Resolved = true;
        pending.ChosenIndex = choiceIndex;
        pending.OutcomeText = string.Join(" ", texts);
        return pending.OutcomeText;
    }

    /// <summary>Whether a choice would start a relationship (used to warn that it would be an affair).</summary>
    public static bool StartsRomance(ChoiceDef c) =>
        c.Effects.Concat(c.Success?.Effects ?? new()).Any(e => e.Type == "start_dating");
}
