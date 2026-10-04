using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OneMoreYear.Simulation;
using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Model;
using OneMoreYear.Simulation.Systems;

namespace OneMoreYear.Game;

/// <summary>
/// The first thing anyone sees: the title and a short menu on the left, a wall of family photographs
/// on the right. Starting a new life opens its own page with the choices, so the first look is a
/// game and not a form.
/// </summary>
public partial class TitleScreen : Control
{
    private Main _main = null!;
    private VBoxContainer _left = null!;
    private OptionButton _year = null!;
    private OptionButton _sex = null!, _conditions = null!, _city = null!;
    private Label _conditionInfo = null!;
    private IReadOnlyList<CityDef> _cities = null!;

    /// <summary>Where a life can begin: a decade and what the country felt like then (country content).</summary>
    private IReadOnlyList<(int Year, string Title, string Text)> _decades = Array.Empty<(int, string, string)>();
    private Label _decadeInfo = null!;
    /// <summary>Every country in the content; a choice appears as soon as there is more than one.</summary>
    private readonly List<CountryDef> _countries = ContentDb.Embedded.Countries.Values.OrderBy(c => c.Name).ToList();
    private OptionButton? _country;
    private LineEdit _seed = null!;
    private bool _onNewLife;

    public void Init(Main main) => _main = main;

    public override void _Ready()
    {
        // The wall of photographs, on the right.
        var wall = new PhotoWall();
        wall.SetAnchorsPreset(LayoutPreset.FullRect);
        wall.AnchorLeft = 0.44f;
        AddChild(wall);

        // The menu, on the left, scrolling only if the window is very small.
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        scroll.AnchorRight = 0.46f;
        AddChild(scroll);
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        margin.AddThemeConstantOverride("margin_left", 90);
        margin.AddThemeConstantOverride("margin_right", 30);
        margin.AddThemeConstantOverride("margin_top", 40);
        margin.AddThemeConstantOverride("margin_bottom", 30);
        scroll.AddChild(margin);
        _left = Ui.VBox(10);
        _left.Alignment = BoxContainer.AlignmentMode.Center;
        _left.SizeFlagsVertical = SizeFlags.ExpandFill;
        margin.AddChild(_left);

        ShowMenu();
        // The first time the game starts, ask about dark themes before anything else.
        bool automated = System.Linq.Enumerable.Any(OS.GetCmdlineUserArgs(), a => a == "--smoke" || a.StartsWith("--screenshots="));
        if (Settings.ShouldAskAboutContent && !automated)
            CallDeferred(nameof(AskAboutContent));
    }

    /// <summary>Escape (or B on a controller) goes back from the new life page to the menu.</summary>
    public override void _UnhandledInput(InputEvent e)
    {
        if (!_onNewLife || _main.HasModal || !e.IsActionPressed("ui_cancel")) return;
        GetViewport().SetInputAsHandled();
        ShowMenu();
    }

    // --- The menu ---------------------------------------------------------------------------------

    public void ShowMenu()
    {
        Ui.Clear(_left);
        _onNewLife = false;
        var title = Ui.Label("ONE MORE\nYEAR", 84, UiTheme.Accent);
        title.AddThemeFontOverride("font", UiTheme.Masthead);
        title.AddThemeConstantOverride("line_spacing", -18);
        _left.AddChild(title);
        _left.AddChild(UiTheme.HandLabel("Live a life. Build a family. Leave a legacy.", 28, UiTheme.Muted));
        _left.AddChild(Ui.Spacer(28));

        Button? first = null;
        if (SaveSystem.HasSave)
        {
            first = MenuItem("Continue", () => _main.ContinueGame(), main: true);
            MenuItem("Load a life", () => _main.ShowSlots());
        }
        var start = MenuItem("New life", ShowNewLife, main: first == null);
        if (Features.CharacterCreator) MenuItem("Create a character", () => _main.ShowCharacterCreator());
        MenuItem("Achievements", () => _main.ShowAchievements());
        if (FamilyArchive.All.Count > 0) MenuItem("Families", () => _main.ShowFamilies());
        MenuItem("Settings", () => _main.ShowSettings(null));
        MenuItem("Quit", () => GetTree().Quit());

        _left.AddChild(Ui.Spacer(36));
        _left.AddChild(Ui.Label("For adults (18+). Contains violence, abuse, addiction and crime. Adjust in Settings.", 14, UiTheme.Faint, wrap: true));
        _left.AddChild(Ui.Label($"Version {Main.Version}", 14, UiTheme.Faint));
        Ui.FocusLater(first ?? start);
    }

    /// <summary>A menu line: large type, no box, a mark in the margin when chosen.</summary>
    private Button MenuItem(string text, Action pressed, bool main = false)
    {
        var b = new Button { Text = text, Alignment = HorizontalAlignment.Left, FocusMode = FocusModeEnum.All, MouseDefaultCursorShape = CursorShape.PointingHand };
        b.AddThemeFontOverride("font", UiTheme.Heading);
        b.AddThemeFontSizeOverride("font_size", main ? 38 : 30);
        var plain = new StyleBoxEmpty { ContentMarginLeft = 18, ContentMarginTop = 2, ContentMarginBottom = 2 };
        var marked = new StyleBoxFlat { BgColor = new Color(UiTheme.Accent, 0.08f), BorderColor = UiTheme.Accent, ContentMarginLeft = 18, ContentMarginTop = 2, ContentMarginBottom = 2 };
        marked.BorderWidthLeft = 5;
        b.AddThemeStyleboxOverride("normal", plain);
        b.AddThemeStyleboxOverride("hover", marked);
        b.AddThemeStyleboxOverride("focus", marked);
        b.AddThemeStyleboxOverride("pressed", marked);
        b.AddThemeStyleboxOverride("hover_pressed", marked);
        var color = main ? UiTheme.Accent : UiTheme.Text;
        b.AddThemeColorOverride("font_color", color);
        b.AddThemeColorOverride("font_hover_color", UiTheme.Accent);
        b.AddThemeColorOverride("font_focus_color", UiTheme.Accent);
        b.AddThemeColorOverride("font_pressed_color", UiTheme.AccentDark);
        b.AddThemeColorOverride("font_hover_pressed_color", UiTheme.AccentDark);
        b.Pressed += () => { Sound.Play("click"); pressed(); };
        // Keyboard and controller: the mark follows the mouse too, so there is only ever one.
        b.MouseEntered += () => b.GrabFocus();
        _left.AddChild(b);
        return b;
    }

    // --- A new life -------------------------------------------------------------------------------

    /// <summary>The choices for a new life, on a page of their own.</summary>
    public void ShowNewLife()
    {
        Ui.Clear(_left);
        _onNewLife = true;
        var heading = UiTheme.HeadingLabel("A new life", 44, UiTheme.Accent);
        _left.AddChild(heading);
        _left.AddChild(UiTheme.HandLabel("Everything can be left to chance. That is how it is meant to be played.", 22, UiTheme.Muted, wrap: true));
        _left.AddChild(Ui.Spacer(8));

        var options = Ui.VBox(10);
        if (_countries.Count > 1)
        {
            _country = Option(options, "Country", _countries.Select(c => c.Name));
            // --country=ID picks another country for automated runs.
            _country.Selected = Math.Max(0, _countries.FindIndex(c => c.Id == Main.ArgCountry));
            _country.ItemSelected += _ => FillForCountry();
        }
        _year = Option(options, "Begin in", Array.Empty<string>());
        // What the decade was like, so "the record years" means something before you pick it.
        _decadeInfo = Ui.Label("", 15, UiTheme.Faint, wrap: true);
        options.AddChild(_decadeInfo);
        _year.ItemSelected += _ => UpdateDecadeInfo();

        // Sex and city share a row: "You are [a girl] in [Umeå]".
        _sex = Option(options, "You are", new[] { "Surprise me", "A boy", "A girl" });
        var whoRow = (HBoxContainer)_sex.GetParent();
        whoRow.AddChild(Ui.Label("in", 18, UiTheme.Muted));
        _city = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 44), ClipText = true };
        whoRow.AddChild(_city);
        FillForCountry();
        _conditions = Option(options, "Your start", new[] { "Leave it to chance" }.Concat(StartChoices.Conditions.Select(c => c.Name)));
        _conditionInfo = Ui.Label("", 15, UiTheme.Faint, wrap: true);
        options.AddChild(_conditionInfo);
        _conditions.ItemSelected += _ => UpdateConditionInfo();
        UpdateConditionInfo();

        var seedRow = Ui.HBox(12);
        var seedLabel = Ui.Label("Seed", 18, UiTheme.Muted);
        seedLabel.CustomMinimumSize = new Vector2(130, 0);
        seedRow.AddChild(seedLabel);
        _seed = new LineEdit { PlaceholderText = "Random, or a code from a friend", MaxLength = 24, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        seedRow.AddChild(_seed);
        options.AddChild(seedRow);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", UiTheme.PaperCard(20));
        card.AddChild(options);
        _left.AddChild(card);
        _left.AddChild(Ui.Spacer(6));

        var buttons = Ui.HBox(10);
        var back = Ui.Button("Back", ShowMenu, 56);
        back.CustomMinimumSize = new Vector2(140, 56);
        buttons.AddChild(back);
        var begin = Ui.Button("Begin", StartNew, 56);
        begin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        UiTheme.MakePrimary(begin);
        buttons.AddChild(begin);
        _left.AddChild(buttons);
        Ui.FocusLater(begin);
    }

    private static OptionButton Option(VBoxContainer parent, string label, IEnumerable<string> items)
    {
        var row = Ui.HBox(12);
        var l = Ui.Label(label, 18, UiTheme.Muted);
        l.CustomMinimumSize = new Vector2(130, 0);
        row.AddChild(l);
        var o = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 44), ClipText = true };
        foreach (var item in items) o.AddItem(item);
        row.AddChild(o);
        parent.AddChild(row);
        return o;
    }

    private CountryDef Country => _countries[_country?.Selected ?? Math.Max(0, _countries.FindIndex(c => c.Id == "sweden"))];

    /// <summary>The decades and cities of the chosen country.</summary>
    private void FillForCountry()
    {
        int previousYear = _decades.Count > 0 && _year.Selected >= 0 && _year.Selected < _decades.Count ? _decades[_year.Selected].Year : 1970;
        _decades = GameSession.StartDecades(Country.Id);
        _year.Clear();
        foreach (var (_, decade, _) in _decades) _year.AddItem(decade);
        int index = _decades.ToList().FindIndex(d => d.Year == previousYear);
        _year.Selected = index >= 0 ? index : 0;
        UpdateDecadeInfo();
        _cities = Country.Cities;
        _city.Clear();
        _city.AddItem("Surprise me");
        foreach (var c in _cities) _city.AddItem(c.Name);
        _city.Selected = 0;
    }

    private void UpdateDecadeInfo() =>
        _decadeInfo.Text = _year.Selected >= 0 && _year.Selected < _decades.Count ? _decades[_year.Selected].Text : "";

    private void AskAboutContent() => _main.ShowContentSettings(null, firstTime: true);

    private void StartNew()
    {
        string? seed = string.IsNullOrWhiteSpace(_seed.Text) ? null : _seed.Text;
        int year = _decades[_year.Selected].Year;
        var choices = new NewGameOptions
        {
            PlayerSex = _sex.Selected switch { 1 => Sex.Male, 2 => Sex.Female, _ => null },
            StartConditions = _conditions.Selected > 0 ? StartChoices.Conditions[_conditions.Selected - 1].Id : null,
            CityId = _city.Selected > 0 ? _cities[_city.Selected - 1].Id : null,
            CountryId = Country.Id,
        };
        // All slots taken: the player picks which family to replace.
        if (SaveSystem.FirstEmptySlot() == null)
            _main.ShowSlots(slot => _main.StartNewGame(year, seed, null, slot, choices));
        else
            _main.StartNewGame(year, seed, null, choices: choices);
    }

    private void UpdateConditionInfo() =>
        _conditionInfo.Text = _conditions.Selected > 0
            ? StartChoices.Conditions[_conditions.Selected - 1].Description
            : "Whatever family fate gives you.";
}
