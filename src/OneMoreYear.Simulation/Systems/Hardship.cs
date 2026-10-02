using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// The hardest things in a life, beyond DarkSystem: sexual violence between adults, suicide, relapse
/// into addiction, and losing your home. All follow the content settings. As with abuse of children,
/// sexual violence is never something the player does and is never described – only what it leaves
/// behind (docs/design-decisions.md).
/// </summary>
public static class Hardship
{
    public const string SurvivorFlag = "survivor";
    public const string HomelessFlag = "homeless";
    public const string RecoveredPrefix = "recovered_";

    public static void Update(SimContext ctx)
    {
        var w = ctx.World;
        int count = w.People.Count;
        for (int i = 0; i < count; i++)
        {
            var p = w.People[i];
            if (!p.IsAlive || !(p.InFamily || p.Id == w.PlayerId)) continue;
            int age = p.Age(ctx.Year);
            if (age >= 16 && p.Id != w.PlayerId && ctx.Happens(ContentCategories.SexualViolence)) MaybeAssault(ctx, p);
            if (age >= 14) MaybeRelapse(ctx, p);
            if (age >= 16) MaybeSuicide(ctx, p);
            Home(ctx, p);
        }
        RevealAssaults(ctx);
    }

    // --- Sexual violence (adults) ------------------------------------------------------------

    private static void MaybeAssault(SimContext ctx, Person victim)
    {
        var rng = ctx.Rng;
        int age = victim.Age(ctx.Year);
        double chance = (victim.Sex == Sex.Female ? 0.003 : 0.0008) * (age < 30 ? 1.5 : age > 60 ? 0.3 : 1);
        if (!rng.Chance(chance)) return;
        var w = ctx.World;

        // Sometimes it is someone in the family: a partner or a relative with a cruel streak.
        var family = Kinship.Distances(w, victim, 2).Keys.Select(w.Get)
            .Append(w.TryGet(victim.PartnerId)).OfType<Person>()
            .Where(x => x.IsAlive && x.Id != victim.Id && x.Id != w.PlayerId && x.Age(ctx.Year) >= 18
                        && (x.HasTrait("cruel") || x.HasTrait("predatory") || x.HasTrait("manipulative")))
            .Distinct().ToList();
        Person? perpetrator = family.Count > 0 && rng.Chance(0.25) ? rng.Pick(family) : null;
        Assaulted(ctx, victim, perpetrator);
    }

    /// <summary>What an assault leaves behind. A known perpetrator becomes a family secret.</summary>
    public static void Assaulted(SimContext ctx, Person victim, Person? perpetrator)
    {
        var w = ctx.World;
        victim.Flags.Add(SurvivorFlag);
        RelationshipSystem.AddMemory(ctx, victim, "assaulted",
            perpetrator != null ? $"What {perpetrator.FirstName} did to me" : "The night I was assaulted", -65, perpetrator?.Id);
        victim.Happiness = Math.Max(0, victim.Happiness - 20);
        AilmentSystem.MaybeTrauma(ctx, victim, 0.45);
        if (perpetrator != null)
        {
            w.Rel(victim.Id, perpetrator.Id)[RelDim.Fear] += 45;
            w.Secrets.Add(new Secret
            {
                Id = w.Secrets.Count + 1, Kind = "assault", Year = ctx.Year,
                SubjectId = perpetrator.Id, VictimId = victim.Id, KnownBy = new List<int> { perpetrator.Id, victim.Id },
            });
        }

        // The player learns when it happens to someone close – and has to decide how to be there.
        var player = w.Player;
        bool close = victim.PartnerId == player.Id || player.ChildIds.Contains(victim.Id) || player.ParentIds.Contains(victim.Id)
                     || Kinship.Siblings(w, player).Any(s => s.Id == victim.Id);
        if (close && player.IsAlive && ctx.Shown(ContentCategories.SexualViolence))
            EventSystem.QueueSituation(ctx, "loved_one_assaulted", new() { ["target"] = victim.Id });
    }

    /// <summary>Survivors may tell the family who did it, years later. Then the family splits.</summary>
    private static void RevealAssaults(SimContext ctx)
    {
        var w = ctx.World;
        foreach (var s in w.Secrets.Where(s => s.Kind == "assault" && !s.Revealed).ToList())
        {
            var victim = w.TryGet(s.VictimId);
            var perpetrator = w.Get(s.SubjectId);
            if (victim is not { IsAlive: true } || victim.Id == w.PlayerId) continue;
            if (!ctx.Rng.Chance(0.06 + ctx.Mod(victim, "resilience") * 0.04)) continue;
            s.Revealed = true;
            w.Log($"{victim.FirstName} told the family what {perpetrator.FullName} had done to {(victim.Sex == Sex.Male ? "him" : "her")}.",
                3, "secret", victim.Id, perpetrator.Id);
            foreach (var relative in Kinship.Distances(w, victim, 2).Keys.Select(w.Get).Where(r => r.IsAlive && r.Id != perpetrator.Id && r.Id != victim.Id))
            {
                if (!s.KnownBy.Contains(relative.Id)) s.KnownBy.Add(relative.Id);
                RelationshipSystem.AddMemory(ctx, relative, "learned_assault", $"Found out what {perpetrator.FirstName} did to {victim.FirstName}", -45, perpetrator.Id);
            }
            if (perpetrator.IsAlive && perpetrator.Activity != Activity.Prison && ctx.Rng.Chance(0.3))
                CrimeSystem.Arrest(ctx, perpetrator, ctx.Content.Crimes["sexual_assault"], victim, perpetrator.Id == w.PlayerId);
            if (w.TryGet(perpetrator.PartnerId) is { } partner && partner.Id != victim.Id && ctx.Rng.Chance(0.4))
                FamilySystem.BreakUp(ctx, partner, perpetrator);
        }
    }

    // --- Suicide ---------------------------------------------------------------------------

    /// <summary>
    /// Deep, long depression can end in suicide – for other people, as grief and consequence. For the
    /// player it becomes "the darkest night", where every choice is a way to reach for help.
    /// </summary>
    private static void MaybeSuicide(SimContext ctx, Person p)
    {
        if (!p.Ailments.TryGetValue("depression", out int since) || p.Happiness > 15) return;
        var w = ctx.World;
        if (p.Id == w.PlayerId)
        {
            if (ctx.Shown(ContentCategories.Suicide) && !p.Flags.Contains($"darkest_night_{ctx.Year - 1}") && ctx.Rng.Chance(0.35))
            {
                p.Flags.Add($"darkest_night_{ctx.Year}");
                EventSystem.QueueSituation(ctx, "darkest_night");
            }
            return;
        }
        if (!ctx.Happens(ContentCategories.Suicide)) return;
        double chance = 0.012 + (ctx.Year - since) * 0.003 - (p.Flags.Contains(AilmentSystem.TreatedPrefix + "depression") ? 0.008 : 0);
        if (!ctx.Rng.Chance(Math.Max(0.002, chance))) return;
        LifeSystem.Die(ctx, p, "suicide");
        foreach (var other in Kinship.Circle(w, p).Where(x => x.IsAlive))
        {
            var rel = w.FindRel(other.Id, p.Id);
            if (rel == null || rel.Closeness < 40) continue;
            RelationshipSystem.AddMemory(ctx, other, "lost_to_suicide", $"Wondering if I could have saved {p.FirstName}", -35, mentionId: p.Id);
            AilmentSystem.MaybeTrauma(ctx, other, 0.1);
        }
        if (ctx.Shown(ContentCategories.Suicide) && w.FindRel(w.PlayerId, p.Id) is { Closeness: >= 40 })
            EventSystem.QueueSituation(ctx, "loss_by_suicide", new() { ["target"] = p.Id });
    }

    // --- Relapse ---------------------------------------------------------------------------

    /// <summary>Getting clean is not the end of it: bad years can bring the old habit back.</summary>
    private static void MaybeRelapse(SimContext ctx, Person p)
    {
        if (p.Addiction != null || !ctx.Happens(ContentCategories.Addiction)) return;
        var old = p.Flags.FirstOrDefault(f => f.StartsWith(RecoveredPrefix));
        if (old == null) return;
        double chance = 0.02 + (p.Happiness < 35 ? 0.08 : 0) + ctx.Mod(p, "addiction") * 0.03;
        if (!ctx.Rng.Chance(chance)) return;
        if (p.Id == ctx.World.PlayerId && !ctx.Shown(ContentCategories.Addiction)) return;
        p.Addiction = old[RecoveredPrefix.Length..];
        p.AddictionSince = ctx.Year;
        p.Flags.Add("addicted");
        if (p.InFamily || p.Id == ctx.World.PlayerId)
            ctx.World.Log($"{p.FirstName} started {DarkSystem.What(p.Addiction) switch { "drugs" => "using", "gambling" => "gambling", _ => "drinking" }} again.",
                ctx.Importance(false, p), "dark", p.Id);
        if (p.Id == ctx.World.PlayerId) EventSystem.QueueSituation(ctx, "relapse");
    }

    // --- Losing your home ------------------------------------------------------------------

    private static void Home(SimContext ctx, Person p)
    {
        var w = ctx.World;
        if (p.Flags.Contains(HomelessFlag))
        {
            p.Happiness = Math.Max(0, p.Happiness - 6);
            p.Health = Math.Max(1, p.Health - 3);
            if (p.Money > 0 || p.Activity == Activity.Working)
            {
                p.Flags.Remove(HomelessFlag);
                if (p.InFamily || p.Id == w.PlayerId) w.Log($"{p.FirstName} got a roof over {(p.Sex == Sex.Male ? "his" : "her")} head again.", ctx.Importance(false, p), "home", p.Id);
            }
            return;
        }
        // Deep debt and no income: the bailiffs come for renters.
        if (p.OwnsHome || p.LivesWithParents || p.Flags.Contains(HousingSystem.CareHomeFlag) || p.Age(ctx.Year) < 18) return;
        if (p.Activity is Activity.Working or Activity.Prison || p.Money > -ctx.NominalRef(120000) || !ctx.Rng.Chance(0.3)) return;
        if (p.PartnerId != null && p.PartnerStatus is PartnerStatus.Cohabiting or PartnerStatus.Married) return;
        if (p.Id == w.PlayerId) { EventSystem.QueueSituation(ctx, "eviction"); return; }
        if (Kinship.Parents(w, p).Any(x => x.IsAlive) && ctx.Rng.Chance(0.6)) HousingSystem.MoveBackHome(ctx, p);
        else BecomeHomeless(ctx, p);
    }

    public static void BecomeHomeless(SimContext ctx, Person p)
    {
        p.Flags.Add(HomelessFlag);
        p.SharesFlat = false;
        if (p.InFamily || p.Id == ctx.World.PlayerId)
            ctx.World.Log($"{p.FirstName} was evicted and ended up homeless.", ctx.Importance(true, p), "home", p.Id);
    }
}
