using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// The dark side of family life, driven by traits: addiction, violence at home and abuse that
/// becomes a secret. Abuse is only ever described through its consequences – see
/// docs/design-decisions.md. The player is never made to abuse anyone.
/// </summary>
public static class DarkSystem
{
    private static readonly string[] TraumaTraits = { "gloomy", "paranoid", "addictive", "hot_tempered" };

    public static void Update(SimContext ctx)
    {
        var w = ctx.World;
        int count = w.People.Count;
        for (int i = 0; i < count; i++)
        {
            var p = w.People[i];
            if (!p.IsAlive || !(p.InFamily || p.Id == w.PlayerId)) continue;
            int age = p.Age(ctx.Year);
            if (age >= 14) Addiction(ctx, p);
            if (age >= 18 && p.Id != w.PlayerId) Violence(ctx, p);
            if (age >= 18 && p.Id != w.PlayerId && ctx.Mod(p, "predatory") > 0) Abuse(ctx, p);
            if (age >= 60 && ctx.Rng.Chance(0.03)) Mellow(ctx, p);
        }
        RevealAbuse(ctx);
    }

    // --- Addiction --------------------------------------------------------------------------

    private static void Addiction(SimContext ctx, Person p)
    {
        var rng = ctx.Rng;
        var w = ctx.World;
        bool isPlayer = p.Id == w.PlayerId;
        if (p.Addiction == null)
        {
            double chance = 0.003 + ctx.Mod(p, "addiction") * 0.035 + (p.Happiness < 35 ? 0.015 : 0);
            if (!rng.Chance(chance)) return;
            int age = p.Age(ctx.Year);
            p.Addiction = rng.PickWeighted(new[] { "alcohol", "drugs", "gambling" },
                k => k switch { "drugs" => age < 30 ? 0.4 : 0.1, "gambling" => 0.15, _ => 0.5 });
            p.AddictionSince = ctx.Year;
            p.Flags.Add("addicted");
            w.Log($"{p.FirstName} started struggling with {What(p.Addiction)}.", ctx.Importance(false, p), "dark", p.Id);
            if (isPlayer) EventSystem.QueueSituation(ctx, "addiction_begins");
            return;
        }

        // Living with it.
        p.Health = Math.Max(1, p.Health - (p.Addiction == "drugs" ? 5 : p.Addiction == "alcohol" ? 3 : 0.5));
        double cost = p.Addiction switch { "drugs" => 50000, "gambling" => 60000, _ => 20000 };
        p.Money -= ctx.Nominal(cost);
        EconomySystem.Record(ctx, p, $"Your {What(p.Addiction)} habit", -ctx.Nominal(cost));
        p.Performance = Math.Max(0, p.Performance - 8);
        if (p.PartnerId is { } pid) RelationshipSystem.Change(ctx, pid, p.Id, RelDim.Bitterness, 6);
        foreach (var kid in Kinship.Children(w, p).Where(k => k.IsAlive && k.Age(ctx.Year) is >= 5 and < 18))
        {
            if (kid.Memories.Any(m => m.Kind == "addicted_parent" && m.AboutId == p.Id)) continue;
            string text = p.Addiction switch
            {
                "alcohol" => $"{p.FirstName} was always drunk when I was a kid",
                "drugs" => $"{p.FirstName} was on drugs when I grew up",
                _ => $"{p.FirstName} gambled away our money"
            };
            RelationshipSystem.AddMemory(ctx, kid, "addicted_parent", text, -35, p.Id);
        }

        if (p.Addiction == "drugs" && rng.Chance(0.02))
        {
            LifeSystem.Die(ctx, p, "an overdose");
            return;
        }
        if (isPlayer) return; // the player decides through events
        if (rng.Chance(0.06 + ctx.Mod(p, "resilience") * 0.1)) Recover(ctx, p);
    }

    public static void Recover(SimContext ctx, Person p)
    {
        if (p.Addiction == null) return;
        ctx.World.Log($"{p.FirstName} got clean after {Math.Max(1, ctx.Year - p.AddictionSince)} years of {What(p.Addiction)}.",
            ctx.Importance(false, p), "dark", p.Id);
        p.Addiction = null;
        p.Flags.Remove("addicted");
        p.Happiness += 10;
    }

    public static string What(string? addiction) => addiction switch
    {
        "drugs" => "drugs",
        "gambling" => "gambling",
        _ => "alcohol"
    };

    // --- Violence at home -------------------------------------------------------------------

    private static void Violence(SimContext ctx, Person p)
    {
        var w = ctx.World;
        double violence = ctx.Mod(p, "violence");
        if (violence <= 0) return;
        double chance = 0.02 * violence + (p.Addiction == "alcohol" ? 0.03 : 0);
        if (!ctx.Rng.Chance(chance)) return;

        var victims = new List<Person>();
        if (w.TryGet(p.PartnerId) is { IsAlive: true } partner
            && p.PartnerStatus is PartnerStatus.Cohabiting or PartnerStatus.Married) victims.Add(partner);
        victims.AddRange(Kinship.Children(w, p).Where(k => k.IsAlive && k.Age(ctx.Year) is >= 2 and < 18));
        if (victims.Count == 0) return;
        Hit(ctx, p, ctx.Rng.Pick(victims));
    }

    /// <summary>Someone lashes out and hits a partner or child. Everyone in the home remembers.</summary>
    public static void Hit(SimContext ctx, Person abuser, Person victim)
    {
        var w = ctx.World;
        RelationshipSystem.AddMemory(ctx, victim, "hit", $"{abuser.FirstName} hit me", -45, abuser.Id);
        w.Rel(victim.Id, abuser.Id)[RelDim.Fear] += 35;
        victim.Health = Math.Max(1, victim.Health - 4);
        foreach (var kid in Kinship.Children(w, abuser).Where(k => k.IsAlive && k.Id != victim.Id && k.Age(ctx.Year) is >= 4 and < 18))
            RelationshipSystem.AddMemory(ctx, kid, "witnessed_violence", $"Saw {abuser.FirstName} hit {victim.FirstName}", -25, abuser.Id);
        w.Log($"{abuser.FirstName} hit {victim.FirstName} in a fit of rage.", ctx.Importance(true, abuser, victim), "dark", abuser.Id, victim.Id);

        if (victim.Id == w.PlayerId)
        {
            string eventId = victim.PartnerId == abuser.Id ? "violence_by_partner" : "violence_by_parent";
            EventSystem.QueueSituation(ctx, eventId, new() { ["target"] = abuser.Id });
            return;
        }
        if (abuser.PartnerId == victim.Id && ctx.Rng.Chance(0.3 + ctx.Mod(victim, "resilience") * 0.2))
            FamilySystem.BreakUp(ctx, victim, abuser);
    }

    // --- Abuse (a secret) -----------------------------------------------------------------

    private static void Abuse(SimContext ctx, Person predator)
    {
        var w = ctx.World;
        if (!ctx.Rng.Chance(0.07)) return;
        var children = Kinship.Distances(w, predator, 2).Keys.Select(w.Get)
            .Where(c => c.IsAlive && c.Age(ctx.Year) is >= 5 and <= 13 && c.Id != predator.Id)
            .Where(c => !w.Secrets.Any(s => s.Kind == "abuse" && s.VictimId == c.Id))
            .ToList();
        if (children.Count == 0) return;
        var child = ctx.Rng.Pick(children);

        w.Secrets.Add(new Secret
        {
            Id = w.Secrets.Count + 1,
            Kind = "abuse",
            Year = ctx.Year,
            SubjectId = predator.Id,
            VictimId = child.Id,
            KnownBy = new List<int> { predator.Id, child.Id },
        });
        string who = Kinship.Label(w, child, predator);
        RelationshipSystem.AddMemory(ctx, child, "abused",
            $"Something happened with {predator.FirstName} ({who}) that I can never talk about", -75, predator.Id);
        w.Rel(child.Id, predator.Id)[RelDim.Fear] += 50;
        child.Happiness = Math.Max(0, child.Happiness - 20);
        Traumatize(ctx, child, 0.6);

        if (child.Id == w.PlayerId)
            EventSystem.QueueSituation(ctx, "abuse_child", new() { ["target"] = predator.Id });
    }

    /// <summary>Victims who grow up may one day tell the family. Then everything changes.</summary>
    private static void RevealAbuse(SimContext ctx)
    {
        var w = ctx.World;
        foreach (var s in w.Secrets.Where(s => s.Kind == "abuse" && !s.Revealed).ToList())
        {
            var victim = w.TryGet(s.VictimId);
            var predator = w.Get(s.SubjectId);
            if (victim is not { IsAlive: true } || victim.Age(ctx.Year) < 16) continue;
            if (victim.Id == w.PlayerId)
            {
                if (victim.Age(ctx.Year) >= 18 && !victim.Flags.Contains($"carried_secret_{s.Id}"))
                {
                    victim.Flags.Add($"carried_secret_{s.Id}");
                    EventSystem.QueueSituation(ctx, "carry_secret", new() { ["target"] = predator.Id }, new() { ["secret"] = s.Id });
                }
                continue;
            }
            double chance = 0.04 + (predator.IsAlive ? 0 : 0.06) + ctx.Mod(victim, "resilience") * 0.05;
            if (ctx.Rng.Chance(chance)) Reveal(ctx, s);
        }
    }

    /// <summary>The victim tells the family. Relatives turn against the abuser.</summary>
    public static void Reveal(SimContext ctx, Secret s)
    {
        var w = ctx.World;
        if (s.Revealed) return;
        s.Revealed = true;
        var victim = w.Get(s.VictimId!.Value);
        var predator = w.Get(s.SubjectId);
        w.Log($"{victim.FirstName} revealed that {predator.FullName} abused {(victim.Sex == Sex.Male ? "him" : "her")} as a child.",
            3, "secret", victim.Id, predator.Id);
        RelationshipSystem.AddMemory(ctx, victim, "told_family", "Finally told the family what happened", 15);
        foreach (var relative in Kinship.Distances(w, victim, 2).Keys.Select(w.Get).Where(r => r.IsAlive && r.Id != predator.Id))
        {
            if (!s.KnownBy.Contains(relative.Id)) s.KnownBy.Add(relative.Id);
            RelationshipSystem.AddMemory(ctx, relative, "learned_abuse", $"Found out what {predator.FirstName} did to {victim.FirstName}", -50, predator.Id);
            RelationshipSystem.Change(ctx, relative.Id, victim.Id, RelDim.Closeness, 10);
        }
        if (predator.IsAlive && w.TryGet(predator.PartnerId) is { } partner && ctx.Rng.Chance(0.5))
            FamilySystem.BreakUp(ctx, partner, predator);
    }

    // --- Traits change with life ------------------------------------------------------------

    /// <summary>A trauma can leave a mark: a new dark trait, unless the person is resilient.</summary>
    public static void Traumatize(SimContext ctx, Person p, double chance)
    {
        chance *= 1 - Math.Clamp(ctx.Mod(p, "resilience"), 0, 0.9);
        if (!ctx.Rng.Chance(chance)) return;
        var trait = ctx.Rng.Pick(TraumaTraits);
        if (PersonFactory.TryAddTrait(ctx, p, trait) && p.Id == ctx.World.PlayerId)
            ctx.World.Log($"{p.FirstName} became more {ctx.Content.Traits[trait].Name.ToLowerInvariant()}.", 2, "trait", p.Id);
    }

    /// <summary>Old age softens some edges.</summary>
    private static void Mellow(SimContext ctx, Person p)
    {
        foreach (var t in new[] { "hot_tempered", "impulsive" })
        {
            if (!p.Traits.Remove(t)) continue;
            if (p.Id == ctx.World.PlayerId)
                ctx.World.Log($"{p.FirstName} mellowed with age – no longer {ctx.Content.Traits[t].Name.ToLowerInvariant()}.", 1, "trait", p.Id);
            return;
        }
    }
}
