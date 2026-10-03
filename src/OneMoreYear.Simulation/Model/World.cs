using System.Text.Json.Serialization;
using OneMoreYear.Simulation.Core;

namespace OneMoreYear.Simulation.Model;

/// <summary>
/// The complete game state. Everything needed to continue a game lives here so that it can be
/// saved as a single document. Presentation code must treat this as read-only and change it only
/// through <see cref="GameSession"/>.
/// </summary>
public sealed class World
{
    public const int CurrentSaveVersion = 2;

    public int SaveVersion { get; set; } = CurrentSaveVersion;
    public ulong Seed { get; set; }
    /// <summary>The shareable code the seed came from ("7LB2WVPK"), or null for a plain number.</summary>
    public string? SeedCode { get; set; }
    /// <summary>Dark themes the player has turned down; missing = On.</summary>
    public Dictionary<string, ContentLevel> ContentSettings { get; set; } = new();
    public string CountryId { get; set; } = "";
    public int StartYear { get; set; }
    /// <summary>How the first life started: "comfortable", "ordinary", "hard" – or null, left to chance.</summary>
    public string? StartConditions { get; set; }
    public int Year { get; set; }
    public string FamilyName { get; set; } = "";

    public int PlayerId { get; set; }
    /// <summary>Every character the player has controlled, in order.</summary>
    public List<int> PlayedIds { get; set; } = new();
    public bool GameOver { get; set; }

    /// <summary>All people ever simulated. A person's id is their index + 1.</summary>
    public List<Person> People { get; set; } = new();
    public List<Relationship> Relations { get; set; } = new();
    public List<Secret> Secrets { get; set; } = new();
    public List<LogEntry> Chronicle { get; set; } = new();
    public List<PendingEvent> PendingEvents { get; set; } = new();
    /// <summary>"eventId" → last year it fired for the current player.</summary>
    public Dictionary<string, int> EventHistory { get; set; } = new();

    /// <summary>Where the player's money came from and went, this year and last year.</summary>
    public List<LedgerLine> Ledger { get; set; } = new();

    public int ActionPoints { get; set; }
    public List<string> ActionsThisYear { get; set; } = new();
    /// <summary>Things that happened once and count for achievements (set by events and systems).</summary>
    public SortedSet<string> Feats { get; set; } = new(StringComparer.Ordinal);
    /// <summary>What the family has become known for (ReputationSystem): "learning", "warmth" ...</summary>
    public SortedSet<string> FamilyTraits { get; set; } = new(StringComparer.Ordinal);
    public List<Heirloom> Heirlooms { get; set; } = new();
    public List<Pet> Pets { get; set; } = new();
    public List<Business> Businesses { get; set; } = new();
    public List<Rental> Rentals { get; set; } = new();
    /// <summary>The player's small wish for this year (WishSystem); null when there is none.</summary>
    public Wish? Wish { get; set; }

    public SimRandom Rng { get; set; } = new();
    public int NextEventUid { get; set; } = 1;

    [JsonIgnore] public Person Player => Get(PlayerId);

    public Person Get(int id) => People[id - 1];

    public Person? TryGet(int? id) => id is { } v && v >= 1 && v <= People.Count ? People[v - 1] : null;

    public Person AddPerson(Person person)
    {
        person.Id = People.Count + 1;
        People.Add(person);
        return person;
    }

    [JsonIgnore] public IEnumerable<Person> Living => People.Where(p => p.IsAlive);

    // --- Relationships -------------------------------------------------------------------------

    [JsonIgnore] private Dictionary<long, Relationship>? _relIndex;

    private static long Key(int from, int to) => ((long)from << 32) | (uint)to;

    private Dictionary<long, Relationship> RelIndex
    {
        get
        {
            if (_relIndex == null)
            {
                _relIndex = new Dictionary<long, Relationship>(Relations.Count);
                foreach (var r in Relations) _relIndex[Key(r.FromId, r.ToId)] = r;
            }
            return _relIndex;
        }
    }

    public Relationship? FindRel(int from, int to) => RelIndex.TryGetValue(Key(from, to), out var r) ? r : null;

    public Relationship Rel(int from, int to)
    {
        if (RelIndex.TryGetValue(Key(from, to), out var r)) return r;
        r = new Relationship { FromId = from, ToId = to, LastContactYear = Year };
        Relations.Add(r);
        RelIndex[Key(from, to)] = r;
        return r;
    }

    public double Opinion(int from, int to) => FindRel(from, to)?.Opinion ?? -10;

    public void Log(string text, int importance, string category, params int[] people)
    {
        Chronicle.Add(new LogEntry
        {
            Year = Year,
            Text = Systems.TextFormatter.DoubleStop(text),
            Importance = importance,
            Category = category,
            PersonIds = people.Distinct().ToList()
        });
    }
}

/// <summary>An event waiting for the player's decision (or already answered this year).</summary>
public sealed class PendingEvent
{
    public int Uid { get; set; }
    public string EventId { get; set; } = "";
    public Dictionary<string, int> Roles { get; set; } = new();
    public Dictionary<string, double> Vars { get; set; } = new();
    /// <summary>Generated text for the event: {myth}, {truth}, {diary} ... (set by the system that queued it).</summary>
    public Dictionary<string, string> Words { get; set; } = new();
    public bool Resolved { get; set; }
    public int? ChosenIndex { get; set; }
    public string? OutcomeText { get; set; }
    /// <summary>Extra outcome text from effects that decide things themselves (e.g. a crime).</summary>
    public List<string> ExtraText { get; set; } = new();
    /// <summary>Generated options for events with dynamic choices (e.g. "healthcare:2" job offers).</summary>
    public List<string> Options { get; set; } = new();
}

/// <summary>One line in the player's money breakdown (nominal kronor, + in, − out).</summary>
public sealed class LedgerLine
{
    public int Year { get; set; }
    public string Label { get; set; } = "";
    public double Amount { get; set; }
}

/// <summary>Something that stays in the family and collects its history (HeirloomSystem).</summary>
public sealed class Heirloom
{
    public int Id { get; set; }
    /// <summary>The kind (content/heirlooms.json).</summary>
    public string Kind { get; set; } = "";
    /// <summary>"Erik's pocket watch": named after the first owner.</summary>
    public string Name { get; set; } = "";
    /// <summary>Who has it now; null when it is lost or sold out of the family.</summary>
    public int? OwnerId { get; set; }
    /// <summary>Promised to someone when the owner dies.</summary>
    public int? PromisedToId { get; set; }
    public int SinceYear { get; set; }
    /// <summary>The year it left the family (sold or stolen), if it did.</summary>
    public int? GoneYear { get; set; }
    public bool Sold { get; set; }
    /// <summary>Left the family at some point (and maybe came back): it no longer counts as unbroken.</summary>
    public bool Interrupted { get; set; }
    public List<HeirloomEntry> History { get; set; } = new();
}

public sealed class HeirloomEntry
{
    public int Year { get; set; }
    public string Text { get; set; } = "";
}

/// <summary>A family pet (PetSystem): it lives, ages and dies with the family.</summary>
public sealed class Pet
{
    public int Id { get; set; }
    /// <summary>dog or cat (content/pets.json).</summary>
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public int BirthYear { get; set; }
    public int? DeathYear { get; set; }
    /// <summary>Whose pet it is; it lives in that person's home.</summary>
    public int OwnerId { get; set; }
    /// <summary>playful, lazy, loyal, grumpy or anxious.</summary>
    public string Nature { get; set; } = "";
    public bool IsAlive => DeathYear == null;
}

/// <summary>A wish for one year: which one, and about whom.</summary>
public sealed class Wish
{
    public string Id { get; set; } = "";
    public int? TargetId { get; set; }
    public int Year { get; set; }
}

/// <summary>A family business (BusinessSystem). Money in reference kronor of 2020, so it survives emigration.</summary>
public sealed class Business
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public int OwnerId { get; set; }
    public int FoundedYear { get; set; }
    public double Value { get; set; }
    public double LastProfit { get; set; }
    public int? ClosedYear { get; set; }
    /// <summary>Everyone who has owned it, in order.</summary>
    public List<int> Owners { get; set; } = new();
    public bool IsOpen => ClosedYear == null;
}

/// <summary>A home bought to let (RentalSystem). Money in reference kronor of 2020, so it survives emigration.</summary>
public sealed class Rental
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    /// <summary>"two-room flat": the kind of home, without an article.</summary>
    public string Kind { get; set; } = "";
    public string City { get; set; } = "";
    public double Value { get; set; }
    public double Loan { get; set; }
    public int BoughtYear { get; set; }
    /// <summary>Last year's rent after costs and interest (can be negative).</summary>
    public double LastNet { get; set; }
    public int? SoldYear { get; set; }
    /// <summary>Everyone who has owned it, in order.</summary>
    public List<int> Owners { get; set; } = new();
    public bool IsHeld => SoldYear == null;
    public string Name => $"the {Kind} in {City}";
}
