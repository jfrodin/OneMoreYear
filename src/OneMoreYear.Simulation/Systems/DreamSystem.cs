using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Life dreams (docs/endgame.md): each played person may choose one thing they want from life. It is
/// fulfilled, or given up at its deadline or at death. A dream a parent never reached can be taken
/// over by a child. The dreams are content/dreams.json; the goal kinds are here.
/// </summary>
public static class DreamSystem
{
    public const string ChooseEvent = "choose_dream", FulfilledEvent = "dream_fulfilled", FailedEvent = "dream_failed";
    public const string InheritPrefix = "inherit:";

    public static readonly string[] Goals =
    {
        "job", "top_job", "degree", "children", "grandchildren", "married", "home", "big_home", "wealth",
        "emigrate", "age", "cottage", "close_family", "golden_wedding", "never_divorced",
    };

    public static DreamDef? Of(SimContext ctx, Person p) => p.Dream != null ? ctx.Content.Dreams.GetValueOrDefault(p.Dream) : null;

    /// <summary>Has the person got what the dream asks for?</summary>
    public static bool Reached(SimContext ctx, Person p, DreamDef d)
    {
        var w = ctx.World;
        int age = p.Age(ctx.Year);
        return d.Goal switch
        {
            "job" => d.Param != null && p.Jobs.Contains(d.Param),
            "top_job" => p.Activity == Activity.Working && ctx.Content.Occupation(p.OccupationId) is { } occ && occ.Id != "crime"
                         && (d.Param == null || occ.Id == d.Param) && p.OccupationLevel >= occ.Levels.Count - 1,
            "degree" => p.Education == EducationLevel.University,
            "children" => p.ChildIds.Count >= d.Amount,
            "grandchildren" => Kinship.Children(w, p).Sum(c => c.ChildIds.Count) >= d.Amount,
            "married" => p.Memories.Any(m => m.Kind == "wedding"),
            "home" => p.OwnsHome,
            "big_home" => p.OwnsHome && HousingSystem.HomeTypeOf(ctx, p) is { PriceFactor: >= 1.3 },
            "wealth" => p.PeakRefWorth >= d.Amount,
            "emigrate" => p.Memories.Any(m => m.Kind == "emigrated"),
            "age" => age >= d.Amount,
            "cottage" => p.CottageValue > 0,
            "close_family" => age >= 60 && Kinship.Children(w, p).Count(c => c.IsAlive && c.Age(ctx.Year) >= 18) is var n && n >= 2
                              && Kinship.Children(w, p).Where(c => c.IsAlive && c.Age(ctx.Year) >= 18).All(c => w.Opinion(c.Id, p.Id) >= d.Amount),
            "golden_wedding" => p.PartnerStatus == PartnerStatus.Married && ctx.Year - p.PartnerSinceYear >= d.Amount,
            // Only known at the end of a life.
            "never_divorced" => false,
            _ => false,
        };
    }

    /// <summary>Can this dream still come true for the person (and is it not already true)?</summary>
    private static bool Possible(SimContext ctx, Person p, DreamDef d)
    {
        int age = p.Age(ctx.Year);
        if (d.MinYear > ctx.Year || (d.Countries.Count > 0 && !d.Countries.Contains(ctx.Country.Id))) return false;
        if (d.Deadline is { } deadline && age >= deadline - 2) return false;
        if (d.Goal == "never_divorced" && p.Flags.Contains("divorced")) return false;
        return !Reached(ctx, p, d);
    }

    /// <summary>The year's dream business for the player: fulfilled, given up, or a first offer at sixteen.</summary>
    public static void Update(SimContext ctx)
    {
        var w = ctx.World;
        var p = w.Player;
        if (!p.IsAlive) return;
        if (p.DreamState == DreamState.Active && Of(ctx, p) is { } dream)
        {
            if (Reached(ctx, p, dream)) Fulfil(ctx, p, dream);
            else if (dream.Deadline is { } deadline && p.Age(ctx.Year) >= deadline) Fail(ctx, p, dream);
        }
        else if (p.Dream == null && p.DreamOffered == null && p.Age(ctx.Year) >= 16)
            Offer(ctx, p);
    }

    /// <summary>Queues the choice of a dream: three that fit the person, and a parent's unfinished one.</summary>
    public static void Offer(SimContext ctx, Person p)
    {
        if (p.Dream != null || p.Age(ctx.Year) < 16) return;
        var options = new List<string>();
        // A parent's dream that never came true comes first.
        foreach (var parent in Kinship.Parents(ctx.World, p).Where(x => x.DreamState == DreamState.Failed && x.Dream != null))
            if (Of(ctx, parent) is { } theirs && Possible(ctx, p, theirs) && theirs.MinAge <= Math.Max(p.Age(ctx.Year), theirs.MinAge))
                options.Add($"{InheritPrefix}{theirs.Id}:{parent.Id}");

        int age = p.Age(ctx.Year);
        var pool = ctx.Content.Dreams.Values.Where(d => age >= d.MinAge && age <= d.MaxAge && Possible(ctx, p, d))
            .OrderBy(d => d.Id, StringComparer.Ordinal).ToList();
        while (options.Count(o => !o.StartsWith(InheritPrefix)) < 3 && pool.Count > 0)
        {
            var pick = ctx.Rng.PickWeighted(pool, d => Weight(p, d))!;
            pool.Remove(pick);
            options.Add(pick.Id);
        }
        if (options.Count == 0) return;
        p.DreamOffered = ctx.Year;
        if (EventSystem.QueueSituation(ctx, ChooseEvent) is { } pending) pending.Options = options;
    }

    private static double Weight(Person p, DreamDef d)
    {
        double w = 1;
        foreach (var (trait, factor) in d.Traits)
            if (p.HasTrait(trait)) w *= factor;
        return w;
    }

    /// <summary>The name and line shown for an option of the choice.</summary>
    public static (string Name, string Text) Describe(SimContext ctx, string option)
    {
        if (option.StartsWith(InheritPrefix))
        {
            var parts = option[InheritPrefix.Length..].Split(':');
            var d = ctx.Content.Dreams[parts[0]];
            var parent = ctx.World.Get(int.Parse(parts[1]));
            return ($"{d.Name}, as {parent.FirstName} never did", $"{parent.FirstName} wanted this all {(parent.Sex == Sex.Male ? "his" : "her")} life. You could finish it.");
        }
        var def = ctx.Content.Dreams[option];
        return (def.Name, def.Text);
    }

    public static string Choose(SimContext ctx, Person p, string option)
    {
        int? from = null;
        string id = option;
        if (option.StartsWith(InheritPrefix))
        {
            var parts = option[InheritPrefix.Length..].Split(':');
            id = parts[0];
            from = int.Parse(parts[1]);
        }
        var d = ctx.Content.Dreams[id];
        p.Dream = d.Id;
        p.DreamState = DreamState.Active;
        p.DreamYear = ctx.Year;
        p.DreamFromId = from;
        return from != null
            ? $"For {ctx.World.Get(from.Value).FirstName}, and for yourself: {d.Name.ToLowerInvariant()}."
            : $"Somewhere at the back of your mind, a plan: {d.Name.ToLowerInvariant()}.";
    }

    private static void Fulfil(SimContext ctx, Person p, DreamDef d)
    {
        var w = ctx.World;
        p.DreamState = DreamState.Fulfilled;
        p.Happiness = Math.Min(100, p.Happiness + 12);
        RelationshipSystem.AddMemory(ctx, p, "dream", $"{d.Name}: I did it", 30);
        w.Log($"{p.FirstName} lived {(p.Sex == Sex.Male ? "his" : "her")} dream: {d.Name.ToLowerInvariant()}.", 3, "life", p.Id);
        foreach (var child in Kinship.Children(w, p).Where(c => c.IsAlive && c.Age(ctx.Year) >= 6))
            RelationshipSystem.AddMemory(ctx, child, "parent_dream", $"{p.FirstName} lived {(p.Sex == Sex.Male ? "his" : "her")} dream", 8, p.Id);
        if (p.DreamFromId != null) w.Feats.Add("inherited_dream");
        w.Feats.Add("dream");
        if (p.Id == w.PlayerId) EventSystem.QueueSituation(ctx, FulfilledEvent);
    }

    private static void Fail(SimContext ctx, Person p, DreamDef d)
    {
        p.DreamState = DreamState.Failed;
        RelationshipSystem.AddMemory(ctx, p, "dream_lost", $"{d.Name}: it never happened", -18);
        if (p.Id == ctx.World.PlayerId && p.IsAlive) EventSystem.QueueSituation(ctx, FailedEvent);
    }

    /// <summary>At the player's death: some dreams are only true at the end; the rest are lost.</summary>
    public static void OnDeath(SimContext ctx, Person p)
    {
        if (p.DreamState != DreamState.Active || Of(ctx, p) is not { } d) return;
        if (d.Goal == "never_divorced" && p.Memories.Any(m => m.Kind == "wedding") && !p.Flags.Contains("divorced"))
        {
            p.DreamState = DreamState.Fulfilled;
            ctx.World.Feats.Add("dream");
            if (p.DreamFromId != null) ctx.World.Feats.Add("inherited_dream");
            ctx.World.Log($"{p.FirstName} kept {(p.Sex == Sex.Male ? "his" : "her")} promise to the end.", 2, "life", p.Id);
            return;
        }
        Fail(ctx, p, d);
    }
}
