using System.Text.Json.Serialization;

namespace OneMoreYear.Simulation.Model;

/// <summary>
/// A simulated person. Every person is an independent actor with their own state; relationships
/// to others live in <see cref="World"/>. Ids are stable for the lifetime of a save.
/// </summary>
public sealed class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string BirthLastName { get; set; } = "";
    public Sex Sex { get; set; }
    public int BirthYear { get; set; }
    public int? DeathYear { get; set; }
    public string? CauseOfDeath { get; set; }

    // Family links. Legal parents are used for the family tree; a differing biological
    // father is a secret until revealed.
    public List<int> ParentIds { get; set; } = new();
    public int? BiologicalFatherId { get; set; }
    public bool IsAdopted { get; set; }
    public List<int> ChildIds { get; set; } = new();
    public int? PartnerId { get; set; }
    public PartnerStatus PartnerStatus { get; set; }
    public int PartnerSinceYear { get; set; }
    public List<int> ExPartnerIds { get; set; } = new();
    /// <summary>The most recent break-up: when, with whom, and whether the other person left.</summary>
    public int? LastSplitYear { get; set; }
    public int? LastSplitWithId { get; set; }
    public bool LastSplitByThem { get; set; }
    /// <summary>A baby on the way (born next year), or an adoption being processed.</summary>
    public ExpectedChild? Expecting { get; set; }
    public List<int> FriendIds { get; set; } = new();
    /// <summary>People the player knows through school, work or friends (only kept for the player).</summary>
    public List<Acquaintance> Acquaintances { get; set; } = new();

    /// <summary>Generation relative to the founding family (founders' children = 1).</summary>
    public int Generation { get; set; }
    /// <summary>True for people who belong to the played family (blood or married in).</summary>
    public bool InFamily { get; set; }
    /// <summary>True for founders and everyone descended from them (not married-in partners).</summary>
    public bool IsBlood { get; set; }
    public bool AttractedToSameSex { get; set; }

    // Personality
    public List<string> Traits { get; set; } = new();

    // Attributes (0–100). Partly inherited; they shape school, work and love.
    public double Smarts { get; set; } = 50;
    public double Looks { get; set; } = 50;
    public double Fitness { get; set; } = 50;

    // Appearance
    public int HeightCm { get; set; }
    public string Build { get; set; } = "";
    public string HairColor { get; set; } = "";
    public string EyeColor { get; set; } = "";
    /// <summary>Facial genes; created on first use (Systems.Faces).</summary>
    public Face? Face { get; set; }

    // State
    public double Health { get; set; } = 90;
    public double Happiness { get; set; } = 60;
    public EducationLevel Education { get; set; }
    public Activity Activity { get; set; }
    public int StudyYearsLeft { get; set; }
    public EducationLevel? StudyingFor { get; set; }
    /// <summary>The programme currently being studied.</summary>
    public string? ProgrammeId { get; set; }
    /// <summary>Completed programmes (ids from education content).</summary>
    public List<string> Degrees { get; set; } = new();
    /// <summary>School results 0–100.</summary>
    public double Grades { get; set; } = 50;
    public string? OccupationId { get; set; }
    public int OccupationLevel { get; set; }
    /// <summary>The family's heritage (content/names): decides names, and looks for people with no parents in the world.</summary>
    public string Heritage { get; set; } = "";
    /// <summary>Where the person works (a fictional company, hospital, school ...).</summary>
    public string? Employer { get; set; }
    public double Performance { get; set; } = 50;
    /// <summary>How hard the person works or studies: -1 takes it easy, 0 does the job, 1 gives it everything (the player chooses).</summary>
    public int Effort { get; set; }
    public int YearsInJob { get; set; }
    /// <summary>Yearly gross income in 2020-kronor (price level adjusted when displayed).</summary>
    public double Income { get; set; }
    /// <summary>Savings (negative = debt), in nominal kronor.</summary>
    public double Money { get; set; }
    /// <summary>Highest net worth reached, nominal kronor.</summary>
    public double PeakNetWorth { get; set; }
    /// <summary>Highest and lowest net worth in reference money (Swedish kronor of 2020), comparable across eras and countries.</summary>
    public double PeakRefWorth { get; set; }
    public double LowRefWorth { get; set; }
    /// <summary>Every occupation the person has had (ids), for family achievements like three doctors in a row.</summary>
    public List<string> Jobs { get; set; } = new();
    /// <summary>The life dream the player chose for this person (docs/endgame.md), and how it went.</summary>
    public string? Dream { get; set; }
    public DreamState DreamState { get; set; }
    public int DreamYear { get; set; }
    /// <summary>The parent whose unfinished dream this person took over, if any.</summary>
    public int? DreamFromId { get; set; }
    /// <summary>The year the dream was last offered (so it is offered once per person).</summary>
    public int? DreamOffered { get; set; }
    public bool OwnsHome { get; set; }
    /// <summary>Market value of the home this person holds (0 when living in a partner's home), nominal.</summary>
    public double HomeValue { get; set; }
    /// <summary>What is left of the loan on the home, and what it was at the start (for amortization).</summary>
    public double Mortgage { get; set; }
    public double MortgageStart { get; set; }
    /// <summary>Savings in index funds, and in single company shares (riskier), nominal.</summary>
    public double Funds { get; set; }
    public double Stocks { get; set; }
    /// <summary>The player's own investments, one per fund or company (others keep the simple Funds and Stocks).</summary>
    public List<Holding> Holdings { get; set; } = new();
    /// <summary>The kind of home (country homeTypes); null for the usual home of the city.</summary>
    public string? HomeType { get; set; }
    /// <summary>Market value of a summer cottage, nominal (0 = none).</summary>
    public double CottageValue { get; set; }
    /// <summary>Which city the person lives in (country content).</summary>
    public string? CityId { get; set; }
    /// <summary>Lives in another country than the one the story is in (set when the player emigrates and they stay); null = here.</summary>
    public string? Abroad { get; set; }
    /// <summary>The country an emigrant came from (null for those who never left, or who came back).</summary>
    public string? Homeland { get; set; }
    /// <summary>Still living in the parents' home.</summary>
    public bool LivesWithParents { get; set; }
    /// <summary>Shares a rented flat with others (cheaper than renting alone).</summary>
    public bool SharesFlat { get; set; }
    /// <summary>A child who is favoured in this person's will (gets a larger share).</summary>
    public int? WillFavoriteId { get; set; }
    public List<int> Disinherited { get; set; } = new();

    /// <summary>Convictions: what, when and the sentence.</summary>
    public List<CrimeRecord> CriminalRecord { get; set; } = new();
    public int PrisonYearsLeft { get; set; }

    /// <summary>"alcohol", "drugs" or "gambling" while an addiction is active.</summary>
    public string? Addiction { get; set; }
    public int AddictionSince { get; set; }

    public List<Memory> Memories { get; set; } = new();
    /// <summary>Sorted, so a saved and reloaded game continues exactly the same way.</summary>
    public SortedSet<string> Flags { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Lasting health states (content/ailments.json) and the year each began.</summary>
    public SortedDictionary<string, int> Ailments { get; set; } = new(StringComparer.Ordinal);

    [JsonIgnore] public bool IsAlive => DeathYear is null;
    [JsonIgnore] public string FullName => $"{FirstName} {LastName}";

    public int Age(int year) => (DeathYear ?? year) - BirthYear;

    public LifePhase Phase(int year)
    {
        int age = Age(year);
        if (age < 13) return LifePhase.Childhood;
        if (age < 18) return LifePhase.Teen;
        if (age < 65) return LifePhase.Adult;
        return LifePhase.Senior;
    }

    public bool HasTrait(string id) => Traits.Contains(id);
}

/// <summary>Someone known through a context: a classmate, colleague, boss or a friend's friend.</summary>
public sealed class Acquaintance
{
    public int Id { get; set; }
    /// <summary>classmate, colleague, boss or friend_of_friend.</summary>
    public string Kind { get; set; } = "";
    public int SinceYear { get; set; }
    /// <summary>False once you have left the school or job ("old classmate").</summary>
    public bool Current { get; set; } = true;
    /// <summary>For friend_of_friend: the friend who introduced you.</summary>
    public int? ViaId { get; set; }
}

/// <summary>A child who arrives the year after the decision: pregnancy or adoption.</summary>
public sealed class ExpectedChild
{
    public int ParentAId { get; set; }
    public int? ParentBId { get; set; }
    public int DueYear { get; set; }
    public bool Adoption { get; set; }
}

public sealed class CrimeRecord
{
    public int Year { get; set; }
    public string CrimeId { get; set; } = "";
    /// <summary>"3 years in prison", "a fine of 5,000 kr" ...</summary>
    public string Sentence { get; set; } = "";
}

/// <summary>Money in one fund or company: what was paid in (nominal) and what it is worth now.</summary>
public sealed class Holding
{
    public string AssetId { get; set; } = "";
    public double Invested { get; set; }
    public double Value { get; set; }
    public int SinceYear { get; set; }
    /// <summary>Last year's return (0.12 = +12 %) and dividend paid out (nominal), for the money tab.</summary>
    public double LastReturn { get; set; }
    public double LastDividend { get; set; }
}

public enum DreamState { None, Active, Fulfilled, Failed }
