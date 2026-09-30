using Godot;
using OneMoreYear.Simulation;

namespace OneMoreYear.Game;

/// <summary>
/// Root of the game. Owns the current <see cref="GameSession"/> and switches between screens.
/// All rules live in the simulation library; this layer only shows state and forwards choices.
/// </summary>
public partial class Main : Control
{
    public GameSession? Session { get; private set; }

    private Control? _screen;
    private Control _overlayLayer = null!;

    public override void _Ready()
    {
        Theme = UiTheme.Build();
        SetAnchorsPreset(LayoutPreset.FullRect);
        RegisterInput();

        var bg = new ColorRect { Color = UiTheme.Background, MouseFilter = MouseFilterEnum.Ignore };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        _overlayLayer = new Control { MouseFilter = MouseFilterEnum.Ignore, ZIndex = 10 };
        _overlayLayer.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_overlayLayer);

        ShowTitle();
    }

    /// <summary>Keyboard and controller bindings in addition to Godot's built-in ui_* actions.</summary>
    private static void RegisterInput()
    {
        void Bind(string action, Key key, JoyButton button)
        {
            if (InputMap.HasAction(action)) return;
            InputMap.AddAction(action);
            InputMap.ActionAddEvent(action, new InputEventKey { Keycode = key });
            InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = button });
        }
        Bind("omy_next_year", Key.N, JoyButton.Y);
        Bind("omy_tab_prev", Key.Q, JoyButton.LeftShoulder);
        Bind("omy_tab_next", Key.E, JoyButton.RightShoulder);
    }

    private void SetScreen(Control screen)
    {
        if (_screen != null)
        {
            RemoveChild(_screen);
            _screen.QueueFree();
        }
        Ui.Clear(_overlayLayer);
        _screen = screen;
        screen.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(screen);
        MoveChild(_overlayLayer, -1);
    }

    public void ShowTitle()
    {
        var title = new TitleScreen();
        title.Init(this);
        SetScreen(title);
    }

    public void StartNewGame(int startYear, ulong? seed)
    {
        Session = GameSession.NewGame(new NewGameOptions { StartYear = startYear, Seed = seed });
        SaveSystem.Save(Session);
        ShowGame();
    }

    public void ContinueGame()
    {
        Session = SaveSystem.Load();
        if (Session == null) { ShowTitle(); return; }
        if (Session.GameOver) ShowGameOver();
        else if (Session.NeedsSuccession) ShowSuccession();
        else ShowGame();
    }

    public void ShowGame()
    {
        var game = new GameScreen();
        game.Init(this);
        SetScreen(game);
    }

    public void ShowSuccession()
    {
        var s = new SuccessionScreen();
        s.Init(this);
        SetScreen(s);
    }

    public void ShowGameOver()
    {
        var s = new GameOverScreen();
        s.Init(this);
        SetScreen(s);
    }

    public void AutoSave()
    {
        if (Session != null) SaveSystem.Save(Session);
    }

    /// <summary>Shows a modal message with an OK button (controller friendly). Focus returns afterwards.</summary>
    public void ShowMessage(string title, string text, System.Action? onClose = null)
    {
        var previousFocus = GetViewport().GuiGetFocusOwner();

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.AddChild(center);

        var box = Ui.VBox(16);
        box.CustomMinimumSize = new Vector2(560, 0);
        box.AddChild(Ui.Label(title, 26, UiTheme.Accent));
        box.AddChild(Ui.Label(text, 19, UiTheme.Text, wrap: true));
        var ok = Ui.Button("OK", () =>
        {
            _overlayLayer.RemoveChild(dim);
            dim.QueueFree();
            if (IsInstanceValid(previousFocus) && previousFocus!.IsInsideTree() && previousFocus.IsVisibleInTree())
                previousFocus.GrabFocus();
            onClose?.Invoke();
        });
        ok.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        ok.CustomMinimumSize = new Vector2(140, 48);
        box.AddChild(ok);
        center.AddChild(Ui.Card(box, UiTheme.Panel, UiTheme.AccentDark));

        _overlayLayer.AddChild(dim);
        ok.GrabFocus();
    }

    public bool HasModal => _overlayLayer.GetChildCount() > 0;

    // --- Smoke test -----------------------------------------------------------------------------
    // Run with:  Godot --path game -- --smoke   (add --headless for no window)
    // Plays through the real UI for a few hundred steps and quits; errors show up in the log.

    private int _smokeStep = -1;
    private int _shotFrame;
    private string? _shotDir;

    public override void _Process(double delta)
    {
        if (_shotDir != null || System.Linq.Enumerable.Any(OS.GetCmdlineUserArgs(), a => a.StartsWith("--screenshots=")))
        {
            ScreenshotTour();
            return;
        }
        if (_smokeStep < 0)
        {
            if (!System.Linq.Enumerable.Contains(OS.GetCmdlineUserArgs(), "--smoke")) { SetProcess(false); return; }
            _smokeStep = 0;
            StartNewGame(1960, 12345);
            return;
        }
        _smokeStep++;
        if (HasModal)
        {
            if (FindButton(_overlayLayer) is { } ok) ok.EmitSignal(BaseButton.SignalName.Pressed);
            return;
        }
        switch (_screen)
        {
            case GameScreen game: game.SmokeStep(_smokeStep); break;
            case SuccessionScreen or GameOverScreen:
                if (FindButton(_screen) is { } b) b.EmitSignal(BaseButton.SignalName.Pressed);
                break;
            case TitleScreen:
                GD.Print($"SMOKE TEST DONE after {_smokeStep} steps");
                GetTree().Quit();
                return;
        }
        if (_smokeStep >= 1500)
        {
            GD.Print($"SMOKE TEST DONE: year {Session?.Year}, player {Session?.Player.FullName}, generations {Session?.Stats().Generations}");
            GetTree().Quit();
        }
    }

    /// <summary>
    /// Development aid: --screenshots=DIR plays a few years and saves images of each screen.
    /// </summary>
    private void ScreenshotTour()
    {
        _shotDir ??= System.Linq.Enumerable.First(OS.GetCmdlineUserArgs(), a => a.StartsWith("--screenshots="))["--screenshots=".Length..];
        _shotFrame++;
        void Shot(string name) => GetViewport().GetTexture().GetImage().SavePng($"{_shotDir}/{name}.png");

        switch (_shotFrame)
        {
            case 10: Shot("01_title"); StartNewGame(1970, 777); break;
            case 20:
                for (int i = 0; i < 40 && Session is { } s; i++)
                {
                    var bot = new AutoPlayer((ulong)i);
                    bot.PlayYear(s);
                    if (s.NeedsSuccession || s.GameOver) break;
                }
                if (Session!.NeedsSuccession) ShowSuccession(); else ShowGame();
                break;
            case 30: Shot("02_year"); break;
            case 32: if (_screen is GameScreen g1) g1.ShowTab(1); break;
            case 40: Shot("03_family"); break;
            case 42: if (_screen is GameScreen g2) g2.ShowTab(2); break;
            case 50: Shot("04_tree"); break;
            case 52: if (_screen is GameScreen g3) g3.ShowTab(3); break;
            case 60: Shot("05_chronicle"); break;
            case 62: ShowSuccession(); break;
            case 70: Shot("06_succession"); GetTree().Quit(); break;
        }
    }

    private static Button? FindButton(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button { Disabled: false } b) return b;
            if (FindButton(child) is { } found) return found;
        }
        return null;
    }
}
