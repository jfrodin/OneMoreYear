using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>Health, illness, death, wills and inheritance.</summary>
public static class LifeSystem
{
    public static void UpdateHealth(SimContext ctx, Person p)
    {
        var rng = ctx.Rng;
        int age = p.Age(ctx.Year);
        double change = age switch
        {
            < 30 => rng.Gaussian(1, 1.5),
            < 50 => rng.Gaussian(-0.4, 1.5),
            < 70 => rng.Gaussian(-1.1, 1.8),
            _ => rng.Gaussian(-2.2, 2.2)
        };
        change += ctx.Mod(p, "health");
        if (p.Happiness < 25) change -= 1;
        p.Health = Math.Clamp(p.Health + change, 1, 100);
        p.Happiness = Math.Clamp(p.Happiness + (60 - p.Happiness) * 0.1, 0, 100);

        // Serious illness.
        double illness = age < 30 ? 0.004 : 0.004 + (age - 30) * 0.0009;
        if (rng.Chance(illness))
        {
            double hit = rng.Range(15, 40);
            p.Health = Math.Max(1, p.Health - hit);
            string what = rng.Pick(new[] { "cancer", "a heart attack", "a stroke", "severe pneumonia", "diabetes", "depression" });
            if (p.InFamily || p.Id == ctx.World.PlayerId)
                ctx.World.Log($"{p.FirstName} was struck by {what}.", ctx.Importance(false, p), "health", p.Id);
            p.Happiness -= 10;
        }
    }

    public static double MortalityChance(SimContext ctx, Person p)
    {
        int age = p.Age(ctx.Year);
        double baseRate = 0.0002 + 0.000028 * Math.Exp(0.095 * age);
        if (age == 0) baseRate += ctx.Year < 1970 ? 0.012 : 0.003;
        double healthFactor = Math.Clamp(Math.Exp((60 - p.Health) / 22), 0.3, 25);
        return Math.Min(0.95, baseRate * healthFactor * ctx.Country.MortalityScale * (1 + ctx.Mod(p, "risk") * 0.5));
    }

    public static bool CheckDeath(SimContext ctx, Person p)
    {
        if (!ctx.Rng.Chance(MortalityChance(ctx, p))) return false;
        int age = p.Age(ctx.Year);
        string cause = age < 15
            ? ctx.Rng.Pick(new[] { "a sudden illness", "an accident", "a drowning accident", "leukaemia" })
            : age < 45
            ? ctx.Rng.Pick(new[] { "a car accident", "a drowning accident", "cancer", "a heart attack", "a sudden illness" })
            : age < 85
                ? ctx.Rng.Pick(new[] { "cancer", "a heart attack", "a stroke", "heart failure", "lung cancer" })
                : ctx.Rng.Pick(new[] { "old age", "heart failure", "pneumonia", "a stroke" });
        Die(ctx, p, cause);
        return true;
    }

    public static void Die(SimContext ctx, Person p, string cause)
    {
        var w = ctx.World;
        p.DeathYear = ctx.Year;
        p.CauseOfDeath = cause;
        int age = p.Age(ctx.Year);

        bool close = p.Id == w.PlayerId || (w.Player.IsAlive && Kinship.Distances(w, w.Player, 2).ContainsKey(p.Id));
        if (p.InFamily || p.Id == w.PlayerId || close)
            w.Log($"{p.FullName} died of {cause}, aged {age}.", ctx.Importance(true, p), "death", p.Id);

        // Grief.
        foreach (var other in Kinship.Circle(w, p))
        {
            var rel = w.FindRel(other.Id, p.Id);
            if (rel == null || rel.Closeness < 35) continue;
            RelationshipSystem.AddMemory(ctx, other, "death",
                $"{p.FirstName} died when I was {other.Age(ctx.Year)}", -Math.Min(60, rel.Closeness * 0.5), mentionId: p.Id);
            other.Happiness -= rel.Closeness * 0.2;
        }

        int? spouseId = p.PartnerStatus == PartnerStatus.Married ? p.PartnerId : null;
        if (p.PartnerId is { } pid)
        {
            var partner = w.Get(pid);
            partner.PartnerId = null;
            partner.PartnerStatus = PartnerStatus.None;
            partner.Flags.Add("widowed");
            if (!partner.ExPartnerIds.Contains(p.Id)) partner.ExPartnerIds.Add(p.Id);
        }
        foreach (var fid in p.FriendIds) w.Get(fid).FriendIds.Remove(p.Id);

        Inherit(ctx, p, spouseId);
    }

    // --- Wills and inheritance -------------------------------------------------------------

    /// <summary>Old NPCs sometimes write a will that favours or cuts out a child.</summary>
    public static void UpdateWill(SimContext ctx, Person p)
    {
        if (p.Id == ctx.World.PlayerId || p.Age(ctx.Year) < 65 || p.Flags.Contains("will_written")) return;
        if (!ctx.Rng.Chance(0.12)) return;
        p.Flags.Add("will_written");
        var kids = Kinship.Children(ctx.World, p).Where(k => k.IsAlive).ToList();
        if (kids.Count < 2) return;
        var byOpinion = kids.OrderByDescending(k => ctx.World.Opinion(p.Id, k.Id)).ToList();
        double best = ctx.World.Opinion(p.Id, byOpinion[0].Id), worst = ctx.World.Opinion(p.Id, byOpinion[^1].Id);
        if (best - worst > 35) p.WillFavoriteId = byOpinion[0].Id;
        if (worst < -40 && ctx.Rng.Chance(0.5)) p.Disinherited.Add(byOpinion[^1].Id);
    }

    public static void Inherit(SimContext ctx, Person dead, int? spouseId)
    {
        var w = ctx.World;
        double estate = EconomySystem.NetWorth(ctx, dead);
        dead.Money = 0;
        dead.OwnsHome = false;
        if (estate <= 0)
        {
            if (estate < -ctx.Nominal(20000) && dead.InFamily)
                w.Log($"{dead.FirstName} left {EconomySystem.Format(ctx, -estate)} of debt behind.", 1, "economy", dead.Id);
            return;
        }

        var shares = new Dictionary<int, double>();
        var spouse = w.TryGet(spouseId) is { IsAlive: true } s ? s : null;
        var kids = Kinship.Children(w, dead).Where(k => k.IsAlive && !dead.Disinherited.Contains(k.Id)).ToList();
        double rest = estate;
        if (spouse != null)
        {
            double share = kids.Count > 0 ? estate * ctx.Country.SpouseInheritanceShare : estate;
            shares[spouse.Id] = share;
            rest -= share;
        }
        if (rest > 0)
        {
            if (kids.Count > 0)
            {
                double units = kids.Sum(k => k.Id == dead.WillFavoriteId ? 2.0 : 1.0);
                foreach (var k in kids) shares[k.Id] = rest * (k.Id == dead.WillFavoriteId ? 2.0 : 1.0) / units;
            }
            else
            {
                var heirs = Kinship.Parents(w, dead).Where(x => x.IsAlive).ToList();
                if (heirs.Count == 0) heirs = Kinship.Siblings(w, dead).Where(x => x.IsAlive).ToList();
                if (heirs.Count > 0) foreach (var h in heirs) shares[h.Id] = rest / heirs.Count;
                else if (spouse == null && dead.InFamily)
                    w.Log($"{Kinship.Genitive(dead.FirstName)} estate of {EconomySystem.Format(ctx, rest)} went to the state.", 1, "economy", dead.Id);
            }
        }

        foreach (var (id, amount) in shares)
        {
            var heir = w.Get(id);
            heir.Money += amount;
            if (heir.InFamily && amount > ctx.Nominal(10000))
                w.Log($"{heir.FirstName} inherited {EconomySystem.Format(ctx, amount)} from {dead.FirstName}.", ctx.Importance(false, heir), "economy", id, dead.Id);
        }

        // Unequal wills breed resentment among siblings.
        var allKids = Kinship.Children(w, dead).Where(k => k.IsAlive).ToList();
        if (dead.WillFavoriteId is { } fav && allKids.Any(k => k.Id == fav))
        {
            var favorite = w.Get(fav);
            w.Log($"{Kinship.Genitive(dead.FirstName)} will favoured {favorite.FirstName} over the other children.", 3, "inheritance",
                allKids.Select(k => k.Id).Append(dead.Id).ToArray());
            foreach (var k in allKids.Where(k => k.Id != fav))
            {
                RelationshipSystem.AddMemory(ctx, k, "unfair_will", $"{favorite.FirstName} got more than me when {dead.FirstName} died", -30, fav);
                w.Rel(k.Id, fav)[RelDim.Envy] += 25;
            }
        }
        foreach (var k in allKids.Where(k => dead.Disinherited.Contains(k.Id)))
        {
            w.Log($"{k.FirstName} was disinherited by {dead.FirstName}.", 3, "inheritance", k.Id, dead.Id);
            RelationshipSystem.AddMemory(ctx, k, "disinherited", $"{dead.FirstName} disinherited me", -50, dead.Id);
            foreach (var sib in allKids.Where(s => s.Id != k.Id))
                w.Rel(k.Id, sib.Id)[RelDim.Envy] += 30;
        }
    }
}
