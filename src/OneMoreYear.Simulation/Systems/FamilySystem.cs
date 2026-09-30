using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>Dating, moving in, marriage, children, break-ups and affairs.</summary>
public static class FamilySystem
{
    public static void Update(SimContext ctx)
    {
        var w = ctx.World;
        DeliverExpected(ctx);
        int count = w.People.Count; // people born this year are not processed until next year
        for (int i = 0; i < count; i++)
        {
            var p = w.People[i];
            if (!p.IsAlive || !p.InFamily) continue;
            int age = p.Age(ctx.Year);
            if (age < 16) continue;

            if (p.PartnerId is { } pid)
            {
                var partner = w.Get(pid);
                if (p.Id < pid || !partner.InFamily) UpdateCouple(ctx, p, partner);
                if (p.Id != w.PlayerId && p.PartnerId != null) MaybeStartAffair(ctx, p);
            }
            else if (p.Id != w.PlayerId && age >= 18 && age <= 75)
            {
                SeekPartner(ctx, p);
            }
        }
    }

    private static void UpdateCouple(SimContext ctx, Person a, Person b)
    {
        var w = ctx.World;
        var rng = ctx.Rng;
        int years = ctx.Year - a.PartnerSinceYear;
        double oa = w.Opinion(a.Id, b.Id), ob = w.Opinion(b.Id, a.Id);
        bool playerCouple = a.Id == w.PlayerId || b.Id == w.PlayerId;

        // --- Break-up ---
        double baseChance = a.PartnerStatus switch
        {
            PartnerStatus.Dating => 0.14,
            PartnerStatus.Cohabiting => 0.05,
            _ => 0.022 * ctx.DivorceIndex
        };
        if (playerCouple)
        {
            var npc = a.Id == w.PlayerId ? b : a;
            var player = a.Id == w.PlayerId ? a : b;
            double npcOpinion = w.Opinion(npc.Id, player.Id);
            double leave = npcOpinion < 20 ? baseChance + (20 - npcOpinion) / 150 + ctx.Mod(npc, "divorce") * 0.04 : 0;
            if (years >= 1 && rng.Chance(leave))
            {
                EventSystem.QueueSituation(ctx, "partner_leaves", new() { ["target"] = npc.Id });
                return;
            }
        }
        else
        {
            double low = Math.Min(oa, ob);
            double chance = baseChance + (low < 15 ? 0.08 : 0) + (low < -10 ? 0.25 : 0)
                            + (ctx.Mod(a, "divorce") + ctx.Mod(b, "divorce")) * 0.03;
            if (years >= 1 && rng.Chance(chance))
            {
                var initiator = oa < ob ? a : b;
                BreakUp(ctx, initiator, initiator == a ? b : a);
                return;
            }
        }

        // --- Moving in / marriage ---
        if (playerCouple)
        {
            var npc = a.Id == w.PlayerId ? b : a;
            if (a.PartnerStatus == PartnerStatus.Cohabiting && years >= 2 && w.Opinion(npc.Id, w.PlayerId) > 40
                && a.Age(ctx.Year) >= ctx.Country.MarriageAge && b.Age(ctx.Year) >= ctx.Country.MarriageAge
                && rng.Chance(0.15))
                EventSystem.QueueSituation(ctx, "partner_proposes", new() { ["target"] = npc.Id });
        }
        else if (a.PartnerStatus == PartnerStatus.Dating && years >= 1 && oa > 15 && ob > 15 && rng.Chance(0.4)
                 && a.Age(ctx.Year) >= ctx.Country.AdultAge && b.Age(ctx.Year) >= ctx.Country.AdultAge)
            MoveIn(ctx, a, b);
        else if (a.PartnerStatus == PartnerStatus.Cohabiting && years >= 1 && oa > 25 && ob > 25
                 && rng.Chance(0.14 * (ctx.Year < 1970 ? 2 : 1)))
            Marry(ctx, a, b);

        // --- Children ---
        TryHaveChildren(ctx, a, b, playerCouple);
    }

    private static void TryHaveChildren(SimContext ctx, Person a, Person b, bool playerCouple)
    {
        var mother = a.Sex == Sex.Female ? a : b.Sex == Sex.Female ? b : null;
        int kids = a.ChildIds.Intersect(b.ChildIds).Count();
        double statusFactor = a.PartnerStatus switch
        {
            PartnerStatus.Dating => 0.08,
            PartnerStatus.Cohabiting => 0.8,
            _ => 1.0
        };

        if (a.Sex == b.Sex)
        {
            // Same-sex couples adopt now and then.
            int age = Math.Min(a.Age(ctx.Year), b.Age(ctx.Year));
            if (!playerCouple && a.PartnerStatus != PartnerStatus.Dating && age is >= 28 and <= 45 && kids < 2 && ctx.Rng.Chance(0.05))
            {
                var child = PersonFactory.CreateBaby(ctx, a, b);
                child.IsAdopted = true;
                ctx.World.Log($"{a.FirstName} and {b.FirstName} adopted {child.FirstName}.", ctx.Importance(true, a, b), "family", a.Id, b.Id, child.Id);
            }
            return;
        }

        if (mother == null) return;
        double fertility = FertilityByAge(ctx, mother.Age(ctx.Year));
        if (fertility <= 0) return;
        double desire = kids switch { 0 => 0.22, 1 => 0.26, 2 => 0.12, 3 => 0.05, _ => 0.02 };
        double chance = desire * fertility * statusFactor * ctx.FertilityIndex;
        if (playerCouple)
        {
            // The player decides; this is an "oops" – and it arrives next year like any pregnancy.
            var player = a.Id == ctx.World.PlayerId ? a : b;
            if (player.Expecting == null && ctx.Rng.Chance(0.02 * fertility * statusFactor)) Expect(ctx, player, player == a ? b : a);
            return;
        }
        if (ctx.Rng.Chance(chance)) HaveChild(ctx, a, b);
    }

    /// <summary>A baby on the way (or an adoption being processed) – it arrives next year.</summary>
    public static void Expect(SimContext ctx, Person parent, Person? other)
    {
        if (parent.Expecting != null) return;
        bool adoption = other != null && other.Sex == parent.Sex;
        parent.Expecting = new ExpectedChild { ParentAId = parent.Id, ParentBId = other?.Id, DueYear = ctx.Year + 1, Adoption = adoption };
        parent.Flags.Add("expecting");
        string who = other == null ? parent.FirstName : $"{parent.FirstName} and {other.FirstName}";
        ctx.World.Log(adoption ? $"{who} were approved to adopt a child." : $"{who} are expecting a baby.",
            ctx.Importance(false, parent, other), "family", parent.Id, other?.Id ?? parent.Id);
    }

    /// <summary>Babies (and adopted children) that were expected last year arrive.</summary>
    private static void DeliverExpected(SimContext ctx)
    {
        var w = ctx.World;
        foreach (var p in w.People.Where(p => p.Expecting is { } e && e.DueYear <= ctx.Year).ToList())
        {
            var e = p.Expecting!;
            p.Expecting = null;
            p.Flags.Remove("expecting");
            if (!p.IsAlive) continue;
            var other = w.TryGet(e.ParentBId);
            var child = HaveChild(ctx, p, other);
            child.IsAdopted = e.Adoption;
            if (p.Id == w.PlayerId || other?.Id == w.PlayerId)
            {
                var pending = EventSystem.QueueSituation(ctx, "name_baby", new() { ["target"] = child.Id });
                if (pending != null) pending.Options = BabyNames(ctx, child);
            }
        }
    }

    /// <summary>Name suggestions for the player's baby: the one it was given plus three more.</summary>
    public static List<string> BabyNames(SimContext ctx, Person child)
    {
        var names = new List<string> { child.FirstName };
        for (int i = 0; i < 20 && names.Count < 4; i++)
        {
            var n = PersonFactory.RandomFirstName(ctx, child.Sex);
            if (!names.Contains(n)) names.Add(n);
        }
        return names;
    }

    /// <summary>Gives a child a new first name and updates what has been written about them.</summary>
    public static void Rename(SimContext ctx, Person child, string name)
    {
        string old = child.FirstName;
        if (old == name) return;
        child.FirstName = name;
        var pattern = new System.Text.RegularExpressions.Regex($@"\b{System.Text.RegularExpressions.Regex.Escape(old)}\b");
        foreach (var entry in ctx.World.Chronicle.Where(l => l.PersonIds.Contains(child.Id)))
            entry.Text = pattern.Replace(entry.Text, name);
        foreach (var parent in child.ParentIds.Select(ctx.World.Get))
            foreach (var m in parent.Memories.Where(m => m.AboutId == child.Id || m.MentionId == child.Id))
                m.Text = pattern.Replace(m.Text, name);
    }

    /// <summary>
    /// Chance factor for pregnancy in a normal relationship. Starts at the age of consent and is low
    /// in the teens; it is possible from about 12 biologically, but below the age of consent it only
    /// belongs to the abuse storylines, never to normal life.
    /// </summary>
    public static double FertilityByAge(SimContext ctx, int age) => age < ctx.Country.AgeOfConsent ? 0 : age switch
    {
        < 18 => 0.12,
        < 25 => 0.8,
        < 35 => 1.0,
        < 40 => 0.6,
        < 45 => 0.25,
        _ => 0
    };

    public static Person HaveChild(SimContext ctx, Person a, Person? b, int? biologicalFatherId = null)
    {
        var child = PersonFactory.CreateBaby(ctx, a, b, biologicalFatherId);
        var w = ctx.World;
        string parents = b == null ? a.FirstName : $"{a.FirstName} and {b.FirstName}";
        w.Log($"{parents} had a {(child.Sex == Sex.Male ? "son" : "daughter")}, {child.FirstName}.", ctx.Importance(true, a, b, child), "family", a.Id, b?.Id ?? a.Id, child.Id);
        RelationshipSystem.AddMemory(ctx, a, "child_born", $"{child.FirstName} was born", 30, child.Id);
        if (b != null) RelationshipSystem.AddMemory(ctx, b, "child_born", $"{child.FirstName} was born", 30, child.Id);
        return child;
    }

    private static void SeekPartner(SimContext ctx, Person p)
    {
        int age = p.Age(ctx.Year);
        double baseChance = age switch { < 26 => 0.2, < 36 => 0.22, < 51 => 0.12, _ => 0.05 };
        baseChance *= 1 + ctx.Mod(p, "social");
        baseChance *= 0.7 + p.Looks / 100 * 0.6;
        baseChance *= 1 + ctx.Mod(p, "charm") * 0.3;
        if (p.Flags.Contains("widowed") || p.ExPartnerIds.Count > 0) baseChance *= 0.7;
        if (!ctx.Rng.Chance(baseChance)) return;
        var partner = CreatePartnerFor(ctx, p);
        StartDating(ctx, p, partner);
    }

    public static Person CreatePartnerFor(SimContext ctx, Person p, int ageOffsetMin = -4, int ageOffsetMax = 4)
    {
        var sex = p.AttractedToSameSex ? p.Sex : (p.Sex == Sex.Male ? Sex.Female : Sex.Male);
        int age = Math.Max(18, p.Age(ctx.Year) + ctx.Rng.Range(ageOffsetMin, ageOffsetMax) + (p.Sex == Sex.Male ? -1 : 1));
        // Some adults go for much younger or much older partners (always adults).
        age = Math.Max(ctx.Country.AdultAge, EventSystem.RomanticAge(ctx, p, age));
        var partner = PersonFactory.CreateStranger(ctx, sex, age);
        partner.CityId = p.CityId;
        partner.LivesWithParents = false;
        partner.AttractedToSameSex = p.AttractedToSameSex;
        partner.Generation = p.Generation;
        return partner;
    }

    public static void StartDating(SimContext ctx, Person a, Person b)
    {
        foreach (var (x, y) in new[] { (a, b), (b, a) })
        {
            x.PartnerId = y.Id;
            x.PartnerStatus = PartnerStatus.Dating;
            x.PartnerSinceYear = ctx.Year;
            x.FriendIds.Remove(y.Id);
            var r = ctx.World.Rel(x.Id, y.Id);
            r.Closeness = Math.Max(r.Closeness, 60 + ctx.Rng.Gaussian(0, 8));
            r.Attraction = Math.Max(r.Attraction, 75 + ctx.Rng.Gaussian(0, 8));
            r.Trust = Math.Max(r.Trust, 55);
            r.LastContactYear = ctx.Year;
        }
        if (a.InFamily || b.InFamily)
        {
            a.InFamily = b.InFamily = true;
            if (!b.IsBlood) b.Generation = a.Generation;
            if (!a.IsBlood) a.Generation = b.Generation;
        }
        ctx.World.Log($"{a.FirstName} started dating {b.FullName}.", ctx.Importance(false, a, b), "love", a.Id, b.Id);
        FamilyReacts(ctx, a, b);
    }

    /// <summary>
    /// Some couples make the family talk: cousins, and adults with a big age gap ("half your age plus
    /// seven"). The younger one's parents take it worst.
    /// </summary>
    private static void FamilyReacts(SimContext ctx, Person a, Person b)
    {
        var w = ctx.World;
        if (Kinship.AreCousins(w, a, b))
        {
            w.Log($"The family is whispering: {a.FirstName} and {b.FirstName} are cousins.", ctx.Importance(true, a, b), "love", a.Id, b.Id);
            foreach (var parent in Kinship.Parents(w, a).Concat(Kinship.Parents(w, b)).Where(x => x.IsAlive).Distinct())
                foreach (var child in new[] { a, b }.Where(c => c.ParentIds.Contains(parent.Id)))
                    RelationshipSystem.AddMemory(ctx, parent, "cousin_couple",
                        $"{child.FirstName} got together with a cousin", -12, child.Id);
            return;
        }

        var (older, younger) = a.BirthYear <= b.BirthYear ? (a, b) : (b, a);
        int oldAge = older.Age(ctx.Year), youngAge = younger.Age(ctx.Year);
        if (youngAge < ctx.Country.AdultAge || youngAge >= oldAge / 2 + 7) return;

        w.Log($"The age gap between {older.FirstName} ({oldAge}) and {younger.FirstName} ({youngAge}) sets tongues wagging.",
            ctx.Importance(true, a, b), "love", older.Id, younger.Id);
        foreach (var parent in Kinship.Parents(w, younger).Where(x => x.IsAlive && x.Id != older.Id))
        {
            RelationshipSystem.AddMemory(ctx, parent, "age_gap",
                $"{older.FirstName} is far too old for {younger.FirstName}", -30, older.Id);
            RelationshipSystem.Change(ctx, parent.Id, younger.Id, RelDim.Trust, -10);
        }
        if (Kinship.Children(w, older).FirstOrDefault(c => c.IsAlive && Math.Abs(c.Age(ctx.Year) - youngAge) <= 5) is { } sameAgeChild)
            RelationshipSystem.AddMemory(ctx, sameAgeChild, "age_gap_parent",
                $"{older.FirstName} is dating someone my own age", -20, older.Id);
    }

    public static void MoveIn(SimContext ctx, Person a, Person b)
    {
        if (!OldEnoughToMoveIn(ctx, a) || !OldEnoughToMoveIn(ctx, b)) return;
        a.PartnerStatus = b.PartnerStatus = PartnerStatus.Cohabiting;
        HousingSystem.MoveInTogether(ctx, a, b);
        ctx.World.Log($"{a.FirstName} and {b.FirstName} moved in together.", ctx.Importance(false, a, b), "love", a.Id, b.Id);
        EconomySystem.MergeHomes(ctx, a, b);
    }

    public static void Marry(SimContext ctx, Person a, Person b)
    {
        if (a.Age(ctx.Year) < ctx.Country.MarriageAge || b.Age(ctx.Year) < ctx.Country.MarriageAge) return;
        a.PartnerStatus = b.PartnerStatus = PartnerStatus.Married;
        a.Flags.Add($"married_to_{b.Id}");
        b.Flags.Add($"married_to_{a.Id}");
        if (a.Sex != b.Sex && ctx.Rng.Chance(ctx.Country.WifeTakesNameChance))
        {
            var (wife, husband) = a.Sex == Sex.Female ? (a, b) : (b, a);
            wife.LastName = husband.LastName;
        }
        RelationshipSystem.AddMemory(ctx, a, "wedding", $"Married {b.FirstName}", 30, b.Id);
        RelationshipSystem.AddMemory(ctx, b, "wedding", $"Married {a.FirstName}", 30, a.Id);
        ctx.World.Log($"{a.FirstName} and {b.FirstName} got married.", ctx.Importance(true, a, b), "love", a.Id, b.Id);
    }

    /// <summary>Adults decide for themselves; from CohabitWithConsentAge it needs the parents' consent (handled by the event).</summary>
    public static bool OldEnoughToMoveIn(SimContext ctx, Person p) => p.Age(ctx.Year) >= ctx.Country.CohabitWithConsentAge;

    public static void BreakUp(SimContext ctx, Person initiator, Person other)
    {
        var w = ctx.World;
        bool married = initiator.PartnerStatus == PartnerStatus.Married;
        bool longTerm = initiator.PartnerStatus != PartnerStatus.Dating;
        foreach (var (x, y) in new[] { (initiator, other), (other, initiator) })
        {
            x.PartnerId = null;
            x.PartnerStatus = PartnerStatus.None;
            if (!x.ExPartnerIds.Contains(y.Id)) x.ExPartnerIds.Add(y.Id);
            w.Rel(x.Id, y.Id)[RelDim.Closeness] -= 30;
            x.LastSplitYear = ctx.Year;
            x.LastSplitWithId = y.Id;
            x.LastSplitByThem = x == other;
        }

        RelationshipSystem.AddMemory(ctx, other, married ? "divorced" : "dumped",
            married ? $"{initiator.FirstName} wanted a divorce" : $"{initiator.FirstName} broke up with me", longTerm ? -45 : -20, initiator.Id);
        RelationshipSystem.AddMemory(ctx, initiator, "breakup", $"Left {other.FirstName}", -10, other.Id);

        var sharedKids = initiator.ChildIds.Intersect(other.ChildIds).Select(w.Get).Where(k => k.IsAlive).ToList();
        foreach (var kid in sharedKids)
        {
            int age = kid.Age(ctx.Year);
            if (age >= 25) continue;
            RelationshipSystem.AddMemory(ctx, kid, "parents_split",
                $"My parents split up when I was {age}", age < 18 ? -30 : -12, initiator.Id);
        }

        foreach (var (x, y) in new[] { (initiator, other), (other, initiator) })
            if (!x.IsBlood && x.ChildIds.Count(c => y.ChildIds.Contains(c)) == 0) x.InFamily = false;

        string text = married
            ? $"{initiator.FirstName} and {other.FirstName} divorced. It was {initiator.FirstName} who wanted out."
            : $"{initiator.FirstName} broke up with {other.FirstName}.";
        w.Log(text, ctx.Importance(married, initiator, other), "love", initiator.Id, other.Id);
    }

    // --- Affairs ------------------------------------------------------------------------------

    private static void MaybeStartAffair(SimContext ctx, Person p)
    {
        var w = ctx.World;
        int age = p.Age(ctx.Year);
        if (age < 20 || age > 65 || p.PartnerId is not { } pid) return;
        if (w.Secrets.Any(s => s.Kind == "affair" && s.Active && s.SubjectId == p.Id)) return;

        double chance = 0.004 + ctx.Mod(p, "infidelity") * 0.03 + (w.Opinion(p.Id, pid) < 15 ? 0.02 : 0);
        if (!ctx.Rng.Chance(chance)) return;

        var partner = w.Get(pid);
        Person? lover = null;
        var sex = p.AttractedToSameSex ? p.Sex : (p.Sex == Sex.Male ? Sex.Female : Sex.Male);
        if (partner.Id == w.PlayerId && ctx.Rng.Chance(0.35))
        {
            // The cruellest option: someone the player trusts.
            lover = partner.FriendIds.Select(w.Get)
                .FirstOrDefault(f => f.IsAlive && f.Sex == sex && f.Age(ctx.Year) >= 18 && f.Id != p.Id);
        }
        lover ??= PersonFactory.CreateStranger(ctx, sex, Math.Max(18, age + ctx.Rng.Range(-8, 5)));
        StartAffair(ctx, p, lover);
    }

    public static Secret StartAffair(SimContext ctx, Person subject, Person lover)
    {
        var w = ctx.World;
        var secret = new Secret
        {
            Id = w.Secrets.Count + 1,
            Kind = "affair",
            Year = ctx.Year,
            SubjectId = subject.Id,
            OtherId = lover.Id,
            VictimId = subject.PartnerId,
            KnownBy = new List<int> { subject.Id, lover.Id },
        };
        w.Secrets.Add(secret);
        w.Rel(subject.Id, lover.Id)[RelDim.Attraction] = 85;
        w.Rel(lover.Id, subject.Id)[RelDim.Attraction] = 80;
        w.Rel(subject.Id, lover.Id)[RelDim.Closeness] += 30;
        return secret;
    }
}
