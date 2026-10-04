using OneMoreYear.Simulation.Content;

/// <summary>--onechoice: random and milestone events with a single choice, which the player cannot really decide.</summary>
static class ChoiceReport
{
    public static void Run()
    {
        var content = ContentDb.Embedded;
        foreach (var e in content.Events.Values.Where(e => e.Trigger is "random" or "milestone" && e.Choices.Count == 1).OrderBy(e => e.Id))
            Console.WriteLine($"{e.Id,-34} {e.Title,-28} {e.Choices[0].Text}");
    }
}
