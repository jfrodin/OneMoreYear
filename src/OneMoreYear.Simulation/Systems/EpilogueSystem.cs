using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// The last page (docs/endgame.md): when a family's story ends, a few sentences say what kind of family
/// it was, from what it actually did; then the lives that were played. A score is kept in the background
/// to sort the player's families, but the page itself is words.
/// </summary>
public static class EpilogueSystem
{
    public static EpilogueView Write(SimContext ctx)
    {
        var w = ctx.World;
        var played = w.PlayedIds.Select(w.Get).ToList();
        var meters = ReputationSystem.Meters(ctx);
        var sentences = new List<string>();

        // What they did for a living.
        var work = played.SelectMany(p => p.Jobs.Take(1)).GroupBy(j => j).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).FirstOrDefault();
        string trade = work != null && ctx.Content.Occupation(work.Key) is { } occ && work.Key != "crime"
            ? $"A family that mostly worked in {CareerSystem.FieldName(occ)}" : "A family that tried a little of everything";
        // Money.
        double best = played.Count > 0 ? played.Max(p => p.PeakRefWorth) : 0;
        double typical = played.Count > 0 ? played.Select(p => p.PeakRefWorth).OrderBy(v => v).ElementAt(played.Count / 2) : 0;
        string money = typical < 1_500_000 ? "never had much money"
            : typical < 8_000_000 ? "did well enough"
            : w.FamilyTraits.Contains("old_money") ? "made money and kept it" : "made a fortune";
        sentences.Add($"{trade}, and {money}.");
        // Closeness.
        sentences.Add(meters[ReputationSystem.Warmth] >= 35 || w.FamilyTraits.Contains("close_knit")
            ? "They stayed close, through all of it."
            : meters[ReputationSystem.Warmth] < 12
                ? "They drifted apart, and found each other again mostly at funerals."
                : "They loved each other in the ordinary way: with arguments, and Christmas anyway.");
        if (meters[ReputationSystem.Learning] >= 30 || w.FamilyTraits.Contains("readers")) sentences.Add("There were books in every house.");
        if (meters[ReputationSystem.Notoriety] >= 3 || w.FamilyTraits.Contains("notorious")) sentences.Add("There were things nobody talked about at dinner.");
        if (played.Any(p => p.Memories.Any(m => m.Kind == "emigrated"))) sentences.Add("Somewhere along the way, they packed everything and started again in another country.");
        int oldest = played.Count > 0 ? played.Max(p => (p.DeathYear ?? w.Year) - p.BirthYear) : 0;
        if (oldest >= 95) sentences.Add($"One of them lived to {oldest}.");
        int dreams = played.Count(p => p.DreamState == DreamState.Fulfilled);
        if (dreams > 0)
            sentences.Add(dreams == 1 ? "One of them got exactly what they dreamed of." : $"{dreams} of them got what they dreamed of.");
        if (w.Heirlooms.FirstOrDefault(h => h.OwnerId != null && !h.Interrupted) is { } kept)
            sentences.Add($"{kept.Name} is still in the family.");

        var lives = played.Select(p => new PlayedLifeView(p.FullName,
            p.IsAlive ? $"born {p.BirthYear}" : $"{p.BirthYear} to {p.DeathYear}",
            Summary(ctx, p))).ToList();

        int generations = LegacySystem.GenerationsPlayed(w);
        int score = generations * 100 + (w.Year - w.StartYear) * 2 + dreams * 40 + w.FamilyTraits.Count * 30
                    + w.People.Count(p => p.IsBlood && p.IsAlive) * 3 + (int)Math.Min(300, best / 1_000_000 * 5);
        return new EpilogueView(w.FamilyName, w.StartYear, w.Year, string.Join(" ", sentences), lives, score);
    }

    private static string Summary(SimContext ctx, Person p)
    {
        var parts = new List<string>();
        if (p.Jobs.LastOrDefault() is { } job && ctx.Content.Occupation(job) is { } occ && job != "crime")
            parts.Add($"Worked in {CareerSystem.FieldName(occ)}");
        if (p.ChildIds.Count > 0) parts.Add(p.ChildIds.Count == 1 ? "one child" : $"{p.ChildIds.Count} children");
        if (!p.IsAlive) parts.Add($"died at {p.DeathYear - p.BirthYear}");
        string line = string.Join(", ", parts);
        if (DreamSystem.Of(ctx, p) is { } dream)
            line += (line.Length > 0 ? ". " : "") + (p.DreamState == DreamState.Fulfilled ? $"Lived the dream: {dream.Name.ToLowerInvariant()}" : $"Dreamed of: {dream.Name.ToLowerInvariant()}");
        return line.Length > 0 ? TextFormatter.Capitalize(line) + "." : "";
    }
}
