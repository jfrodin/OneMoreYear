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
    public const int CurrentSaveVersion = 1;

    public int SaveVersion { get; set; } = CurrentSaveVersion;
    public ulong Seed { get; set; }
    public string CountryId { get; set; } = "";
    public int StartYear { get; set; }
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

    public int ActionPoints { get; set; }
    public List<string> ActionsThisYear { get; set; } = new();

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
            Text = text,
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
    public bool Resolved { get; set; }
    public int? ChosenIndex { get; set; }
    public string? OutcomeText { get; set; }
}
