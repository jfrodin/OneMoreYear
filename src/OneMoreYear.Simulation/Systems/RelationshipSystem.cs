using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>Relationship drift, memories, sibling conflicts and friendships.</summary>
public static class RelationshipSystem
{
    /// <summary>
    /// Stores a memory and applies its immediate effect on the relationship to the person it is about.
    /// </summary>
    public static Memory AddMemory(SimContext ctx, Person holder, string kind, string text, double impact, int? aboutId = null)
    {
        var m = new Memory { Year = ctx.Year, Kind = kind, Text = text, Impact = impact, AboutId = aboutId };
        holder.Memories.Add(m);
        holder.Happiness = Math.Clamp(holder.Happiness + impact * 0.15, 0, 100);
        if (aboutId is { } about && about != holder.Id)
        {
            var r = ctx.World.Rel(holder.Id, about);
            if (impact < 0)
            {
                r[RelDim.Bitterness] += -impact * 0.6;
                r[RelDim.Trust] -= -impact * 0.4;
            }
            else
            {
                r[RelDim.Closeness] += impact * 0.4;
                r[RelDim.Trust] += impact * 0.2;
            }
        }
        return m;
    }

    public static void Change(SimContext ctx, int from, int to, RelDim dim, double amount, bool mutual = false)
    {
        ctx.World.Rel(from, to)[dim] += amount;
        if (mutual) ctx.World.Rel(to, from)[dim] += amount;
    }

    public static void MarkContact(SimContext ctx, int a, int b)
    {
        ctx.World.Rel(a, b).LastContactYear = ctx.Year;
        ctx.World.Rel(b, a).LastContactYear = ctx.Year;
    }

    public static void UpdateYear(SimContext ctx)
    {
        var w = ctx.World;

        // Memories fade; strong ones fade slower.
        foreach (var p in w.People)
        {
            if (!p.IsAlive) continue;
            foreach (var m in p.Memories)
                m.Strength *= Math.Abs(m.Impact) >= 50 ? 0.96 : 0.9;
        }

        // Natural contact: children living at home and couples living together.
        foreach (var p in w.People)
        {
            if (!p.IsAlive) continue;
            if (p.Age(ctx.Year) < 18)
                foreach (var parentId in p.ParentIds)
                    if (w.Get(parentId).IsAlive) MarkContact(ctx, p.Id, parentId);
            if (p.PartnerId is { } pid && p.PartnerStatus is PartnerStatus.Cohabiting or PartnerStatus.Married)
                MarkContact(ctx, p.Id, pid);
        }

        foreach (var r in w.Relations)
        {
            var from = w.Get(r.FromId);
            var to = w.Get(r.ToId);
            if (!from.IsAlive || !to.IsAlive) continue;
            Drift(ctx, from, to, r);
        }

        foreach (var p in w.People)
        {
            if (!p.IsAlive || !p.InFamily || p.Age(ctx.Year) < 14) continue;
            SiblingDynamics(ctx, p);
        }

        UpdateFriends(ctx, w.Player);
    }

    private static void Drift(SimContext ctx, Person from, Person to, Relationship r)
    {
        // Memories about this person keep bitterness and warmth alive.
        double grudge = 0, warmth = 0;
        foreach (var m in from.Memories)
        {
            if (m.AboutId != to.Id) continue;
            if (m.Impact < 0) grudge += -m.Impact * m.Strength;
            else warmth += m.Impact * m.Strength;
        }
        double decay = Math.Clamp(0.88 + ctx.Mod(from, "grudge") * 0.06, 0.8, 0.98);
        r.Bitterness = Math.Max(Math.Min(100, grudge * 0.7), r.Bitterness * decay);
        r.Envy *= 0.9;
        r.Fear *= 0.9;
        r.Trust += (50 + warmth * 0.2 - grudge * 0.3 - r.Trust) * 0.05;
        r.Trust = Math.Clamp(r.Trust, 0, 100);

        bool partners = from.PartnerId == to.Id;
        int sinceContact = ctx.Year - r.LastContactYear;
        if (partners)
        {
            r.Attraction = Math.Max(10, r.Attraction - 1.5);
            r.Closeness = Math.Clamp(r.Closeness + (60 + warmth * 0.2 - r.Closeness) * 0.08 + ctx.Rng.Gaussian(0, 3), 0, 100);
        }
        else if (sinceContact > 2)
        {
            bool friend = from.FriendIds.Contains(to.Id);
            r.Closeness = Math.Max(warmth * 0.4, r.Closeness - (friend ? 3 : 1));
        }

        // Siblings and cousins compare themselves with each other.
        if (from.InFamily && to.InFamily && from.Age(ctx.Year) >= 25 && from.Generation == to.Generation)
        {
            double mine = EconomySystem.NetWorth(ctx, from), theirs = EconomySystem.NetWorth(ctx, to);
            if (theirs > mine * 2 + ctx.Nominal(300000)) r[RelDim.Envy] += 3 + ctx.Mod(from, "envy") * 5;
        }
    }

    private static void SiblingDynamics(SimContext ctx, Person p)
    {
        var w = ctx.World;
        foreach (var sib in Kinship.Siblings(w, p))
        {
            if (sib.Id < p.Id || !sib.IsAlive || sib.Age(ctx.Year) < 14) continue;
            double chance = 0.015 + (ctx.Mod(p, "conflict") + ctx.Mod(sib, "conflict")) * 0.02
                            + (w.Rel(p.Id, sib.Id).Envy + w.Rel(sib.Id, p.Id).Envy) / 2000;
            if (ctx.Rng.Chance(chance))
            {
                var (blamer, blamed) = ctx.Rng.Chance(0.5) ? (p, sib) : (sib, p);
                string reason = ctx.Rng.Pick(new[] { "money", "old grievances", "their parents", "a wedding", "politics", "a borrowed car" });
                AddMemory(ctx, blamer, "fight", $"Big fight with {blamed.FirstName} about {reason}", -30, blamed.Id);
                AddMemory(ctx, blamed, "fight", $"Big fight with {blamer.FirstName} about {reason}", -15, blamer.Id);
                w.Log($"{blamer.FirstName} and {blamed.FirstName} fell out after a fight about {reason}.", 1, "relation", blamer.Id, blamed.Id);
                continue;
            }

            var r1 = w.Rel(p.Id, sib.Id);
            var r2 = w.Rel(sib.Id, p.Id);
            if (Math.Max(r1.Bitterness, r2.Bitterness) > 40)
            {
                double reconcile = 0.04 + (ctx.Mod(p, "loyalty") + ctx.Mod(sib, "loyalty")) * 0.03;
                if (ctx.Rng.Chance(reconcile))
                {
                    foreach (var m in p.Memories.Concat(sib.Memories))
                        if ((m.AboutId == sib.Id || m.AboutId == p.Id) && m.Impact < 0) m.Strength *= 0.4;
                    r1.Bitterness *= 0.4;
                    r2.Bitterness *= 0.4;
                    MarkContact(ctx, p.Id, sib.Id);
                    w.Log($"{p.FirstName} and {sib.FirstName} made up.", 1, "relation", p.Id, sib.Id);
                }
            }
        }
    }

    private static void UpdateFriends(SimContext ctx, Person player)
    {
        var w = ctx.World;
        if (!player.IsAlive) return;
        int age = player.Age(ctx.Year);

        foreach (var fid in player.FriendIds.ToList())
        {
            var f = w.Get(fid);
            if (!f.IsAlive) { player.FriendIds.Remove(fid); continue; }
            if (w.Rel(player.Id, fid).Closeness < 12 && w.Rel(fid, player.Id).Closeness < 20)
            {
                player.FriendIds.Remove(fid);
                f.FriendIds.Remove(player.Id);
                w.Log($"You and {f.FirstName} drifted apart and stopped talking.", 1, "relation", player.Id, fid);
            }
        }

        if (age < 5) return;
        int wanted = 2 + (int)Math.Round(ctx.Mod(player, "social") * 2);
        if (player.FriendIds.Count < wanted && ctx.Rng.Chance(0.3)) AddFriend(ctx, player);
    }

    public static Person AddFriend(SimContext ctx, Person p)
    {
        int age = p.Age(ctx.Year);
        int spread = age < 20 ? 1 : 6;
        var sex = ctx.Rng.Chance(0.6) ? p.Sex : (p.Sex == Sex.Male ? Sex.Female : Sex.Male);
        var friend = PersonFactory.CreateStranger(ctx, sex, Math.Max(3, age + ctx.Rng.Range(-spread, spread)));
        p.FriendIds.Add(friend.Id);
        friend.FriendIds.Add(p.Id);
        PersonFactory.SetBond(ctx, p, friend, 55, 55);
        PersonFactory.SetBond(ctx, friend, p, 55, 55);
        ctx.World.Log($"{p.FirstName} became friends with {friend.FirstName}.", 1, "relation", p.Id, friend.Id);
        return friend;
    }

    public static string OpinionLabel(double opinion) => opinion switch
    {
        >= 55 => "Loves",
        >= 25 => "Likes",
        > -15 => "Neutral",
        > -45 => "Dislikes",
        _ => "Hates"
    };
}
