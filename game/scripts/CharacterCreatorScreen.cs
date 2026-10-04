using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Godot;
using OneMoreYear.Simulation;
using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Model;
using OneMoreYear.Simulation.Systems;

namespace OneMoreYear.Game;

/// <summary>
/// The character creator: everything about a new life chosen by hand. A separate feature (see
/// <see cref="Features.CharacterCreator"/>); the game itself never depends on it. The last creation
/// is remembered so it can be started again or adjusted.
/// </summary>
public partial class CharacterCreatorScreen : Control
{
    private const string LastPath = "user://last_character.json";

    private Main _main = null!;
    private readonly List<CountryDef> _countries = ContentDb.Embedded.Countries.Values.OrderBy(c => c.Name).ToList();
    private readonly IReadOnlyList<(string Id, string Name)> _occupations = CharacterCreator.Occupations();
    private readonly IReadOnlyList<(string Id, string Name)> _ailments = CharacterCreator.Ailments();
    private readonly HashSet<string> _traits = new();

    private LineEdit _name = null!, _seed = null!, _money = null!, _parentsMoney = null!;
    private OptionButton _sex = null!, _country = null!, _city = null!, _family = null!, _home = null!, _job = null!,
        _partner = null!, _addiction = null!, _ailment = null!;
    private SpinBox _year = null!, _age = null!, _children = null!;
    private readonly Dictionary<string, SpinBox> _stats = new();
    private Label _status = null!;
    private Button _start = null!;

    public void Init(Main main) => _main = main;

    public override void _Ready()
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(scroll);
        var center = new CenterContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        scroll.AddChild(center);
        var col = Ui.VBox(12);
        col.CustomMinimumSize = new Vector2(760, 0);
        center.AddChild(col);

        var title = Ui.Label("Create a character", 40, UiTheme.Accent);
        title.AddThemeFontOverride("font", UiTheme.Heading);
        col.AddChild(title);
        col.AddChild(Ui.Label("Choose as much or as little as you like. Anything left on chance is decided as usual. " +
                              "The years before the age you choose are lived for you, then you take over.", 16, UiTheme.Muted, wrap: true));

        // Who and where.
        var who = Section(col, "Who and where");
        _name = new LineEdit { PlaceholderText = "Chance", MaxLength = 24 };
        Row(who, "First name", _name);
        _sex = Options(new[] { "Chance", "Male", "Female" });
        Row(who, "Sex", _sex);
        _country = Options(_countries.Select(c => c.Name));
        _country.ItemSelected += _ => FillCities();
        Row(who, "Country", _country);
        _city = new OptionButton();
        Row(who, "City", _city);
        _year = Spin(1950, 2060, 1975);
        Row(who, "Born in", _year);
        _age = Spin(0, 95, 0);
        Row(who, "Take over at age", _age);
        _seed = new LineEdit { PlaceholderText = "Random", MaxLength = 24 };
        Row(who, "Seed", _seed);

        // Family.
        var family = Section(col, "Family");
        _family = Options(new[] { "Chance" }.Concat(StartChoices.Conditions.Select(c => c.Name)));
        Row(family, "Circumstances", _family);
        _parentsMoney = Amount();
        Row(family, "Parents' savings, each", _parentsMoney);
        _partner = Options(new[] { "Chance", "Yes", "No" });
        Row(family, "A partner (18+)", _partner);
        _children = Spin(0, 6, 0);
        Row(family, "At least this many children (18+)", _children);

        // Body and mind.
        var body = Section(col, "Body and mind (0 = chance)");
        foreach (var stat in new[] { "Smarts", "Looks", "Fitness", "Health", "Happiness" })
        {
            _stats[stat] = Spin(0, 100, 0);
            Row(body, stat, _stats[stat]);
        }
        _addiction = Options(new[] { "None" }.Concat(CharacterCreator.Addictions.Select(Capitalize)));
        Row(body, "Addiction (13+)", _addiction);
        _ailment = Options(new[] { "None" }.Concat(_ailments.Select(a => a.Name)));
        Row(body, "Ailment", _ailment);

        // Work and money, when you take over.
        var work = Section(col, "Work and money, when you take over");
        _job = Options(new[] { "Chance" }.Concat(_occupations.Select(o => o.Name)));
        Row(work, "Job (16+)", _job);
        _money = Amount();
        Row(work, "Savings (2020 money)", _money);
        _home = Options(new[] { "Chance", "Owns a home", "Does not own a home" });
        Row(work, "Home", _home);

        // Traits.
        var traits = Section(col, "Traits");
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 6);
        flow.AddThemeConstantOverride("v_separation", 6);
        foreach (var (id, name) in CharacterCreator.Traits())
        {
            string traitId = id;
            var b = Ui.Button(name, () => { }, 36);
            b.ToggleMode = true;
            b.Toggled += on => { if (on) _traits.Add(traitId); else _traits.Remove(traitId); };
            b.SetMeta("trait", traitId);
            flow.AddChild(b);
        }
        traits.AddChild(flow);

        _status = Ui.Label("", 16, UiTheme.Muted, wrap: true);
        col.AddChild(_status);
        var buttons = Ui.HBox(10);
        var back = Ui.Button("Back", () => _main.ShowTitle(), 52);
        back.CustomMinimumSize = new Vector2(160, 52);
        buttons.AddChild(back);
        var reset = Ui.Button("Clear", () => { DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(LastPath)); _main.ShowCharacterCreator(); }, 52);
        reset.CustomMinimumSize = new Vector2(160, 52);
        buttons.AddChild(reset);
        _start = Ui.Button("Start this life", Start, 52);
        _start.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        UiTheme.MakePrimary(_start);
        buttons.AddChild(_start);
        col.AddChild(buttons);
        col.AddChild(Ui.Spacer(20));

        FillCities();
        Load(flow);
        Ui.FocusLater(_name);
    }

    // --- Building the form ----------------------------------------------------------------------

    private static VBoxContainer Section(VBoxContainer parent, string title)
    {
        var box = Ui.VBox(8);
        box.AddChild(Ui.Label(title, 21, UiTheme.Text));
        parent.AddChild(Ui.Card(box));
        return box;
    }

    private static void Row(VBoxContainer parent, string label, Control control)
    {
        var row = Ui.HBox(12);
        var l = Ui.Label(label, 17, UiTheme.Muted);
        l.CustomMinimumSize = new Vector2(280, 0);
        row.AddChild(l);
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        control.CustomMinimumSize = new Vector2(0, 40);
        row.AddChild(control);
        parent.AddChild(row);
    }

    private static OptionButton Options(IEnumerable<string> items)
    {
        var o = new OptionButton();
        foreach (var item in items) o.AddItem(item);
        return o;
    }

    private static SpinBox Spin(int min, int max, int value) => new() { MinValue = min, MaxValue = max, Value = value, Step = 1, Rounded = true };

    private static LineEdit Amount() => new() { PlaceholderText = "Chance", MaxLength = 14 };

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private CountryDef Country => _countries[Math.Max(0, _country.Selected)];

    private void FillCities()
    {
        _city.Clear();
        _city.AddItem("Chance");
        foreach (var c in Country.Cities) _city.AddItem(c.Name);
        _city.Selected = 0;
    }

    // --- Reading the form -----------------------------------------------------------------------

    private static double? Number(LineEdit e) =>
        double.TryParse(e.Text.Replace(" ", "").Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static double? Stat(SpinBox s) => s.Value > 0 ? s.Value : null;

    private CharacterSpec Spec() => new()
    {
        FirstName = string.IsNullOrWhiteSpace(_name.Text) ? null : _name.Text.Trim(),
        Sex = _sex.Selected switch { 1 => Sex.Male, 2 => Sex.Female, _ => null },
        CountryId = Country.Id,
        CityId = _city.Selected > 0 ? Country.Cities[_city.Selected - 1].Id : null,
        StartYear = (int)_year.Value,
        Age = (int)_age.Value,
        SeedCode = string.IsNullOrWhiteSpace(_seed.Text) ? null : _seed.Text.Trim(),
        Family = _family.Selected > 0 ? StartChoices.Conditions[_family.Selected - 1].Id : null,
        ParentsMoney = Number(_parentsMoney),
        Partner = _partner.Selected switch { 1 => true, 2 => false, _ => null },
        MinChildren = _children.Value > 0 ? (int)_children.Value : null,
        Smarts = Stat(_stats["Smarts"]), Looks = Stat(_stats["Looks"]), Fitness = Stat(_stats["Fitness"]),
        Health = Stat(_stats["Health"]), Happiness = Stat(_stats["Happiness"]),
        Addiction = _addiction.Selected > 0 ? CharacterCreator.Addictions[_addiction.Selected - 1] : null,
        Ailment = _ailment.Selected > 0 ? _ailments[_ailment.Selected - 1].Id : null,
        Occupation = _job.Selected > 0 ? _occupations[_job.Selected - 1].Id : null,
        Money = Number(_money),
        OwnsHome = _home.Selected switch { 1 => true, 2 => false, _ => null },
        Traits = _traits.ToList(),
    };

    private void Start()
    {
        var spec = Spec();
        Save(spec);
        _start.Disabled = true;
        _status.Text = spec.Age > 0 ? $"Living the first {spec.Age} years…" : "Starting…";
        // Let the text show before the years are played.
        GetTree().CreateTimer(0.05).Timeout += () =>
        {
            if (SaveSystem.FirstEmptySlot() == null) _main.ShowSlots(slot => _main.StartCreatedLife(spec, slot));
            else _main.StartCreatedLife(spec);
        };
    }

    // --- Remembering the last creation ----------------------------------------------------------

    private static void Save(CharacterSpec spec)
    {
        try
        {
            using var f = FileAccess.Open(LastPath, FileAccess.ModeFlags.Write);
            f?.StoreString(JsonSerializer.Serialize(spec));
        }
        catch (Exception e) { GD.PushWarning($"Could not remember the character: {e.Message}"); }
    }

    private void Load(HFlowContainer traitButtons)
    {
        CharacterSpec? spec;
        try
        {
            if (!FileAccess.FileExists(LastPath)) return;
            using var f = FileAccess.Open(LastPath, FileAccess.ModeFlags.Read);
            spec = f == null ? null : JsonSerializer.Deserialize<CharacterSpec>(f.GetAsText());
        }
        catch { return; }
        if (spec == null) return;

        _name.Text = spec.FirstName ?? "";
        _sex.Selected = spec.Sex switch { Sex.Male => 1, Sex.Female => 2, _ => 0 };
        _country.Selected = Math.Max(0, _countries.FindIndex(c => c.Id == spec.CountryId));
        FillCities();
        _city.Selected = spec.CityId == null ? 0 : Country.Cities.ToList().FindIndex(c => c.Id == spec.CityId) + 1;
        _year.Value = spec.StartYear;
        _age.Value = spec.Age;
        _seed.Text = spec.SeedCode ?? "";
        _family.Selected = spec.Family == null ? 0 : StartChoices.Conditions.ToList().FindIndex(c => c.Id == spec.Family) + 1;
        _parentsMoney.Text = spec.ParentsMoney?.ToString("0", CultureInfo.InvariantCulture) ?? "";
        _partner.Selected = spec.Partner switch { true => 1, false => 2, _ => 0 };
        _children.Value = spec.MinChildren ?? 0;
        _stats["Smarts"].Value = spec.Smarts ?? 0;
        _stats["Looks"].Value = spec.Looks ?? 0;
        _stats["Fitness"].Value = spec.Fitness ?? 0;
        _stats["Health"].Value = spec.Health ?? 0;
        _stats["Happiness"].Value = spec.Happiness ?? 0;
        _addiction.Selected = spec.Addiction == null ? 0 : Array.IndexOf(CharacterCreator.Addictions, spec.Addiction) + 1;
        _ailment.Selected = spec.Ailment == null ? 0 : _ailments.ToList().FindIndex(a => a.Id == spec.Ailment) + 1;
        _job.Selected = spec.Occupation == null ? 0 : _occupations.ToList().FindIndex(o => o.Id == spec.Occupation) + 1;
        _money.Text = spec.Money?.ToString("0", CultureInfo.InvariantCulture) ?? "";
        _home.Selected = spec.OwnsHome switch { true => 1, false => 2, _ => 0 };
        foreach (var b in traitButtons.GetChildren().OfType<Button>())
            if (spec.Traits.Contains((string)b.GetMeta("trait"))) b.ButtonPressed = true;
    }
}
