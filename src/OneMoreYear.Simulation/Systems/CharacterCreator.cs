using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Everything the character creator lets you decide. Left as null, chance decides as usual.
/// Money is in 2020 kronor (scaled to the country like all content money).
/// </summary>
public sealed record CharacterSpec
{
    public string? FirstName { get; init; }
    public Sex? Sex { get; init; }
    public string CountryId { get; init; } = "sweden";
    public string? CityId { get; init; }
    public int StartYear { get; init; } = 1970;
    /// <summary>The age you take over at. The years before are lived automatically.</summary>
    public int Age { get; init; }
    public string? SeedCode { get; init; }
    public List<string> Traits { get; init; } = new();
    public double? Smarts { get; init; }
    public double? Looks { get; init; }
    public double? Fitness { get; init; }
    public double? Health { get; init; }
    public double? Happiness { get; init; }
    public double? Money { get; init; }
    public bool? OwnsHome { get; init; }
    public string? Occupation { get; init; }
    public bool? Partner { get; init; }
    public int? MinChildren { get; init; }
    public string? Addiction { get; init; }
    public string? Ailment { get; init; }
    /// <summary>The family's circumstances at birth (StartChoices.Conditions).</summary>
    public string? Family { get; init; }
    /// <summary>The parents' savings at birth, each.</summary>
    public double? ParentsMoney { get; init; }
}

/// <summary>
/// The character creator: builds a one-off scenario from a <see cref="CharacterSpec"/>, so a life can
/// start anywhere, at any age, as anyone. Kept apart from the rest of the game on purpose: it is a
/// separate feature (a playtesting tool now, perhaps a small add-on later), switched on in one place.
/// </summary>
public static class CharacterCreator
{
    public static readonly string[] Addictions = { "alcohol", "drugs", "gambling" };

    public static NewGameOptions Options(CharacterSpec spec, IReadOnlyDictionary<string, ContentLevel>? content = null)
    {
        // One seed code for both, so the same creation can be started again from it.
        spec = spec with { SeedCode = string.IsNullOrWhiteSpace(spec.SeedCode) ? Core.SeedCode.Random() : spec.SeedCode.Trim() };
        return new NewGameOptions
        {
            SeedCode = spec.SeedCode,
            CountryId = spec.CountryId,
            CityId = spec.CityId,
            StartYear = spec.StartYear,
            PlayerSex = spec.Sex,
            StartConditions = spec.Family,
            ContentSettings = content,
            Custom = ToScenario(spec),
        };
    }

    public static ScenarioDef ToScenario(CharacterSpec spec)
    {
        bool needs = spec.Partner != null || spec.MinChildren > 0 || spec.Sex != null;
        return new ScenarioDef
        {
            Id = "custom",
            Name = string.IsNullOrWhiteSpace(spec.FirstName) ? "A created life" : spec.FirstName!,
            StartYear = spec.StartYear,
            Age = Math.Clamp(spec.Age, 0, 95),
            Seed = Core.SeedCode.ToSeed(spec.SeedCode ?? ""),
            Player = new ScenarioTweak
            {
                FirstName = spec.FirstName,
                Traits = spec.Traits.Count > 0 ? spec.Traits.ToList() : null,
                ExactTraits = spec.Traits.Count > 0,
                Smarts = spec.Smarts, Looks = spec.Looks, Fitness = spec.Fitness,
                Health = spec.Health, Happiness = spec.Happiness,
                Money = spec.Money, OwnsHome = spec.OwnsHome,
                Occupation = spec.Age >= 16 ? spec.Occupation : null,
                Addiction = spec.Age >= 13 ? spec.Addiction : null,
                Ailment = spec.Age >= 13 || spec.Ailment == "dementia" ? spec.Ailment : null,
            },
            Parents = spec.ParentsMoney is { } pm ? new ScenarioTweak { Money = pm } : null,
            Requires = needs
                ? new ScenarioRequirements
                {
                    Partner = spec.Age >= 18 ? spec.Partner : null,
                    MinChildren = spec.Age >= 18 ? spec.MinChildren : null,
                    Sex = spec.Sex switch { Model.Sex.Male => "male", Model.Sex.Female => "female", _ => null },
                }
                : null,
        };
    }

    public static GameSession Start(CharacterSpec spec, IReadOnlyDictionary<string, ContentLevel>? content = null) =>
        GameSession.NewGame(Options(spec, content));

    // What can be picked, for the creator's lists.
    public static IReadOnlyList<(string Id, string Name)> Traits(ContentDb? db = null) =>
        (db ?? ContentDb.Embedded).Traits.Values.OrderBy(t => t.Name).Select(t => (t.Id, t.Name)).ToList();

    public static IReadOnlyList<(string Id, string Name)> Occupations(ContentDb? db = null) =>
        (db ?? ContentDb.Embedded).Occupations.OrderBy(o => o.Name).Select(o => (o.Id, o.Name)).ToList();

    public static IReadOnlyList<(string Id, string Name)> Ailments(ContentDb? db = null) =>
        (db ?? ContentDb.Embedded).Ailments.Values.OrderBy(a => a.Name).Select(a => (a.Id, a.Name)).ToList();
}
