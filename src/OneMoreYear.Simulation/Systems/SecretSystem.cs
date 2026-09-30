using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Secrets separate what is true from who knows it. Affairs can lead to children with a hidden
/// biological father; sooner or later things tend to come out.
/// </summary>
public static class SecretSystem
{
    public static void Update(SimContext ctx)
    {
        var w = ctx.World;
        bool playerLearnedSomething = false;

        foreach (var s in w.Secrets.ToList())
        {
            if (s.Revealed) continue;
            var subject = w.Get(s.SubjectId);
            var victim = w.TryGet(s.VictimId);

            if (s.Kind == "affair")
            {
                if (s.Active) AffairYear(ctx, s, subject);
                if (victim == null || !victim.IsAlive || !subject.IsAlive) { s.Active = false; continue; }

                // Good liars hide it longer.
                double discover = (s.Active ? 0.12 : 0.02) * Math.Clamp(1 - ctx.Mod(subject, "dishonesty") * 0.4, 0.3, 1);
                if (ctx.Rng.Chance(discover))
                {
                    if (victim.Id == w.PlayerId && ctx.Shown(ContentCategories.Infidelity))
                        EventSystem.QueueSituation(ctx, "discovered_partner_affair",
                            new() { ["target"] = s.SubjectId, ["other"] = s.OtherId ?? s.SubjectId }, new() { ["secret"] = s.Id });
                    else
                        RevealAffair(ctx, s, victim);
                    continue;
                }

                if (!playerLearnedSomething && w.Player.IsAlive && !s.KnownBy.Contains(w.PlayerId)
                    && w.PlayerId != s.SubjectId && w.PlayerId != s.OtherId
                    && Kinship.Distances(w, w.Player, 2).ContainsKey(victim.Id)
                    && ctx.Rng.Chance(0.05))
                {
                    s.KnownBy.Add(w.PlayerId);
                    playerLearnedSomething = true;
                    EventSystem.QueueSituation(ctx, "learned_affair",
                        new() { ["target"] = s.SubjectId, ["other"] = victim.Id }, new() { ["secret"] = s.Id });
                }
            }
            else if (s.Kind == "paternity")
            {
                var child = w.TryGet(s.ChildId);
                if (child == null || !child.IsAlive) continue;
                double chance = ctx.Year >= 2000 ? 0.035 : 0.012;
                if (!ctx.Rng.Chance(chance)) continue;
                RevealPaternity(ctx, s);
            }
        }
    }

    private static void AffairYear(SimContext ctx, Secret s, Person subject)
    {
        var w = ctx.World;
        var lover = w.TryGet(s.OtherId);
        if (lover == null || !lover.IsAlive || !subject.IsAlive || subject.PartnerId != s.VictimId)
        {
            s.Active = false;
            return;
        }
        // A child from the affair, raised by the betrayed partner.
        var husband = w.TryGet(s.VictimId);
        if (subject.Sex == Sex.Female && lover.Sex == Sex.Male && husband is { Sex: Sex.Male }
            && FamilySystem.FertilityByAge(ctx, subject.Age(ctx.Year)) > 0 && ctx.Rng.Chance(0.06))
        {
            var child = PersonFactory.CreateBaby(ctx, subject, husband, lover.Id);
            w.Log($"{subject.FirstName} and {husband.FirstName} had a {(child.Sex == Sex.Male ? "son" : "daughter")}, {child.FirstName}.", ctx.Importance(true, subject, husband, child), "family", subject.Id, husband.Id, child.Id);
            w.Secrets.Add(new Secret
            {
                Id = w.Secrets.Count + 1,
                Kind = "paternity",
                Year = ctx.Year,
                SubjectId = subject.Id,
                OtherId = lover.Id,
                VictimId = husband.Id,
                ChildId = child.Id,
                KnownBy = new List<int> { subject.Id },
            });
        }
        if (ctx.Rng.Chance(0.3)) s.Active = false;
    }

    /// <summary>The betrayed partner finds out about the affair.</summary>
    public static void RevealAffair(SimContext ctx, Secret s, Person discoveredBy, bool allowAutoBreakup = true)
    {
        var w = ctx.World;
        s.Revealed = true;
        if (!s.KnownBy.Contains(discoveredBy.Id)) s.KnownBy.Add(discoveredBy.Id);
        var subject = w.Get(s.SubjectId);
        var victim = w.TryGet(s.VictimId);
        var lover = w.TryGet(s.OtherId);
        if (victim == null) return;

        RelationshipSystem.AddMemory(ctx, victim, "betrayed",
            $"{subject.FirstName} cheated on me{(lover != null ? $" with {lover.FirstName}" : "")}", -60, subject.Id);
        if (lover != null)
        {
            RelationshipSystem.AddMemory(ctx, victim, "betrayed_by_lover", $"{lover.FirstName} had an affair with {subject.FirstName}", -40, lover.Id);
            if (victim.FriendIds.Remove(lover.Id)) lover.FriendIds.Remove(victim.Id);
        }
        foreach (var kid in Kinship.Children(w, victim).Where(k => k.IsAlive && subject.ChildIds.Contains(k.Id) && k.Age(ctx.Year) >= 12))
            RelationshipSystem.AddMemory(ctx, kid, "parent_affair", $"Fick veta att {subject.FirstName} varit otrogen", -20, subject.Id);

        w.Log($"It came out that {subject.FirstName} had been cheating on {victim.FirstName}{(lover != null ? $" with {lover.FullName}" : "")}.",
            3, "secret", subject.Id, victim.Id, lover?.Id ?? subject.Id);

        if (allowAutoBreakup && victim.PartnerId == subject.Id && victim.Id != w.PlayerId)
        {
            double leave = 0.55 - ctx.Mod(victim, "loyalty") * 0.2 + ctx.Mod(victim, "conflict") * 0.1;
            if (ctx.Rng.Chance(leave)) FamilySystem.BreakUp(ctx, victim, subject);
        }
    }

    public static void RevealPaternity(SimContext ctx, Secret s)
    {
        var w = ctx.World;
        s.Revealed = true;
        var mother = w.Get(s.SubjectId);
        var bio = w.TryGet(s.OtherId);
        var legal = w.TryGet(s.VictimId);
        var child = w.Get(s.ChildId!.Value);

        if (legal is { IsAlive: true })
        {
            RelationshipSystem.AddMemory(ctx, legal, "not_my_child", $"{child.FirstName} is not my biological child", -55, mother.Id);
            w.Rel(legal.Id, child.Id)[RelDim.Closeness] -= 15;
        }
        if (child.Age(ctx.Year) >= 10)
            RelationshipSystem.AddMemory(ctx, child, "true_father",
                $"Found out that my biological father is {bio?.FullName ?? "someone else"}", -35, mother.Id);

        w.Log($"A DNA test revealed that {Kinship.Genitive(child.FirstName)} biological father is {bio?.FullName ?? "unknown"}, not {legal?.FirstName}.",
            3, "secret", child.Id, mother.Id, legal?.Id ?? mother.Id, bio?.Id ?? mother.Id);

        if (child.Id == w.PlayerId)
            EventSystem.QueueSituation(ctx, "paternity_revealed_child", new() { ["target"] = bio?.Id ?? mother.Id, ["other"] = mother.Id });
        else if (legal?.Id == w.PlayerId)
            EventSystem.QueueSituation(ctx, "paternity_revealed_father", new() { ["target"] = mother.Id, ["other"] = child.Id });
        else if (legal != null && legal.PartnerId == mother.Id && ctx.Rng.Chance(0.5))
            FamilySystem.BreakUp(ctx, legal, mother);
    }
}
