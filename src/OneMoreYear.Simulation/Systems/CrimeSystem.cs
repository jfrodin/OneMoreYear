using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Crime and punishment. Anyone can commit a crime; personality decides how tempting and how risky
/// it is (see docs/design-decisions.md). Getting caught means a fine or prison, and the family reacts.
/// </summary>
public static class CrimeSystem
{
    /// <summary>
    /// Commits a crime and returns what happened, in the player's words when <paramref name="p"/> is the player.
    /// </summary>
    public static string Commit(SimContext ctx, Person p, CrimeDef crime, Person? victim)
    {
        var w = ctx.World;
        var rng = ctx.Rng;
        bool isPlayer = p.Id == w.PlayerId;
        string victimName = victim?.FirstName ?? "";
        var texts = new List<string>();

        // The deed.
        double gain = crime.GainMax > 0 ? rng.Range(crime.GainMin, crime.GainMax) : 0;
        if (crime.Id == "blackmail" && victim != null)
            gain = Math.Clamp(ctx.Real(Math.Max(0, victim.Money)) * 0.15, crime.GainMin, crime.GainMax);
        if (gain > 0)
        {
            double nominal = ctx.Nominal(gain);
            if (victim != null) { victim.Money -= nominal; EconomySystem.Record(ctx, victim, $"Blackmailed by {p.FirstName}", -nominal); }
            p.Money += nominal;
            EconomySystem.Record(ctx, p, $"{crime.Name}", nominal);
            texts.Add($"You get away with {EconomySystem.Format(ctx, nominal)}.");
        }

        switch (crime.Id)
        {
            case "assault" when victim != null:
                RelationshipSystem.AddMemory(ctx, victim, "assaulted", $"{p.FirstName} beat me up", -50, p.Id);
                w.Rel(victim.Id, p.Id)[RelDim.Fear] += 40;
                victim.Health = Math.Max(1, victim.Health - rng.Range(8, 25));
                texts.Add($"{victimName} ends up in hospital.");
                break;
            case "blackmail" when victim != null:
                RelationshipSystem.AddMemory(ctx, victim, "blackmailed", $"{p.FirstName} blackmailed me", -45, p.Id);
                w.Rel(victim.Id, p.Id)[RelDim.Fear] += 30;
                break;
            case "murder" when victim != null:
                LifeSystem.Die(ctx, victim, "murder");
                w.Secrets.Add(new Secret
                {
                    Id = w.Secrets.Count + 1, Kind = "murder", Year = ctx.Year,
                    SubjectId = p.Id, VictimId = victim.Id, KnownBy = new List<int> { p.Id },
                });
                texts.Add($"{victimName} is dead. Nobody saw you. You hope.");
                break;
        }

        // Guilt – unless you don't have it in you.
        if (ctx.Mod(p, "crime") <= 0 && !p.HasTrait("cruel"))
        {
            double guilt = crime.Guilt * (1 + ctx.Mod(p, "warmth") * 0.5);
            p.Happiness = Math.Max(0, p.Happiness - guilt);
            if (guilt >= 10) RelationshipSystem.AddMemory(ctx, p, "guilt", $"What I did to {(victim != null ? victimName : "get that money")}", -guilt);
        }
        p.Flags.Add($"crime_{ctx.Year}");

        // Getting caught.
        if (rng.Chance(CatchChance(ctx, p, crime, victim)))
        {
            texts.Add(Arrest(ctx, p, crime, victim, isPlayer));
            return string.Join(" ", texts);
        }
        if (isPlayer)
        {
            if (crime.Id != "murder") w.Log($"{p.FirstName} {Did(crime, victim)} and got away with it.", 2, "crime", p.Id);
            texts.Add("Nobody comes knocking. This time.");
        }
        return string.Join(" ", texts);
    }

    public static double CatchChance(SimContext ctx, Person p, CrimeDef crime, Person? victim)
    {
        double chance = crime.CatchChance
                        * (1 - Math.Clamp(ctx.Mod(p, "crime") * 0.3 + ctx.Mod(p, "dishonesty") * 0.15, 0, 0.6))
                        * (1 + ctx.Mod(p, "risk") * 0.1);
        // Doing it again and again draws attention.
        chance += p.Flags.Count(f => f.StartsWith("crime_") && int.TryParse(f[6..], out var y) && ctx.Year - y <= 3) * 0.05;
        // A brave or manipulative victim goes to the police when blackmailed.
        if (crime.Id == "blackmail" && victim != null && (victim.HasTrait("brave") || victim.HasTrait("manipulative"))) chance += 0.3;
        return Math.Clamp(chance, 0.05, 0.9);
    }

    private static string Did(CrimeDef crime, Person? victim) => victim != null ? $"{crime.Did} {victim.FirstName}" : crime.Did;

    /// <summary>Caught: a fine, or prison. The family finds out.</summary>
    public static string Arrest(SimContext ctx, Person p, CrimeDef crime, Person? victim, bool isPlayer)
    {
        var w = ctx.World;
        int age = p.Age(ctx.Year);
        int priors = p.CriminalRecord.Count;
        string sentence;
        if (age < 15)
        {
            sentence = "a talk with social services";
            p.Happiness -= 8;
        }
        else if (crime.PrisonMax == 0 || (priors == 0 && crime.PrisonMin == 0 && ctx.Rng.Chance(0.6)))
        {
            double fine = ctx.Nominal(crime.Fine * (1 + priors * 0.5));
            p.Money -= fine;
            EconomySystem.Record(ctx, p, $"Fine for {crime.Name.ToLowerInvariant()}", -fine);
            sentence = $"a fine of {EconomySystem.Format(ctx, fine)}";
        }
        else
        {
            int years = Math.Max(1, ctx.Rng.Range(Math.Max(1, crime.PrisonMin), Math.Max(1, crime.PrisonMax)) + Math.Min(priors, 3));
            Imprison(ctx, p, years);
            sentence = $"{years} year{(years == 1 ? "" : "s")} in prison";
        }
        p.CriminalRecord.Add(new CrimeRecord { Year = ctx.Year, CrimeId = crime.Id, Sentence = sentence });
        w.Log(victim != null
                ? $"{p.FullName} was convicted – {p.FirstName} {crime.Did} {victim.FirstName} – and got {sentence}."
                : $"{p.FullName} was convicted of {crime.Name.ToLowerInvariant()} and got {sentence}.",
            ctx.Importance(true, p), "crime", victim != null ? new[] { p.Id, victim.Id } : new[] { p.Id });

        // The family's shame.
        foreach (var relative in Kinship.Parents(w, p).Concat(Kinship.Children(w, p)).Append(w.TryGet(p.PartnerId)).OfType<Person>()
                     .Where(r => r.IsAlive && r.Age(ctx.Year) >= 8).Distinct())
            RelationshipSystem.AddMemory(ctx, relative, "convicted_relative", $"{p.FirstName} was convicted of {crime.Name.ToLowerInvariant()}",
                crime.Violent ? -30 : -18, p.Id);

        return isPlayer ? $"But the police find you. You get {sentence}." : "";
    }

    public static void Imprison(SimContext ctx, Person p, int years)
    {
        p.Activity = Activity.Prison;
        p.PrisonYearsLeft = years;
        p.OccupationId = null;
        p.Income = 0;
        p.Flags.Remove(CareerSystem.PartTimeFlag);
        p.ProgrammeId = null;
        p.StudyingFor = null;
    }

    /// <summary>Prison time, release, crimes committed by others, and old murders coming to light.</summary>
    public static void Update(SimContext ctx)
    {
        var w = ctx.World;
        int count = w.People.Count;
        for (int i = 0; i < count; i++)
        {
            var p = w.People[i];
            if (!p.IsAlive) continue;
            if (p.Activity == Activity.Prison) { PrisonYear(ctx, p); continue; }
            if (p.Id != w.PlayerId && (p.InFamily || p.OccupationId == "crime") && p.Age(ctx.Year) >= 14) NpcCrime(ctx, p);
        }

        foreach (var s in w.Secrets.Where(s => s.Kind == "murder" && !s.Revealed).ToList())
        {
            var killer = w.Get(s.SubjectId);
            if (!killer.IsAlive || killer.Activity == Activity.Prison || !ctx.Rng.Chance(0.06)) continue;
            s.Revealed = true;
            var victim = w.TryGet(s.VictimId);
            w.Log($"New evidence: the police arrest {killer.FullName} for the murder of {victim?.FullName} in {s.Year}.", 3, "crime", killer.Id, victim?.Id ?? killer.Id);
            string text = Arrest(ctx, killer, ctx.Content.Crimes["murder"], victim, killer.Id == w.PlayerId);
            if (killer.Id == w.PlayerId) EventSystem.QueueSituation(ctx, "caught_for_murder", new() { ["target"] = victim?.Id ?? killer.Id });
        }
    }

    private static void PrisonYear(SimContext ctx, Person p)
    {
        var w = ctx.World;
        p.PrisonYearsLeft--;
        p.Happiness = Math.Max(0, p.Happiness - 4);
        foreach (var kid in Kinship.Children(w, p).Where(k => k.IsAlive && k.Age(ctx.Year) is >= 4 and < 18))
            if (!kid.Memories.Any(m => m.Kind == "parent_in_prison" && m.AboutId == p.Id))
                RelationshipSystem.AddMemory(ctx, kid, "parent_in_prison", $"{p.FirstName} was in prison when I was a kid", -30, p.Id);
        if (w.TryGet(p.PartnerId) is { } partner && ctx.Mod(partner, "loyalty") < 1 && ctx.Rng.Chance(0.25))
            FamilySystem.BreakUp(ctx, partner, p);

        if (p.PrisonYearsLeft > 0) return;
        CareerSystem.BecomeJobSeeker(p, ctx);
        w.Log($"{p.FirstName} was released from prison.", ctx.Importance(false, p), "crime", p.Id);
    }

    private static void NpcCrime(SimContext ctx, Person p)
    {
        double crimeMod = ctx.Mod(p, "crime");
        if (p.OccupationId == "crime")
        {
            // Career criminals get caught now and then.
            if (ctx.Rng.Chance(0.08)) Arrest(ctx, p, ctx.Content.Crimes["drug_dealing"], null, false);
            return;
        }
        if (crimeMod <= 0 || !ctx.Rng.Chance(0.05 * crimeMod)) return;
        var options = ctx.Content.Crimes.Values.Where(c => !c.Targeted && c.MinAge <= p.Age(ctx.Year) && (!c.Violent || ctx.Happens(ContentCategories.Violence)))
            .OrderBy(c => c.Id).ToList();
        if (options.Count == 0) return;
        Commit(ctx, p, ctx.Rng.Pick(options), null);
    }

    /// <summary>Someone the player knows a secret about (for blackmail).</summary>
    public static bool HasSecretKnownTo(World w, Person player, Person other) =>
        w.Secrets.Any(s => s.SubjectId == other.Id && !s.Revealed && s.KnownBy.Contains(player.Id) && s.Kind is "affair" or "paternity" or "murder");
}
