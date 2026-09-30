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
        foreach (var (path, json) in files)
        {
            try
            {
                if (path.Contains("content/events/"))
                {
                    foreach (var e in Deserialize<List<EventDef>>(json)) db.Events[e.Id] = e;
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
                else if (path.EndsWith("education.json"))
                {
                    foreach (var p in Deserialize<List<ProgrammeDef>>(json)) db.Programmes[p.Id] = p;
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
        db.RandomEvents.AddRange(db.Events.Values.Where(e => e.Trigger == "random").OrderBy(e => e.Id, StringComparer.Ordinal));
        return db;
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidDataException("Empty content file");

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
        foreach (var e in Events.Values)
        {
            if (e.Choices.Count == 0 && e.DynamicChoices == null) errors.Add($"Event {e.Id} has no choices.");
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
