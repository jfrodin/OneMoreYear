using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OneMoreYear.Simulation;
using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Model;
using OneMoreYear.Simulation.Systems;

namespace OneMoreYear.Game;

public partial class TitleScreen : Control
{
    private Main _main = null!;
    private OptionButton _year = null!;
    private OptionButton _sex = null!, _conditions = null!, _city = null!;
    private Label _conditionInfo = null!;
    private IReadOnlyList<CityDef> _cities = null!;

    /// <summary>Where a life can begin: a decade and what the country felt like then (country content).</summary>
    private IReadOnlyList<(int Year, string Title)> _decades = Array.Empty<(int, string)>();
    /// <summary>Every country in the content; a choice appears as soon as there is more than one.</summary>
    private readonly List<CountryDef> _countries = ContentDb.Embedded.Countries.Values.OrderBy(c => c.Name).ToList();
    private OptionButton? _country;
    private LineEdit _seed = null!;

    public void Init(Main main) => _main = main;

    public override void _Ready()
    {
        // Centred when the window is big enough, scrollable when it is not.
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(scroll);
        var center = new CenterContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        scroll.AddChild(center);

        var col = Ui.VBox(10);
        col.CustomMinimumSize = new Vector2(520, 0);
        center.AddChild(col);

        var title = Ui.Label("ONE MORE YEAR", 56, UiTheme.Accent);
        title.AddThemeFontOverride("font", UiTheme.Masthead);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(title);
        var tagline = UiTheme.HandLabel("Live a life. Build a family. Leave a legacy.", 26, UiTheme.Muted);
        tagline.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(tagline);
        col.AddChild(Ui.Spacer(12));

        Button? first = null;
        if (SaveSystem.HasSave)
        {
            var saved = Ui.HBox(10);
            var cont = Ui.Button("Continue", () => _main.ContinueGame(), 56);
            cont.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            UiTheme.MakePrimary(cont);
            saved.AddChild(cont);
            var load = Ui.Button("Load game", () => _main.ShowSlots(), 56);
            load.CustomMinimumSize = new Vector2(180, 56);
            saved.AddChild(load);
            col.AddChild(saved);
            first = cont;
        }

        // New game options
        var options = Ui.VBox(10);
        var newTitle = Ui.Label("A new life", 26, UiTheme.Accent);
        newTitle.AddThemeFontOverride("font", UiTheme.Heading);
        options.AddChild(newTitle);
        if (_countries.Count > 1)
        {
            var countryRow = Ui.HBox(12);
            var countryLabel = Ui.Label("Country", 18, UiTheme.Muted);
            countryLabel.CustomMinimumSize = new Vector2(140, 0);
            countryRow.AddChild(countryLabel);
            _country = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 44) };
            foreach (var c in _countries) _country.AddItem(c.Name);
            // --country=ID picks another country for automated runs.
            _country.Selected = Math.Max(0, _countries.FindIndex(c => c.Id == Main.ArgCountry));
            _country.ItemSelected += _ => FillForCountry();
            countryRow.AddChild(_country);
            options.AddChild(countryRow);
        }
        var yearRow = Ui.HBox(12);
        var yearLabel = Ui.Label("Begin in", 18, UiTheme.Muted);
        yearLabel.CustomMinimumSize = new Vector2(140, 0);
        yearRow.AddChild(yearLabel);
        _year = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 44) };
        yearRow.AddChild(_year);
        options.AddChild(yearRow);

        var seedRow = Ui.HBox(12);
        var seedLabel = Ui.Label("Seed", 18, UiTheme.Muted);
        seedLabel.CustomMinimumSize = new Vector2(140, 0);
        seedRow.AddChild(seedLabel);
        _seed = new LineEdit { PlaceholderText = "Random, or a code from a friend", MaxLength = 24, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        seedRow.AddChild(_seed);
        options.AddChild(seedRow);

        // A few choices about the first life – all left to chance unless the player wants otherwise.
        OptionButton Choice(string label, IEnumerable<string> items)
        {
            var row = Ui.HBox(12);
            var l = Ui.Label(label, 18, UiTheme.Muted);
            l.CustomMinimumSize = new Vector2(140, 0);
            row.AddChild(l);
            var o = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 44) };
            foreach (var item in items) o.AddItem(item);
            row.AddChild(o);
            options.AddChild(row);
            return o;
        }
        // Sex and city share a row: "You are [a girl] in [Umeå]".
        _sex = Choice("You are", new[] { "Surprise me", "A boy", "A girl" });
        var whoRow = (HBoxContainer)_sex.GetParent();
        whoRow.AddChild(Ui.Label("in", 18, UiTheme.Muted));
        _city = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 44) };
        whoRow.AddChild(_city);
        FillForCountry();
        _conditions = Choice("Your start", new[] { "Leave it to chance (as intended)" }.Concat(StartChoices.Conditions.Select(c => c.Name)));
        _conditionInfo = Ui.Label("", 15, UiTheme.Faint, wrap: true);
        options.AddChild(_conditionInfo);
        _conditions.ItemSelected += _ => UpdateConditionInfo();
        UpdateConditionInfo();

        // The character creator: a separate feature, switched on in Features.
        if (Features.CharacterCreator)
        {
            var create = Ui.Button("Create a character…", () => _main.ShowCharacterCreator(), 48);
            options.AddChild(create);
        }

        var start = Ui.Button("New Life", StartNew, 56);
        // As visible as Continue: starting over is just as much a main path.
        UiTheme.MakePrimary(start);
        options.AddChild(start);
        col.AddChild(Ui.Card(options));

        var row = Ui.HBox(10);
        var achievements = Ui.Button("Achievements", () => _main.ShowAchievements());
        achievements.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(achievements);
        if (FamilyArchive.All.Count > 0)
        {
            var families = Ui.Button("Families", () => _main.ShowFamilies());
            families.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(families);
        }
        var content = Ui.Button("Settings", () => _main.ShowSettings(null));
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(content);
        col.AddChild(row);
        var quit = Ui.Button("Quit", () => GetTree().Quit());
        col.AddChild(quit);

        var warning = Ui.Label("For adults (18+). Contains violence, abuse, addiction and crime. Adjust in Settings.", 15, UiTheme.Faint, wrap: true);
        warning.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(warning);
        var hint = Ui.Label($"Version {Main.Version}" + (_countries.Count == 1 ? $"  ·  {_countries[0].Name} is the first country. More will follow." : ""), 15, UiTheme.Faint);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(hint);

        Ui.FocusLater(first ?? start);
        // The first time the game starts, ask about dark themes before anything else.
        bool automated = System.Linq.Enumerable.Any(OS.GetCmdlineUserArgs(), a => a == "--smoke" || a.StartsWith("--screenshots="));
        if (Settings.ShouldAskAboutContent && !automated)
            CallDeferred(nameof(AskAboutContent));
    }

    private CountryDef Country => _countries[_country?.Selected ?? Math.Max(0, _countries.FindIndex(c => c.Id == "sweden"))];

    /// <summary>The decades and cities of the chosen country.</summary>
    private void FillForCountry()
    {
        int previousYear = _decades.Count > 0 && _year.Selected >= 0 ? _decades[_year.Selected].Year : 1970;
        _decades = GameSession.StartDecades(Country.Id);
        _year.Clear();
        foreach (var (_, decade) in _decades) _year.AddItem(decade);
        int index = _decades.ToList().FindIndex(d => d.Year == previousYear);
        _year.Selected = index >= 0 ? index : 0;
        _cities = Country.Cities;
        _city.Clear();
        _city.AddItem("Surprise me");
        foreach (var c in _cities) _city.AddItem(c.Name);
        _city.Selected = 0;
    }

    private void AskAboutContent() => _main.ShowContentSettings(null, firstTime: true);

    private void StartNew()
    {
        string? seed = string.IsNullOrWhiteSpace(_seed.Text) ? null : _seed.Text;
        int year = _decades[_year.Selected].Year;
        string? scenario = null;
        var choices = new NewGameOptions
        {
            PlayerSex = _sex.Selected switch { 1 => Sex.Male, 2 => Sex.Female, _ => null },
            StartConditions = _conditions.Selected > 0 ? StartChoices.Conditions[_conditions.Selected - 1].Id : null,
            CityId = _city.Selected > 0 ? _cities[_city.Selected - 1].Id : null,
            CountryId = Country.Id,
        };
        // All slots taken: the player picks which family to replace.
        if (SaveSystem.FirstEmptySlot() == null)
            _main.ShowSlots(slot => _main.StartNewGame(year, seed, scenario, slot, choices));
        else
            _main.StartNewGame(year, seed, scenario, choices: choices);
    }

    private void UpdateConditionInfo() =>
        _conditionInfo.Text = _conditions.Selected > 0
            ? StartChoices.Conditions[_conditions.Selected - 1].Description
            : "Whatever family fate gives you. This is how One More Year is meant to be played.";

}
