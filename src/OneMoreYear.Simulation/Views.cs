namespace OneMoreYear.Simulation;

// Plain read-only data prepared for the presentation layer, so UI code never needs to know
// how the simulation computes anything.

public sealed record NewGameOptions
{
    public ulong? Seed { get; init; }
    public string CountryId { get; init; } = "sweden";
    public int StartYear { get; init; } = 1970;
    /// <summary>A test scenario id (content/scenarios.json). Uses its start year, and its seed unless Seed is set.</summary>
    public string? ScenarioId { get; init; }
}

public sealed record ChoiceView(int Index, string Text, string? Hint, int? ChancePercent, bool Available);

public sealed record EventView(int Uid, string Title, string Text, IReadOnlyList<ChoiceView> Choices,
    bool Resolved, string? OutcomeText, int? TargetId);

public sealed record ActionView(string Id, string Title, string? Hint, int? ChancePercent, bool Enabled, string Category = "life");

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
    public IReadOnlyList<(string Name, string Description, string Tone)> Traits { get; init; } = Array.Empty<(string, string, string)>();
    /// <summary>Something serious going on, e.g. "Struggling with alcohol".</summary>
    public string? Condition { get; init; }
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
    public double Smarts { get; init; }
    public double Looks { get; init; }
    public double Fitness { get; init; }
    public double Grades { get; init; }
    public string AppearanceText { get; init; } = "";
    public string Home { get; init; } = "";
    public IReadOnlyList<PersonLink> Links { get; init; } = Array.Empty<PersonLink>();
}

public sealed record PersonLink(int Id, string Relation, string Name, bool Alive);

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

public sealed record LadderStep(string Title, string Salary, string Requirement, bool IsCurrent, bool Qualified);

/// <summary>Everything the School &amp; Work tab shows.</summary>
public sealed record CareerView
{
    public string Status { get; init; } = "";
    public string EducationLevel { get; init; } = "";
    public string? Programme { get; init; }
    public string? ProgrammeDescription { get; init; }
    public int YearsLeft { get; init; }
    public double? Grades { get; init; }
    public bool PartTimeJob { get; init; }
    public IReadOnlyList<string> Degrees { get; init; } = Array.Empty<string>();
    public string? JobTitle { get; init; }
    public string? Employer { get; init; }
    public string? Field { get; init; }
    public string? Salary { get; init; }
    public int YearsInJob { get; init; }
    public double? Performance { get; init; }
    public int PromotionChancePercent { get; init; }
    public string? PromotionNote { get; init; }
    public IReadOnlyList<LadderStep> Ladder { get; init; } = Array.Empty<LadderStep>();
    public IReadOnlyList<string> CriminalRecord { get; init; } = Array.Empty<string>();
}

public sealed record LedgerView(string Label, string Amount, double Raw);

/// <summary>Everything the Money tab shows.</summary>
public sealed record MoneyView
{
    public string Money { get; init; } = "";
    public bool InDebt { get; init; }
    public string NetWorth { get; init; } = "";
    public string? Home { get; init; }
    public string YearlyIncome { get; init; } = "";
    public int SaveRatePercent { get; init; }
    public int TaxPercent { get; init; }
    public int Year { get; init; }
    public IReadOnlyList<LedgerView> ThisYear { get; init; } = Array.Empty<LedgerView>();
    public string ThisYearTotal { get; init; } = "";
    public IReadOnlyList<LedgerView> LastYear { get; init; } = Array.Empty<LedgerView>();
    public string LastYearTotal { get; init; } = "";
}

/// <summary>Everything needed to draw a person's portrait and figure. Genes are 0–1.</summary>
public sealed record PortraitView
{
    public int Id { get; init; }
    public bool Male { get; init; }
    public int Age { get; init; }
    public bool Alive { get; init; }
    public string HairColor { get; init; } = "";
    public string EyeColor { get; init; } = "";
    /// <summary>0–1: how grey / how bald at this age.</summary>
    public double Grey { get; init; }
    public double Bald { get; init; }
    /// <summary>0 = thin, 0.5 = normal, 1 = heavy (from height, weight and build).</summary>
    public double Heaviness { get; init; }
    /// <summary>-1 = miserable, 0 = neutral, 1 = happy.</summary>
    public double Mood { get; init; }
    public bool Glasses { get; init; }
    public int HeightCm { get; init; }
    public double Fitness { get; init; }
    public Model.Face Face { get; init; } = new();
}
