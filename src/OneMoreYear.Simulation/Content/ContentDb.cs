using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Content;

/// <summary>All data-driven game content: traits, countries, occupations and events.</summary>
public sealed class ContentDb
{
    public Dictionary<string, TraitDef> Traits { get; } = new();
    public Dictionary<string, CountryDef> Countries { get; } = new();
    public List<OccupationDef> Occupations { get; } = new();
    public Dictionary<string, ProgrammeDef> Programmes { get; } = new();
    public Dictionary<string, CrimeDef> Crimes { get; } = new();
    public List<ScenarioDef> Scenarios { get; } = new();
    public Dictionary<string, EmployerNamesDef> Employers { get; } = new();
    public Dictionary<string, NamesDef> Names { get; } = new();
    public Dictionary<string, AilmentDef> Ailments { get; } = new();
    public Dictionary<string, EventDef> Events { get; } = new();
    public List<EventDef> RandomEvents { get; } = new();

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static ContentDb? _embedded;

    /// <summary>The content shipped inside the simulation assembly (cached).</summary>
    public static ContentDb Embedded => _embedded ??= LoadEmbedded();

    public static ContentDb LoadEmbedded()
    {
        var asm = typeof(ContentDb).Assembly;
        var files = new List<(string Path, string Json)>();
        foreach (var name in asm.GetManifestResourceNames().OrderBy(n => n, StringComparer.Ordinal))
        {
            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            using var stream = asm.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            files.Add((name.Replace('\\', '/'), reader.ReadToEnd()));
        }
        return Load(files);
    }

    /// <summary>Loads content from (path, json) pairs. The path decides what kind of content it is.</summary>
    public static ContentDb Load(IEnumerable<(string Path, string Json)> files)
    {
        var db = new ContentDb();
        var patches = new List<EventPatchDef>();
        foreach (var (path, json) in files)
        {
            try
            {
                if (path.Contains("content/events/"))
                {
                    foreach (var e in Deserialize<List<EventDef>>(json)) db.Events[e.Id] = e;
                }
                else if (path.Contains("content/names/"))
                {
                    var n = Deserialize<NamesDef>(json);
                    db.Names[n.Country] = n;
                }
                else if (path.Contains("content/employers/"))
                {
                    var e = Deserialize<EmployerNamesDef>(json);
                    db.Employers[e.Country] = e;
                }
                else if (path.Contains("content/countries/"))
                {
                    var c = Deserialize<CountryDef>(json);
                    db.Countries[c.Id] = c;
                }
                else if (path.EndsWith("traits.json"))
                {
                    foreach (var t in Deserialize<List<TraitDef>>(json)) db.Traits[t.Id] = t;
                }
                else if (path.EndsWith("crimes.json"))
                {
                    foreach (var c in Deserialize<List<CrimeDef>>(json)) db.Crimes[c.Id] = c;
                }
                else if (path.EndsWith("education.json"))
                {
                    foreach (var p in Deserialize<List<ProgrammeDef>>(json)) db.Programmes[p.Id] = p;
                }
                else if (path.EndsWith("insights.json"))
                {
                    patches.AddRange(Deserialize<List<EventPatchDef>>(json));
                }
                else if (path.EndsWith("ailments.json"))
                {
                    foreach (var a in Deserialize<List<AilmentDef>>(json)) db.Ailments[a.Id] = a;
                }
                else if (path.EndsWith("scenarios.json"))
                {
                    db.Scenarios.AddRange(Deserialize<List<ScenarioDef>>(json));
                }
                else if (path.EndsWith("occupations.json"))
                {
                    db.Occupations.AddRange(Deserialize<List<OccupationDef>>(json));
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Could not read content file {path}: {ex.Message}", ex);
            }
        }
        // Passive checks and trait choices live in their own file and are added to the events here.
        foreach (var patch in patches)
        {
            if (!db.Events.TryGetValue(patch.Event, out var target)) throw new InvalidDataException($"insights.json: unknown event {patch.Event}");
            target.Insights.AddRange(patch.Insights);
            target.Choices.AddRange(patch.Choices);
        }
        db.RandomEvents.AddRange(db.Events.Values.Where(e => e.Trigger == "random").OrderBy(e => e.Id, StringComparer.Ordinal));
        return db;
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, ContentJsonOptions) ?? throw new InvalidDataException("Empty content file");

    /// <summary>Content is read strictly: a misspelt field is an error, not silently ignored. (Saves stay lenient.)</summary>
    private static readonly JsonSerializerOptions ContentJsonOptions = new(JsonOptions) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    public OccupationDef? Occupation(string? id) => id == null ? null : Occupations.FirstOrDefault(o => o.Id == id);

    public ProgrammeDef? Programme(string? id) => id != null && Programmes.TryGetValue(id, out var p) ? p : null;

    public double TraitModifier(Person p, string key)
    {
        double sum = 0;
        foreach (var t in p.Traits)
            if (Traits.TryGetValue(t, out var def) && def.Modifiers.TryGetValue(key, out var v)) sum += v;
        return sum;
    }

    /// <summary>Checks references between content files. Returns a list of problems (empty = OK).</summary>
    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Traits.Count == 0) errors.Add("No traits loaded.");
        if (Countries.Count == 0) errors.Add("No countries loaded.");
        if (Occupations.Count == 0) errors.Add("No occupations loaded.");
        foreach (var t in Traits.Values)
            if (t.Opposite != null && !Traits.ContainsKey(t.Opposite)) errors.Add($"Trait {t.Id}: unknown opposite {t.Opposite}");
        foreach (var o in Occupations)
        {
            if (o.Levels.Count == 0) errors.Add($"Occupation {o.Id} has no levels.");
            if (!o.Levels.Any(l => l.Entry)) errors.Add($"Occupation {o.Id} has no entry level.");
            foreach (var t in o.TraitAffinity.Keys)
                if (!Traits.ContainsKey(t)) errors.Add($"Occupation {o.Id}: unknown trait {t}");
            foreach (var l in o.Levels)
                foreach (var d in l.RequiresDegree ?? new())
                    if (!Programmes.ContainsKey(d)) errors.Add($"Occupation {o.Id}: unknown degree {d}");
        }
        if (Programmes.Count == 0) errors.Add("No education programmes loaded.");
        foreach (var p in Programmes.Values)
            foreach (var o in p.LeadsTo)
                if (Occupation(o) == null) errors.Add($"Programme {p.Id}: unknown occupation {o}");
        foreach (var s in Scenarios)
            foreach (var t in new[] { s.Player, s.Parents, s.Father, s.Mother, s.Grandfather })
                foreach (var trait in t?.Traits ?? new())
                    if (!Traits.ContainsKey(trait)) errors.Add($"Scenario {s.Id}: unknown trait {trait}");
        foreach (var e in Events.Values)
            foreach (var c in e.Content.Where(c => ContentCategories.All.All(x => x.Id != c)))
                errors.Add($"Event {e.Id}: unknown content category {c}");
        // Era text, [[year: before || after]], must be well formed.
        foreach (var e in Events.Values)
        {
            var texts = new List<string?> { e.Title, e.Text };
            foreach (var c in e.Choices) texts.AddRange(new[] { c.Text, c.Hint, c.Result, c.Success?.Text, c.Failure?.Text });
            foreach (var t in texts.OfType<string>())
                if (Systems.TextFormatter.ByEra(2000, t) is var resolved && (resolved.Contains("[[") || resolved.Contains("]]") || resolved.Contains("||")))
                    errors.Add($"Event {e.Id}: broken era text in \"{t}\"");
        }
        foreach (var e in Events.Values)
        {
            if (e.Choices.Count == 0 && e.DynamicChoices == null) errors.Add($"Event {e.Id} has no choices.");
            foreach (var i in e.Insights)
                if (i.Trait != null ? !Traits.ContainsKey(i.Trait) : i.Attribute is not ("smarts" or "looks" or "fitness"))
                    errors.Add($"Event {e.Id}: insight needs a known trait or attribute");
            foreach (var c in e.Choices.Where(c => c.Trait != null && !Traits.ContainsKey(c.Trait)))
                errors.Add($"Event {e.Id}: unknown choice trait {c.Trait}");
            if (e.Choices.Count > 0 && e.Choices.All(c => c.Trait != null)) errors.Add($"Event {e.Id}: every choice needs a trait – someone could have none");
            CheckConditions(e.Id, e.Conditions, errors);
            CheckConditions(e.Id, e.Target?.Conditions, errors);
            CheckConditions(e.Id, e.Other?.Conditions, errors);
            foreach (var c in e.Choices)
            {
                CheckConditions(e.Id, c.Requires, errors);
                foreach (var t in c.ChanceTraits.Keys)
                    if (!Traits.ContainsKey(t)) errors.Add($"Event {e.Id}: unknown trait {t}");
                var all = c.Effects.Concat(c.Success?.Effects ?? new()).Concat(c.Failure?.Effects ?? new());
                foreach (var eff in all)
                {
                    if (!Systems.EffectApplier.KnownTypes.Contains(eff.Type)) errors.Add($"Event {e.Id}: unknown effect type '{eff.Type}'");
                    if (eff.Trait != null && !Traits.ContainsKey(eff.Trait)) errors.Add($"Event {e.Id}: unknown trait {eff.Trait}");
                    // Design rule: abusing a child is never something the player can choose to do.
                    if (eff.Type == "crime" && eff.Kind is "child_abuse" or "sexual_assault") errors.Add($"Event {e.Id}: {eff.Kind} can never be a player choice");
                    if (eff.Var != null && !e.Vars.ContainsKey(eff.Var)) errors.Add($"Event {e.Id}: unknown variable {eff.Var}");
                    if ((eff.Who == "target" || eff.To == "target") && e.Target == null && e.Trigger != "situation") errors.Add($"Event {e.Id}: effect uses target but the event has none");
                    if ((eff.Who == "other" || eff.To == "other") && e.Other == null && e.Trigger != "situation") errors.Add($"Event {e.Id}: effect uses other but the event has none");
                }
                if (c.Chance != null && c.Success == null) errors.Add($"Event {e.Id}: choice with chance has no success outcome");
            }
        }
        return errors;
    }

    private void CheckConditions(string eventId, ConditionDef? c, List<string> errors)
    {
        if (c == null) return;
        foreach (var t in (c.TraitsAny ?? new()).Concat(c.TraitsNone ?? new()))
            if (!Traits.ContainsKey(t)) errors.Add($"Event {eventId}: unknown trait {t}");
    }
}
