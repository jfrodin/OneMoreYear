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
}
