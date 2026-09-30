using System.Collections.Generic;
using Godot;
using OneMoreYear.Simulation;
using OneMoreYear.Simulation.Content;

namespace OneMoreYear.Game;

public partial class TitleScreen : Control
{
    private Main _main = null!;
    private SpinBox _year = null!;
    private LineEdit _seed = null!;
    private OptionButton _scenario = null!;
    private Label _scenarioInfo = null!;
    private IReadOnlyList<ScenarioDef> _scenarios = null!;

    public void Init(Main main) => _main = main;

    public override void _Ready()
    {
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var col = Ui.VBox(14);
        col.CustomMinimumSize = new Vector2(520, 0);
        center.AddChild(col);

        var title = Ui.Label("ONE MORE YEAR", 72, UiTheme.Accent);
        title.AddThemeFontOverride("font", UiTheme.Masthead);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(title);
        var tagline = UiTheme.HandLabel("Live a life. Build a family. Leave a legacy.", 30, UiTheme.Muted);
        tagline.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(tagline);
        col.AddChild(Ui.Spacer(30));

        Button? first = null;
        if (SaveSystem.HasSave)
        {
            var cont = Ui.Button("Continue", () => _main.ContinueGame(), 56);
            UiTheme.MakePrimary(cont);
            col.AddChild(cont);
            first = cont;
        }

        // New game options
        var options = Ui.VBox(10);
        var yearRow = Ui.HBox(12);
        var yearLabel = Ui.Label("Start year", 18, UiTheme.Muted);
        yearLabel.CustomMinimumSize = new Vector2(140, 0);
        yearRow.AddChild(yearLabel);
        _year = new SpinBox { MinValue = 1950, MaxValue = 2020, Step = 1, Value = 1970, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        yearRow.AddChild(_year);
        options.AddChild(yearRow);

        var seedRow = Ui.HBox(12);
        var seedLabel = Ui.Label("Seed", 18, UiTheme.Muted);
        seedLabel.CustomMinimumSize = new Vector2(140, 0);
        seedRow.AddChild(seedLabel);
        _seed = new LineEdit { PlaceholderText = "Random – or a code from a friend", MaxLength = 24, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        seedRow.AddChild(_seed);
        options.AddChild(seedRow);

        // Test scenarios (a playtesting tool, docs/test-scenarios.md): only in development builds, never in a release.
        var scenarioRow = Ui.HBox(12);
        var scenarioLabel = Ui.Label("Scenario", 18, UiTheme.Muted);
        scenarioLabel.CustomMinimumSize = new Vector2(140, 0);
        scenarioRow.AddChild(scenarioLabel);
        _scenarios = GameSession.AvailableScenarios();
        _scenario = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 44) };
        _scenario.AddItem("None – a random family");
        foreach (var s in _scenarios) _scenario.AddItem($"{s.Name}  (seed {s.Seed})");
        _scenario.ItemSelected += _ => UpdateScenarioInfo();
        scenarioRow.AddChild(_scenario);
        scenarioRow.Visible = OS.IsDebugBuild();
        options.AddChild(scenarioRow);
        _scenarioInfo = Ui.Label("", 15, UiTheme.Faint, wrap: true);
        options.AddChild(_scenarioInfo);
        UpdateScenarioInfo();

        var start = Ui.Button("New Life", StartNew, 56);
        if (first == null) UiTheme.MakePrimary(start);
        options.AddChild(start);
        col.AddChild(Ui.Card(options));

        var content = Ui.Button("Content settings", () => _main.ShowContentSettings(null));
        col.AddChild(content);
        var quit = Ui.Button("Quit", () => GetTree().Quit());
        col.AddChild(quit);

        var warning = Ui.Label("For adults (18+). Contains violence, abuse, addiction and crime – adjust in Content settings.", 15, UiTheme.Faint, wrap: true);
        warning.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(warning);
        var hint = Ui.Label($"Version {Main.Version}  ·  Sweden is the first country. More will follow.", 15, UiTheme.Faint);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(hint);

        Ui.FocusLater(first ?? start);
        // The first time the game starts, ask about dark themes before anything else.
        bool automated = System.Linq.Enumerable.Any(OS.GetCmdlineUserArgs(), a => a == "--smoke" || a.StartsWith("--screenshots="));
        if (Settings.ShouldAskAboutContent && !automated)
            CallDeferred(nameof(AskAboutContent));
    }

    private void AskAboutContent() => _main.ShowContentSettings(null, firstTime: true);

    private void StartNew()
    {
        string? seed = string.IsNullOrWhiteSpace(_seed.Text) ? null : _seed.Text;
        _main.StartNewGame((int)_year.Value, seed, SelectedScenario?.Id);
    }

    private ScenarioDef? SelectedScenario => _scenario.Selected > 0 ? _scenarios[_scenario.Selected - 1] : null;

    private void UpdateScenarioInfo()
    {
        var s = SelectedScenario;
        _year.Editable = s == null;
        _scenarioInfo.Text = s == null ? "" : $"{s.Description}\nStarts in {s.StartYear}{(s.Age > 0 ? $", aged {s.Age}" : "")}. A seed above replaces the scenario's own.";
        _scenarioInfo.Visible = s != null;
    }
}
