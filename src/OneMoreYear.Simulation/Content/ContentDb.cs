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
    public List<AchievementDef> Achievements { get; } = new();
    public Dictionary<string, DreamDef> Dreams { get; } = new();
    public Dictionary<string, FamilyTraitDef> FamilyTraits { get; } = new();
    public Dictionary<string, HeirloomDef> Heirlooms { get; } = new();
    public Dictionary<string, PetKindDef> PetKinds { get; } = new();
    public Dictionary<string, HobbyDef> Hobbies { get; } = new();
    public Dictionary<string, BusinessKindDef> BusinessKinds { get; } = new();
    public List<EventDef> RandomEvents { get; } = new();
    /// <summary>History and life's milestones (a first word, turning eighty): they come for sure, the year their conditions first hold.</summary>
    public List<EventDef> HistoryEvents { get; } = new();

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
                    foreach (var e in Deserialize<List<EventDef>>(json))
                    {
                        // Two events with one id: the later would silently replace the earlier.
                        if (db.Events.ContainsKey(e.Id)) throw new InvalidDataException($"Event id {e.Id} is used twice ({path}).");
                        db.Events[e.Id] = e;
                    }
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
                else if (path.EndsWith("businesses.json"))
                {
                    foreach (var b in Deserialize<List<BusinessKindDef>>(json)) db.BusinessKinds[b.Id] = b;
                }
                else if (path.EndsWith("hobbies.json"))
                {
                    foreach (var h in Deserialize<List<HobbyDef>>(json)) db.Hobbies[h.Id] = h;
                }
                else if (path.EndsWith("pets.json"))
                {
                    foreach (var k in Deserialize<List<PetKindDef>>(json)) db.PetKinds[k.Id] = k;
                }
                else if (path.EndsWith("heirlooms.json"))
                {
                    foreach (var h in Deserialize<List<HeirloomDef>>(json)) db.Heirlooms[h.Id] = h;
                }
                else if (path.EndsWith("reputation.json"))
                {
                    foreach (var t in Deserialize<List<FamilyTraitDef>>(json)) db.FamilyTraits[t.Id] = t;
                }
                else if (path.EndsWith("dreams.json"))
                {
                    foreach (var d in Deserialize<List<DreamDef>>(json)) db.Dreams[d.Id] = d;
                }
                else if (path.EndsWith("achievements.json"))
                {
                    db.Achievements.AddRange(Deserialize<List<AchievementDef>>(json));
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
        db.HistoryEvents.AddRange(db.Events.Values.Where(e => e.Trigger is "history" or "milestone").OrderBy(e => e.Id, StringComparer.Ordinal));
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
        foreach (var e in Events.Values)
            foreach (var c in e.Countries.Where(c => !Countries.ContainsKey(c)))
                errors.Add($"Event {e.Id}: unknown country {c}");
        foreach (var p in Programmes.Values)
            foreach (var c in p.Countries.Where(c => !Countries.ContainsKey(c)))
                errors.Add($"Programme {p.Id}: unknown country {c}");
        // Era text, [[year: before || after]], must be well formed.
        foreach (var e in Events.Values)
        {
            var texts = new List<string?> { e.Title, e.Text };
            foreach (var c in e.Choices) texts.AddRange(new[] { c.Text, c.Hint, c.Result, c.Success?.Text, c.Failure?.Text });
            foreach (var t in texts.OfType<string>())
                if (Systems.TextFormatter.ByEra(2000, t) is var resolved && (resolved.Contains("[[") || resolved.Contains("]]") || resolved.Contains("||")))
                    errors.Add($"Event {e.Id}: broken era text in \"{t}\"");
                else if (System.Text.RegularExpressions.Regex.IsMatch(t, @"(?i)\byour \{\w\.role\}"))
                    // {t.role} already says "your brother".
                    errors.Add($"Event {e.Id}: \"your {{t.role}}\" says your twice in \"{t}\"");
                else if (t.Contains("{[[") || t.Contains("]]}"))
                    // Era text is not a token: no curly brackets around it.
                    errors.Add($"Event {e.Id}: era text inside curly brackets in \"{t}\"");
                else if (t.Contains(" – ") || t.Contains("—"))
                    // House style: no dashes in game text (they read as machine-written). Use a full stop, comma or colon.
                    errors.Add($"Event {e.Id}: dash in \"{t}\"");
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
                foreach (var sk in c.ChanceSkills.Keys)
                    if (!Hobbies.ContainsKey(sk)) errors.Add($"Event {e.Id}: unknown skill {sk}");
                foreach (var t in c.ChanceTraits.Keys)
                    if (!Traits.ContainsKey(t)) errors.Add($"Event {e.Id}: unknown trait {t}");
                var all = c.Effects.Concat(c.Success?.Effects ?? new()).Concat(c.Failure?.Effects ?? new());
                foreach (var eff in all)
                {
                    if (!Systems.EffectApplier.KnownTypes.Contains(eff.Type)) errors.Add($"Event {e.Id}: unknown effect type '{eff.Type}'");
                    if (eff.Type == "start_business" && (eff.Kind == null || !BusinessKinds.ContainsKey(eff.Kind))) errors.Add($"Event {e.Id}: unknown business {eff.Kind}");
                    if (eff.Type is "hobby" or "skill" && (eff.Kind == null || !Hobbies.ContainsKey(eff.Kind))) errors.Add($"Event {e.Id}: unknown hobby {eff.Kind}");
                    if (eff.Type == "pet_add" && (eff.Kind == null || !PetKinds.ContainsKey(eff.Kind))) errors.Add($"Event {e.Id}: unknown pet {eff.Kind}");
                    if (eff.Type == "emigrate" && (eff.Country == null || !Countries.ContainsKey(eff.Country))) errors.Add($"Event {e.Id}: emigrate to unknown country {eff.Country}");
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
        foreach (var h in Heirlooms.Values)
            if (new[] { h.Name, h.Text }.Any(x => string.IsNullOrWhiteSpace(x) || x.Contains("–") || x.Contains("—"))) errors.Add($"Heirloom {h.Id}: missing text or a dash");
        foreach (var t in FamilyTraits.Values)
        {
            if (t.Meter is not ("learning" or "wealth" or "warmth" or "notoriety")) errors.Add($"Family trait {t.Id}: unknown meter {t.Meter}");
            if (t.Lose >= t.Gain) errors.Add($"Family trait {t.Id}: lose must be below gain");
            if (new[] { t.Name, t.Text, t.Gained, t.Lost }.Any(x => string.IsNullOrWhiteSpace(x) || x.Contains("–") || x.Contains("—"))) errors.Add($"Family trait {t.Id}: missing text or a dash");
        }
        // Dreams: known goals, traits and occupations, and no dashes.
        foreach (var d in Dreams.Values)
        {
            if (!Systems.DreamSystem.Goals.Contains(d.Goal)) errors.Add($"Dream {d.Id}: unknown goal {d.Goal}");
            if (d.Goal == "job" && Occupation(d.Param) == null) errors.Add($"Dream {d.Id}: unknown occupation {d.Param}");
            foreach (var t in d.Traits.Keys) if (!Traits.ContainsKey(t)) errors.Add($"Dream {d.Id}: unknown trait {t}");
            if (new[] { d.Name, d.Text, d.Fulfilled, d.Failed }.Any(t => string.IsNullOrWhiteSpace(t) || t.Contains("–") || t.Contains("—"))) errors.Add($"Dream {d.Id}: missing text or a dash");
        }
        // Achievements: every text has a rule and every rule a text.
        var tiers = new[] { Systems.AchievementSystem.Common, Systems.AchievementSystem.Rare, Systems.AchievementSystem.Legendary, Systems.AchievementSystem.Secret };
        foreach (var a in Achievements)
        {
            if (!Systems.AchievementSystem.RuleIds.Contains(a.Id)) errors.Add($"Achievement {a.Id}: no rule in AchievementSystem");
            if (!tiers.Contains(a.Tier)) errors.Add($"Achievement {a.Id}: unknown tier {a.Tier}");
            if (a.Tier is "rare" or "legendary" && string.IsNullOrWhiteSpace(a.Hint)) errors.Add($"Achievement {a.Id}: needs a hint");
            if (new[] { a.Name, a.Hint, a.Text }.Any(t => t.Contains("–") || t.Contains("—"))) errors.Add($"Achievement {a.Id}: dash in text");
        }
        foreach (var id in Systems.AchievementSystem.RuleIds.Where(id => Achievements.All(a => a.Id != id)))
            errors.Add($"AchievementSystem rule {id} has no text in achievements.json");
        foreach (var dup in Achievements.GroupBy(a => a.Id).Where(g => g.Count() > 1)) errors.Add($"Achievement {dup.Key} is listed twice");
        return errors;
    }

    private void CheckConditions(string eventId, ConditionDef? c, List<string> errors)
    {
        if (c == null) return;
        foreach (var t in (c.TraitsAny ?? new()).Concat(c.TraitsNone ?? new()))
            if (!Traits.ContainsKey(t)) errors.Add($"Event {eventId}: unknown trait {t}");
        foreach (var sk in (c.MinSkills?.Keys ?? Enumerable.Empty<string>()).Concat(c.Hobby != null ? new[] { c.Hobby } : Array.Empty<string>()))
            if (!Hobbies.ContainsKey(sk)) errors.Add($"Event {eventId}: unknown skill {sk}");
        foreach (var job in c.Jobs ?? new())
            if (Occupation(job) == null) errors.Add($"Event {eventId}: unknown occupation {job}");
        foreach (var tag in c.JobTags ?? new())
            if (!Occupations.Any(o => o.Tags.Contains(tag))) errors.Add($"Event {eventId}: no occupation has the tag {tag}");
    }
}
