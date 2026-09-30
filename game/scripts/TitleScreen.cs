using Godot;

namespace OneMoreYear.Game;

public partial class TitleScreen : Control
{
    private Main _main = null!;
    private SpinBox _year = null!;
    private LineEdit _seed = null!;

    public void Init(Main main) => _main = main;

    public override void _Ready()
    {
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var col = Ui.VBox(14);
        col.CustomMinimumSize = new Vector2(520, 0);
        center.AddChild(col);

        var title = Ui.Label("ONE MORE YEAR", 64, UiTheme.Accent);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(title);
        var tagline = Ui.Label("Live a life. Build a family. Leave a legacy.", 20, UiTheme.Muted);
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
        _seed = new LineEdit { PlaceholderText = "Random", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        seedRow.AddChild(_seed);
        options.AddChild(seedRow);

        var start = Ui.Button("New Life", StartNew, 56);
        if (first == null) UiTheme.MakePrimary(start);
        options.AddChild(start);
        col.AddChild(Ui.Card(options));

        var quit = Ui.Button("Quit", () => GetTree().Quit());
        col.AddChild(quit);

        var hint = Ui.Label("Sweden is the first country. More will follow.", 15, UiTheme.Faint);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(hint);

        Ui.FocusLater(first ?? start);
    }

    private void StartNew()
    {
        ulong? seed = ulong.TryParse(_seed.Text.Trim(), out var s) ? s : null;
        _main.StartNewGame((int)_year.Value, seed);
    }
}
