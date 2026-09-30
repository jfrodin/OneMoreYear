namespace OneMoreYear.Simulation;

// Plain read-only data prepared for the presentation layer, so UI code never needs to know
// how the simulation computes anything.

public sealed record NewGameOptions
{
    public ulong? Seed { get; init; }
    public string CountryId { get; init; } = "sweden";
    public int StartYear { get; init; } = 1970;
}

public sealed record ChoiceView(int Index, string Text, string? Hint, int? ChancePercent, bool Available);

public sealed record EventView(int Uid, string Title, string Text, IReadOnlyList<ChoiceView> Choices,
    bool Resolved, string? OutcomeText, int? TargetId);

public sealed record ActionView(string Id, string Title, string? Hint, int? ChancePercent, bool Enabled);

public sealed record RelationView(double Closeness, double Respect, double Trust, double Attraction,
    double Fear, double Envy, double Bitterness, double Opinion, string OpinionLabel);

public sealed record MemoryView(int Year, string Text, double Impact, string? AboutName);

public sealed record PersonView
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string FirstName { get; init; } = "";
    public bool IsMale { get; init; }
    public int Age { get; init; }
    public bool Alive { get; init; }
    public int BirthYear { get; init; }
    public int? DeathYear { get; init; }
    public string? CauseOfDeath { get; init; }
    /// <summary>Relation to the current player, e.g. "Mother", "Nephew", "Friend".</summary>
    public string RoleLabel { get; init; } = "";
    public string Occupation { get; init; } = "";
    public string Education { get; init; } = "";
    public string Partner { get; init; } = "";
    public IReadOnlyList<(string Name, string Description)> Traits { get; init; } = Array.Empty<(string, string)>();
    public double Health { get; init; }
    public string HealthLabel { get; init; } = "";
    public double Happiness { get; init; }
    public string Money { get; init; } = "";
    public string Income { get; init; } = "";
    public bool OwnsHome { get; init; }
    /// <summary>How this person feels about the player (null for the player).</summary>
    public RelationView? TowardsPlayer { get; init; }
    /// <summary>How the player feels about this person (null for the player).</summary>
    public RelationView? FromPlayer { get; init; }
    public IReadOnlyList<MemoryView> Memories { get; init; } = Array.Empty<MemoryView>();
    public int Generation { get; init; }
}

public sealed record ChronicleLine(int Year, string Text, int Importance, string Category, IReadOnlyList<int> PersonIds);

public sealed record YearReport(int Year, int PlayerAge, IReadOnlyList<ChronicleLine> News, bool PlayerDied);

public sealed record HeirCandidate(int Id, string Name, string Relation, int Age, string Money, string Occupation);

public sealed record LifeSummary(string Name, int BirthYear, int DeathYear, int Age, string Cause,
    int Children, int Grandchildren, int Partners, string LastOccupation, string PeakWealth,
    IReadOnlyList<ChronicleLine> Highlights);

public sealed record FamilyStats(int Generations, int FamilyMembers, int Characters, string LargestFortune,
    string LargestFortuneOwner, int Divorces, int Affairs, int Years);

/// <summary>A person in the family tree. <paramref name="IsReference"/> marks a repeat: shown in full elsewhere.</summary>
public sealed record TreeNode(int Id, string Label, bool Alive, bool IsPlayer, bool Played, IReadOnlyList<string> Partners,
    IReadOnlyList<TreeNode> Children, bool IsReference = false);
