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
    public List<int> FriendIds { get; set; } = new();

    /// <summary>Generation relative to the founding family (founders' children = 1).</summary>
    public int Generation { get; set; }
    /// <summary>True for people who belong to the played family (blood or married in).</summary>
    public bool InFamily { get; set; }
    /// <summary>True for founders and everyone descended from them (not married-in partners).</summary>
    public bool IsBlood { get; set; }
    public bool AttractedToSameSex { get; set; }

    // Personality
    public List<string> Traits { get; set; } = new();

    // State
    public double Health { get; set; } = 90;
    public double Happiness { get; set; } = 60;
    public EducationLevel Education { get; set; }
    public Activity Activity { get; set; }
    public int StudyYearsLeft { get; set; }
    public EducationLevel? StudyingFor { get; set; }
    public string? OccupationId { get; set; }
    public int OccupationLevel { get; set; }
    public double Performance { get; set; } = 50;
    public int YearsInJob { get; set; }
    /// <summary>Yearly gross income in 2020-kronor (price level adjusted when displayed).</summary>
    public double Income { get; set; }
    /// <summary>Savings (negative = debt), in nominal kronor.</summary>
    public double Money { get; set; }
    /// <summary>Highest net worth reached, nominal kronor.</summary>
    public double PeakNetWorth { get; set; }
    public bool OwnsHome { get; set; }
    /// <summary>A child who is favoured in this person's will (gets a larger share).</summary>
    public int? WillFavoriteId { get; set; }
    public List<int> Disinherited { get; set; } = new();

    public List<Memory> Memories { get; set; } = new();
    public HashSet<string> Flags { get; set; } = new();

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
