namespace OneMoreYear.Simulation.Model;

/// <summary>
/// A structured memory of something that happened to a person. Memories that blame someone
/// keep feeding the relationship towards that person for many years, fading slowly.
/// </summary>
public sealed class Memory
{
    public int Year { get; set; }
    public string Kind { get; set; } = "";
    /// <summary>Who the holder considers responsible (or who it was about), if anyone.</summary>
    public int? AboutId { get; set; }
    public string Text { get; set; } = "";
    /// <summary>Someone the memory mentions without blaming them (e.g. a relative who died).</summary>
    public int? MentionId { get; set; }
    /// <summary>-100 (traumatic) to +100 (cherished).</summary>
    public double Impact { get; set; }
    /// <summary>1 when fresh, fades towards 0 over time.</summary>
    public double Strength { get; set; } = 1;
}

/// <summary>A fact only some people know about.</summary>
public sealed class Secret
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";
    public int Year { get; set; }
    /// <summary>The person the secret is about (e.g. the one having an affair).</summary>
    public int SubjectId { get; set; }
    /// <summary>The other party (the lover, the biological father, ...).</summary>
    public int? OtherId { get; set; }
    /// <summary>The person most hurt if it comes out (the betrayed partner, the child, ...).</summary>
    public int? VictimId { get; set; }
    /// <summary>A child connected to the secret (e.g. born from an affair).</summary>
    public int? ChildId { get; set; }
    public List<int> KnownBy { get; set; } = new();
    public bool Revealed { get; set; }
    public bool Active { get; set; } = true;
}

/// <summary>An entry in the family chronicle.</summary>
public sealed class LogEntry
{
    public int Year { get; set; }
    public string Text { get; set; } = "";
    public List<int> PersonIds { get; set; } = new();
    /// <summary>1 = minor, 2 = notable, 3 = major life event.</summary>
    public int Importance { get; set; } = 1;
    public string Category { get; set; } = "";
}
