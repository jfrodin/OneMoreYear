using System.Text.RegularExpressions;
using OneMoreYear.Simulation.Content;

/// <summary>--lint: events a child can get whose text talks like an adult's life (driving, wine, salary).</summary>
static class ContextLint
{
    private static readonly Regex Adult = new(
        @"\b(drive|drove|driving|your car|the car keys|wine|beer|whisky|drunk|hangover|salary|payslip|mortgage|rent|colleague|your boss|your wife|your husband|your partner|your children|your kids|nurse|tax|pension|divorce)\b",
        RegexOptions.IgnoreCase);

    public static void Run()
    {
        var content = ContentDb.Embedded;
        foreach (var e in content.Events.Values.OrderBy(e => e.Id))
        {
            int min = e.Conditions?.MinAge ?? 0;
            if (min >= 16 || e.Trigger is "situation" or "action") continue;
            var texts = new List<string> { e.Text };
            foreach (var c in e.Choices)
            {
                if (c.Requires?.MinAge >= 16) continue;
                texts.Add(c.Text);
                if (c.Result != null) texts.Add(c.Result);
                if (c.Success?.Text is { } s) texts.Add(s);
                if (c.Failure?.Text is { } f) texts.Add(f);
            }
            foreach (var t in texts)
                if (Adult.Match(t) is { Success: true } m)
                    Console.WriteLine($"{e.Id,-34} {min,3}  [{m.Value}] {t[..Math.Min(t.Length, 110)]}");
        }
    }

    private static readonly Regex Family = new(@"\byour (son|daughter|children|kids|grandchildren|grandchild|wife|husband|partner)\b", RegexOptions.IgnoreCase);

    /// <summary>Events that talk about your children or partner without making sure you have them.</summary>
    public static void Family_()
    {
        Console.WriteLine("\nTexts about family the player may not have:");
        foreach (var e in ContentDb.Embedded.Events.Values.Where(e => e.Trigger is "random" or "milestone").OrderBy(e => e.Id))
        {
            var c = e.Conditions;
            var roles = new[] { e.Target?.Role, e.Other?.Role };
            foreach (var t in new[] { e.Text }.Concat(e.Texts).Concat(e.Choices.SelectMany(ch => new[] { ch.Text, ch.Result, ch.Success?.Text, ch.Failure?.Text })).OfType<string>())
            {
                if (Family.Match(t) is not { Success: true } m) continue;
                string who = m.Groups[1].Value.ToLowerInvariant();
                bool sure = who switch
                {
                    "son" or "daughter" or "children" or "kids" => c?.MinChildren > 0 || roles.Contains("child"),
                    "grandchildren" or "grandchild" => roles.Contains("grandchild"),
                    _ => c?.HasPartner == true || roles.Contains("partner"),
                };
                if (!sure) Console.WriteLine($"  {e.Id,-34} [{m.Value}] {t[..Math.Min(t.Length, 110)]}");
            }
        }
    }
}
