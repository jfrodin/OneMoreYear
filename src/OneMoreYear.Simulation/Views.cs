namespace OneMoreYear.Simulation;

// Plain read-only data prepared for the presentation layer, so UI code never needs to know
// how the simulation computes anything.

public sealed record NewGameOptions
{
    public ulong? Seed { get; init; }
    /// <summary>A shareable seed code (any text). Used when Seed is not set; a new random code when neither is.</summary>
    public string? SeedCode { get; init; }
    public string CountryId { get; init; } = "sweden";
    public int StartYear { get; init; } = 1970;
    /// <summary>A test scenario id (content/scenarios.json). Uses its start year, and its seed unless Seed is set.</summary>
    public string? ScenarioId { get; init; }
    /// <summary>Dark themes turned down for this game (see Model.ContentCategories).</summary>
    public IReadOnlyDictionary<string, Model.ContentLevel>? ContentSettings { get; init; }

    // A few optional choices about the first life. Left out, chance decides – the way the game is meant to be played.
    /// <summary>The first player's sex.</summary>
    public Model.Sex? PlayerSex { get; init; }
    /// <summary>The family's circumstances: "comfortable" (easier), "ordinary" or "hard" (harder). See Systems.StartChoices.</summary>
    public string? StartConditions { get; init; }
    /// <summary>The city the player is born in (an id from the country's cities).</summary>
    public string? CityId { get; init; }
}

/// <summary>A choice. <paramref name="Tag"/> names the trait that makes it possible ("Charming"); <paramref name="Factors"/> explains the chance.</summary>
public sealed record ChoiceView(int Index, string Text, string? Hint, int? ChancePercent, bool Available, string? Tag = null, string? Factors = null);

/// <summary>Something the player's personality lets them notice: "Paranoid" – "He turns his phone away."</summary>
public sealed record InsightView(string Label, string Text, string Tone);

public sealed record EventView(int Uid, string Title, string Text, IReadOnlyList<ChoiceView> Choices,
    bool Resolved, string? OutcomeText, int? TargetId, IReadOnlyList<InsightView>? Insights = null);

/// <summary>An action button. <paramref name="Locked"/> says why it cannot be done yet (shown greyed out).</summary>
public sealed record ActionView(string Id, string Title, string? Hint, int? ChancePercent, bool Enabled, string Category = "life", string? Locked = null);

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

public sealed record YearReport(int Year, int PlayerAge, IReadOnlyList<ChronicleLine> News, bool PlayerDied)
{
    private static readonly HashSet<string> BigCategories = new() { "family", "death", "health", "succession", "inheritance", "secret", "crime", "dark", "world" };

    /// <summary>News worth a front page: births, deaths, illness, wills, secrets, crime, history – and love that changes a life.</summary>
    public static bool IsFrontPage(ChronicleLine l) =>
        l.Importance >= 3 && (BigCategories.Contains(l.Category)
            || l.Category == "love" && (l.Text.Contains("married") || l.Text.Contains("divorc") || l.Text.Contains("broke up") || l.Text.Contains(" left ")));

    /// <summary>A year with at least one front-page story.</summary>
    public bool IsBigYear => News.Any(IsFrontPage);
}

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
    /// <summary>-1 takes it easy, 0 does the job, 1 gives it everything.</summary>
    public int Effort { get; init; }
    public bool CanChooseEffort { get; init; }
    /// <summary>"Heading towards about 68: working hard +12, ambition +15 …" – what moves performance and grades.</summary>
    public string? PerformanceNote { get; init; }
    public string? GradesNote { get; init; }
    /// <summary>The age school starts giving grades in this country.</summary>
    public int GradesFromAge { get; init; } = 14;
}

public sealed record LedgerView(string Label, string Amount, double Raw);

/// <summary>Everything the Money tab shows.</summary>
/// <summary>A fund or company that can be bought this year. Percentages are whole numbers (12 = +12 %); null when too young to say.</summary>
public sealed record AssetView(string Id, string Name, string Kind, string Description, string Risk, int? LastYearPercent, int? FiveYearPercent, int DividendPercent);

/// <summary>One of the player's holdings.</summary>
public sealed record HoldingView(string AssetId, string Name, string Kind, string Risk, string Invested, string Value, int ChangePercent, int LastYearPercent, string? Dividend, int SinceYear);

/// <summary>A kind of home in the player's city: what renting and buying it would cost, and whether it is possible.</summary>
public sealed record HomeOptionView(string TypeId, string Name, int Sleeps, bool Current,
    string? RentPerMonth, string? Price, string? OwnPerMonth, bool CanBuy, string? CannotBuyReason);

public sealed record MoneyView
{
    public string Money { get; init; } = "";
    public bool InDebt { get; init; }
    public string NetWorth { get; init; } = "";
    public string? Home { get; init; }
    /// <summary>Investments and the home, as label and amount; empty rows are left out.</summary>
    public IReadOnlyList<(string Label, string Amount)> Assets { get; init; } = Array.Empty<(string, string)>();
    /// <summary>"The stock market rose 12 % last year, homes 3 %, inflation 2 %."</summary>
    public string MarketNote { get; init; } = "";
    public string YearlyIncome { get; init; } = "";
    public int SaveRatePercent { get; init; }
    public int TaxPercent { get; init; }
    /// <summary>The share of a shortfall that welfare covers, in words ("half", "a quarter").</summary>
    public string WelfareShare { get; init; } = "half";
    public int Year { get; init; }
    public IReadOnlyList<LedgerView> ThisYear { get; init; } = Array.Empty<LedgerView>();
    public string ThisYearTotal { get; init; } = "";
    public IReadOnlyList<LedgerView> LastYear { get; init; } = Array.Empty<LedgerView>();
    public string LastYearTotal { get; init; } = "";
    /// <summary>The player's funds and shares, one row each.</summary>
    public IReadOnlyList<HoldingView> Holdings { get; init; } = Array.Empty<HoldingView>();
    public string InvestedTotal { get; init; } = "";
    /// <summary>Why the player cannot invest right now (too young, no savings), or null.</summary>
    public string? CannotInvest { get; init; }
    /// <summary>"A three-room flat in Umeå", and roughly what living there costs a month.</summary>
    public string HomeDescription { get; init; } = "";
    public string? HousingPerMonth { get; init; }
    public bool HasMortgage { get; init; }
    public string? Cottage { get; init; }
    /// <summary>Whether the player can choose a new home (an adult, not in prison or a care home).</summary>
    public bool CanMove { get; init; }
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

/// <summary>One card in the graphical family tree.</summary>
public sealed record TreePerson(int Id, string Name, string Years, bool Alive, string Relation, bool IsPlayer, bool Played, bool HalfSibling = false);

/// <summary>
/// The family around one person, generation by generation: grandparents (per parent), parents,
/// siblings (in birth order, including the person), the partner, children and grandchildren (per child).
/// </summary>
public sealed record FamilyFocusView(
    TreePerson Focus,
    IReadOnlyList<TreePerson> Parents,
    IReadOnlyDictionary<int, IReadOnlyList<TreePerson>> Grandparents,
    IReadOnlyList<TreePerson> Siblings,
    TreePerson? Partner,
    IReadOnlyList<TreePerson> Children,
    IReadOnlyDictionary<int, IReadOnlyList<TreePerson>> Grandchildren);

/// <summary>The page that opens a new decade: its name, what it was like, and the family since the last chapter.</summary>
public sealed record ChapterView(int Year, string Title, string Name, string Text, string PlayerLine, IReadOnlyList<string> FamilyLines);
