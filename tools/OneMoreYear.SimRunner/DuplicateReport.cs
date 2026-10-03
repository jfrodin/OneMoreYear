using OneMoreYear.Simulation.Content;

/// <summary>
/// --duplicates: pairs of events that probably tell the same moment, found by the words they share
/// and overlapping ages. Candidates for a "group" (or a rewrite), not proof.
/// </summary>
static class DuplicateReport
{
    private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "about", "after", "again", "always", "another", "around", "because", "been", "before", "being", "could",
        "every", "everyone", "everything", "first", "from", "have", "into", "just", "like", "little", "long",
        "more", "never", "nobody", "nothing", "only", "other", "over", "really", "says", "should", "some",
        "someone", "something", "still", "than", "that", "their", "them", "then", "there", "they", "thing",
        "this", "time", "very", "want", "wants", "were", "what", "when", "where", "which", "while", "with",
        "would", "year", "years", "your", "yours", "today", "tonight", "name",
    };

    public static void Run(double threshold)
    {
        var content = ContentDb.Embedded;
        var events = content.Events.Values.Where(e => e.Trigger is "random" or "milestone").ToList();
        var words = events.ToDictionary(e => e.Id, e => Words(e.Title + " " + e.Text + " " + string.Join(" ", e.Choices.Select(c => c.Text))));
        var pairs = new List<(double Score, EventDef A, EventDef B)>();
        for (int i = 0; i < events.Count; i++)
        for (int j = i + 1; j < events.Count; j++)
        {
            var a = events[i];
            var b = events[j];
            if (a.Group != null && a.Group == b.Group) continue;
            if (a.Countries.Count > 0 && b.Countries.Count > 0 && !a.Countries.Intersect(b.Countries).Any()) continue;
            int aMin = a.Conditions?.MinAge ?? 0, aMax = a.Conditions?.MaxAge ?? 120, bMin = b.Conditions?.MinAge ?? 0, bMax = b.Conditions?.MaxAge ?? 120;
            if (aMax < bMin || bMax < aMin) continue;
            var wa = words[a.Id];
            var wb = words[b.Id];
            if (wa.Count == 0 || wb.Count == 0) continue;
            double score = (double)wa.Intersect(wb).Count() / Math.Min(wa.Count, wb.Count);
            if (score >= threshold && wa.Intersect(wb).Count() >= 4) pairs.Add((score, a, b));
        }
        foreach (var (score, a, b) in pairs.OrderByDescending(p => p.Score))
            Console.WriteLine($"{score:0.00}  {a.Id} \"{a.Title}\"  ~  {b.Id} \"{b.Title}\"   [{string.Join(" ", words[a.Id].Intersect(words[b.Id]).Take(8))}]");
        Console.WriteLine($"{pairs.Count} pairs at {threshold:0.00} or more.");
    }

    private static HashSet<string> Words(string text) =>
        System.Text.RegularExpressions.Regex.Matches(text.ToLowerInvariant(), "[a-z]{4,}")
            .Select(m => m.Value.TrimEnd('s')).Where(w => !Stop.Contains(w) && !Stop.Contains(w + "s") && w.Length >= 4).ToHashSet();
}
