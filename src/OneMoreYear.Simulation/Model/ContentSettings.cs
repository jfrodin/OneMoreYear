namespace OneMoreYear.Simulation.Model;

/// <summary>How much of a dark theme the player wants to see.</summary>
public enum ContentLevel
{
    /// <summary>It can happen, and the player gets events and choices about it.</summary>
    On,
    /// <summary>It can happen to others, off-screen: a line in the chronicle, no events or choices.</summary>
    Mentioned,
    /// <summary>It never happens.</summary>
    Off,
}

/// <summary>The dark themes a player can turn down (docs/design-decisions.md). Stored per save.</summary>
public static class ContentCategories
{
    public const string SexualAbuse = "sexual_abuse";
    public const string Violence = "violence";
    public const string Murder = "murder";
    public const string Addiction = "addiction";
    public const string Infidelity = "infidelity";
    public const string MentalHealth = "mental_health";
    public const string SexualViolence = "sexual_violence";
    public const string Suicide = "suicide";

    public static readonly IReadOnlyList<(string Id, string Name, string Description)> All = new[]
    {
        (SexualAbuse, "Sexual abuse of children", "Abuse of children as a family secret – never shown, only its consequences."),
        (SexualViolence, "Sexual violence", "Assault and harassment between adults – you are only ever the survivor or next of kin. Never shown."),
        (Violence, "Violence", "Violence at home, assault, robbery and fights."),
        (Murder, "Murder", "Killing someone, and murders being solved."),
        (Addiction, "Addiction", "Alcohol, drugs and gambling taking over a life."),
        (Infidelity, "Infidelity", "Affairs, betrayal and children with a hidden father."),
        (MentalHealth, "Mental illness", "Depression, anxiety, burnout and trauma – and living with them."),
        (Suicide, "Suicide", "Losing someone to suicide, and the darkest nights – where every choice is a way to reach out."),
    };
}
