using System.Linq;
using Godot;
using OneMoreYear.Simulation;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Game;

/// <summary>
/// Root of the game. Owns the current <see cref="GameSession"/> and switches between screens.
/// All rules live in the simulation library; this layer only shows state and forwards choices.
/// </summary>
public partial class Main : Control
{
    public GameSession? Session { get; private set; }

    /// <summary>The game version from project.godot (application/config/version).</summary>
    public static string Version => ProjectSettings.GetSetting("application/config/version").AsString();

    private Control? _screen;
    private Control _overlayLayer = null!;
    private AlbumPaper _paper = null!;
    /// <summary>Screenshot tour only: show another decade's look.</summary>
    private int? _eraOverride;

    public override void _Ready()
    {
        Theme = UiTheme.Build();
        SetAnchorsPreset(LayoutPreset.FullRect);
        RegisterInput();
        Sound.Init(this);
        Settings.Apply();

        _paper = new AlbumPaper();
        AddChild(_paper);
        _paper.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _overlayLayer = new Control { MouseFilter = MouseFilterEnum.Ignore, ZIndex = 10 };
        _overlayLayer.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_overlayLayer);

        // Development aid: --load=path/to/save.json opens a saved situation (e.g. from a playtest note).
        var load = System.Linq.Enumerable.FirstOrDefault(OS.GetCmdlineUserArgs(), a => a.StartsWith("--load="));
        if (load != null && System.IO.File.Exists(load["--load=".Length..]))
        {
            Session = GameSession.Load(System.IO.File.ReadAllText(load["--load=".Length..]));
            if (Session.NeedsSuccession) ShowSuccession(); else ShowGame();
            return;
        }
        var probe = System.Linq.Enumerable.FirstOrDefault(OS.GetCmdlineUserArgs(), a => a.StartsWith("--styleprobe="));
        if (probe != null && OS.IsDebugBuild())
        {
            AddChild(new StyleProbe(probe["--styleprobe=".Length..]));
            SetProcess(false);
            return;
        }
        var gallery = System.Linq.Enumerable.FirstOrDefault(OS.GetCmdlineUserArgs(), a => a.StartsWith("--portraits="));
        if (gallery != null && OS.IsDebugBuild())
        {
            AddChild(new PortraitGallery(gallery["--portraits=".Length..]));
            SetProcess(false);
            return;
        }
        // --scenario=id starts a test scenario directly (development builds only, see docs/test-scenarios.md).
        var scenario = System.Linq.Enumerable.FirstOrDefault(OS.GetCmdlineUserArgs(), a => a.StartsWith("--scenario="));
        if (scenario != null && OS.IsDebugBuild())
        {
            StartNewGame(1970, null, scenario["--scenario=".Length..]);
            return;
        }

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
        Bind("omy_feedback", Key.F1, JoyButton.Back);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("omy_feedback") && !HasModal)
        {
            GetViewport().SetInputAsHandled();
            ShowFeedback();
        }
    }

    // --- Playtest feedback ----------------------------------------------------------------------

    /// <summary>
    /// F1 / Select: write a playtest note. It is appended to docs/playtest-notes.md together with the
    /// current situation, a screenshot and a copy of the save so the moment can be reopened later.
    /// </summary>
    public void ShowFeedback()
    {
        var screenshot = GetViewport().GetTexture().GetImage();
        var previousFocus = GetViewport().GuiGetFocusOwner();

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.AddChild(center);

        var box = Ui.VBox(14);
        box.CustomMinimumSize = new Vector2(720, 0);
        box.AddChild(Ui.Label("Playtest note", 26, UiTheme.Accent));
        box.AddChild(Ui.Label(FeedbackContext(), 16, UiTheme.Muted, wrap: true));
        var text = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 220),
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            PlaceholderText = "What feels right or wrong? Swedish is fine.",
        };
        box.AddChild(text);

        void Close()
        {
            _overlayLayer.RemoveChild(dim);
            dim.QueueFree();
            if (IsInstanceValid(previousFocus) && previousFocus!.IsInsideTree() && previousFocus.IsVisibleInTree())
                previousFocus.GrabFocus();
        }

        var buttons = Ui.HBox(10);
        buttons.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        buttons.AddChild(Ui.Button("Cancel", Close));
        var save = Ui.Button("Save note  (Ctrl+Enter)", () =>
        {
            if (!string.IsNullOrWhiteSpace(text.Text))
            {
                string where = SaveFeedback(text.Text.Trim(), screenshot);
                Close();
                ShowMessage("Thanks!", $"Your note was saved to {where}.");
                return;
            }
            Close();
        });
        UiTheme.MakePrimary(save);
        save.AddThemeFontSizeOverride("font_size", 18);
        buttons.AddChild(save);
        box.AddChild(buttons);

        text.GuiInput += e =>
        {
            if (e is InputEventKey { Pressed: true, Keycode: Key.Enter or Key.KpEnter, CtrlPressed: true })
            {
                text.AcceptEvent();
                save.EmitSignal(BaseButton.SignalName.Pressed);
            }
            else if (e.IsActionPressed("ui_cancel"))
            {
                text.AcceptEvent();
                Close();
            }
        };

        center.AddChild(Ui.Card(box, UiTheme.Panel, UiTheme.AccentDark));
        _overlayLayer.AddChild(dim);
        text.GrabFocus();
    }

    private string FeedbackContext()
    {
        if (Session == null) return $"Screen: {_screen?.GetType().Name}";
        var p = Session.Player;
        string where = _screen is GameScreen g ? g.CurrentTabName : _screen?.GetType().Name ?? "";
        return $"v{Version}  ·  {Session.Year}  ·  {p.FullName}, {p.Age(Session.Year)}  ·  {where}  ·  seed {Session.SeedCode}";
    }

    /// <summary>Appends the note to the notes file. Returns where it was written.</summary>
    private string SaveFeedback(string note, Image screenshot)
    {
        // When running from the source tree, write into the repo's docs folder; otherwise to user data.
        string docs = System.IO.Path.GetFullPath(System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "docs"));
        if (!System.IO.Directory.Exists(docs)) docs = OS.GetUserDataDir();
        string snapshots = System.IO.Path.Combine(docs, "playtest-saves");
        System.IO.Directory.CreateDirectory(snapshots);

        string stamp = System.DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        screenshot.SavePng(System.IO.Path.Combine(snapshots, stamp + ".png"));
        string files = $"playtest-saves/{stamp}.png";
        if (Session != null)
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(snapshots, stamp + ".json"), Session.Save());
            files += $" · playtest-saves/{stamp}.json";
        }

        string notesPath = System.IO.Path.Combine(docs, "playtest-notes.md");
        string entry = $"\n### {System.DateTime.Now:yyyy-MM-dd HH:mm} · {FeedbackContext()}\n\n{note}\n\n_{files}_\n";
        System.IO.File.AppendAllText(notesPath, entry);
        return notesPath;
    }

    private void SetScreen(Control screen)
    {
        if (_screen != null)
        {
            RemoveChild(_screen);
            _screen.QueueFree();
        }
        Ui.Clear(_overlayLayer);
        ApplyEra();
        _screen = screen;
        screen.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(screen);
        MoveChild(_overlayLayer, -1);
    }

    /// <summary>The look follows the year being played (UiTheme eras); 1970 before any game has started.</summary>
    private void ApplyEra()
    {
        int year = _eraOverride ?? Session?.Year ?? 1970;
        UiTheme.SetYear(year);
        Theme = UiTheme.Build();
        _paper.QueueRedraw();
    }

    public void ShowTitle()
    {
        var title = new TitleScreen();
        title.Init(this);
        SetScreen(title);
    }

    public void StartNewGame(int startYear, string? seedCode, string? scenarioId = null, int? slot = null)
    {
        SaveSystem.CurrentSlot = slot ?? SaveSystem.FirstEmptySlot() ?? 1;
        Session = GameSession.NewGame(new NewGameOptions { StartYear = startYear, SeedCode = seedCode, ScenarioId = scenarioId, ContentSettings = Settings.Content });
        SaveSystem.Save(Session);
        ShowGame();
        bool automated = System.Linq.Enumerable.Any(OS.GetCmdlineUserArgs(), a => a == "--smoke" || a.StartsWith("--screenshots="));
        if (!Settings.IntroSeen && !automated) ShowIntroduction();
    }

    /// <summary>Opens the game saved most recently.</summary>
    public void ContinueGame()
    {
        if (SaveSystem.LatestSlot() is { } slot) LoadSlot(slot);
        else ShowTitle();
    }

    public void LoadSlot(int slot)
    {
        Session = SaveSystem.Load(slot);
        if (Session == null) { ShowTitle(); return; }
        SaveSystem.CurrentSlot = slot;
        if (Session.GameOver) ShowGameOver();
        else if (Session.NeedsSuccession) ShowSuccession();
        else ShowGame();
    }

    /// <summary>
    /// The save slots. <paramref name="pickForNewGame"/> lets the player choose a slot to replace for a
    /// new life (all slots full); otherwise slots can be loaded or deleted.
    /// </summary>
    public void ShowSlots(System.Action<int>? pickForNewGame = null)
    {
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.AddChild(center);
        void Close() { _overlayLayer.RemoveChild(dim); dim.QueueFree(); }

        var box = Ui.VBox(12);
        box.CustomMinimumSize = new Vector2(720, 0);
        box.AddChild(Ui.Label(pickForNewGame != null ? "All slots are full" : "Load a life", 28, UiTheme.Accent));
        if (pickForNewGame != null)
            box.AddChild(Ui.Label("Choose which family to replace with the new life. That family's story will be gone.", 16, UiTheme.Muted, wrap: true));
        Control? first = null;
        for (int slot = 1; slot <= SaveSystem.Slots; slot++)
        {
            var info = SaveSystem.Info(slot);
            var row = Ui.HBox(10);
            var text = Ui.VBox(0);
            text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            text.AddChild(Ui.Label(info == null ? $"Slot {slot} – empty" : $"The {info.Family} family", 20, info == null ? UiTheme.Faint : UiTheme.Text));
            if (info != null)
                text.AddChild(Ui.Label($"{info.Year}  ·  {info.Player}, {info.Age}{(info.GameOver ? "  ·  the end" : "")}  ·  seed {info.SeedCode}" +
                                       (info.SavedAt > System.DateTime.MinValue ? $"  ·  saved {info.SavedAt:d MMM HH:mm}" : ""), 14, UiTheme.Muted));
            row.AddChild(text);
            int s = slot;
            if (info != null && pickForNewGame == null)
            {
                var load = Ui.Button("Load", () => { Close(); LoadSlot(s); }, 44);
                row.AddChild(load);
                first ??= load;
                var delete = Ui.Button("Delete", () =>
                {
                    Close();
                    ShowConfirm($"Delete the {info.Family} family?", "Their whole story will be gone for good.", () => { SaveSystem.Delete(s); ShowTitle(); });
                }, 44);
                row.AddChild(delete);
            }
            else if (info != null && pickForNewGame != null)
            {
                var replace = Ui.Button("Replace", () => { Close(); pickForNewGame(s); }, 44);
                row.AddChild(replace);
                first ??= replace;
            }
            box.AddChild(Ui.Card(row, UiTheme.Panel, UiTheme.Border));
        }
        var cancel = Ui.Button("Back", Close, 44);
        cancel.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        box.AddChild(cancel);
        center.AddChild(Ui.Card(box, UiTheme.Panel, UiTheme.AccentDark));
        _overlayLayer.AddChild(dim);
        Ui.FocusLater(first ?? cancel);
    }

    /// <summary>A yes/no question. <paramref name="onYes"/> runs if the player confirms.</summary>
    public void ShowConfirm(string title, string text, System.Action onYes)
    {
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.AddChild(center);
        void Close() { _overlayLayer.RemoveChild(dim); dim.QueueFree(); }
        var box = Ui.VBox(14);
        box.CustomMinimumSize = new Vector2(520, 0);
        box.AddChild(Ui.Label(title, 26, UiTheme.Accent));
        box.AddChild(Ui.Label(text, 18, UiTheme.Text, wrap: true));
        var buttons = Ui.HBox(10);
        buttons.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var no = Ui.Button("No", Close, 46);
        buttons.AddChild(no);
        buttons.AddChild(Ui.Button("Yes", () => { Close(); onYes(); }, 46));
        box.AddChild(buttons);
        center.AddChild(Ui.Card(box, UiTheme.Panel, UiTheme.AccentDark));
        _overlayLayer.AddChild(dim);
        Ui.FocusLater(no);
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
    /// <summary>
    /// Content settings: how each dark theme is handled. From the title screen they are the defaults
    /// for new games; in a game they change that game too (and become the new defaults).
    /// </summary>
    public void ShowContentSettings(GameSession? game, bool firstTime = false, System.Action? onClose = null)
    {
        var previousFocus = GetViewport().GuiGetFocusOwner();
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.7f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.AddChild(center);

        var box = Ui.VBox(12);
        box.CustomMinimumSize = new Vector2(760, 0);
        box.AddChild(Ui.Label(firstTime ? "Before you start" : "Content settings", 28, UiTheme.Accent));
        box.AddChild(Ui.Label(firstTime
            ? "One More Year is a game for adults. Families can be loving and warm – and they can hide abuse, violence, addiction and betrayal. " +
              "Choose how much of the dark side you want. You can change this at any time."
            : "How each dark theme is handled. \"Mentioned only\" means it can happen to others, off-screen – a line in the chronicle, never an event or a choice for you.",
            17, UiTheme.Muted, wrap: true));

        Control? first = null;
        foreach (var (id, name, description) in ContentCategories.Selectable)
        {
            var row = Ui.HBox(14);
            var text = Ui.VBox(0);
            text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            text.AddChild(Ui.Label(name, 19, UiTheme.Text));
            text.AddChild(Ui.Label(description, 14, UiTheme.Faint, wrap: true));
            row.AddChild(text);
            var pick = new OptionButton { CustomMinimumSize = new Vector2(220, 44) };
            pick.AddItem("On");
            pick.AddItem("Mentioned only");
            pick.AddItem("Off");
            pick.Selected = (int)(game?.ContentLevelOf(id) ?? Settings.ContentLevelOf(id));
            string category = id;
            pick.ItemSelected += index =>
            {
                var level = (ContentLevel)(int)index;
                Settings.SetContentLevel(category, level);
                game?.SetContentLevel(category, level);
            };
            row.AddChild(pick);
            box.AddChild(row);
            first ??= pick;
        }

        var done = Ui.Button(firstTime ? "Continue" : "Done", () =>
        {
            Settings.MarkContentAsked();
            if (game != null) AutoSave();
            _overlayLayer.RemoveChild(dim);
            dim.QueueFree();
            if (IsInstanceValid(previousFocus) && previousFocus!.IsInsideTree() && previousFocus.IsVisibleInTree())
                previousFocus.GrabFocus();
            onClose?.Invoke();
        });
        UiTheme.MakePrimary(done);
        done.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        done.CustomMinimumSize = new Vector2(180, 48);
        box.AddChild(done);
        center.AddChild(Ui.Card(box, UiTheme.Panel, UiTheme.AccentDark));
        _overlayLayer.AddChild(dim);
        Ui.FocusLater(first ?? done);
    }

    /// <summary>Screen, text size, sound, the newspaper – and the content settings, one click away.</summary>
    public void ShowSettings(GameSession? game)
    {
        var previousFocus = GetViewport().GuiGetFocusOwner();
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.AddChild(center);
        void Close()
        {
            _overlayLayer.RemoveChild(dim);
            dim.QueueFree();
            if (IsInstanceValid(previousFocus) && previousFocus!.IsInsideTree() && previousFocus.IsVisibleInTree()) previousFocus.GrabFocus();
        }

        var box = Ui.VBox(14);
        box.CustomMinimumSize = new Vector2(640, 0);
        box.AddChild(Ui.Label("Settings", 28, UiTheme.Accent));

        Control Row(string label, Control control)
        {
            var row = Ui.HBox(14);
            var l = Ui.Label(label, 18, UiTheme.Text);
            l.CustomMinimumSize = new Vector2(220, 0);
            row.AddChild(l);
            control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(control);
            return row;
        }
        OptionButton Options(string[] items, int selected, System.Action<int> changed)
        {
            var o = new OptionButton { CustomMinimumSize = new Vector2(0, 44) };
            foreach (var item in items) o.AddItem(item);
            o.Selected = selected;
            o.ItemSelected += i => changed((int)i);
            return o;
        }
        HSlider Slider(double value, System.Action<double> changed)
        {
            var s = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = value, CustomMinimumSize = new Vector2(0, 36), FocusMode = FocusModeEnum.All };
            s.ValueChanged += v => { changed(v); Sound.Play("click"); };
            return s;
        }

        var screen = Options(new[] { "Window", "Fullscreen" }, Settings.Fullscreen ? 1 : 0, i => Settings.SetFullscreen(i == 1));
        box.AddChild(Row("Screen", screen));
        box.AddChild(Row("Text size", Options(new[] { "Small", "Normal", "Large", "Extra large" }, Settings.TextSize, Settings.SetTextSize)));
        box.AddChild(Row("Volume", Slider(Settings.MasterVolume, Settings.SetMasterVolume)));
        box.AddChild(Row("Sound effects", Slider(Settings.EffectsVolume, Settings.SetEffectsVolume)));
        box.AddChild(Row("The family newspaper", Options(new[] { "Every year", "Only in big years", "Never" }, (int)Settings.Newspaper,
            i => Settings.SetNewspaper((NewspaperMode)i))));

        var buttons = Ui.HBox(10);
        buttons.AddChild(Ui.Button("Content settings…", () => { Close(); ShowContentSettings(game); }, 46));
        buttons.AddChild(Ui.Button("How to play", () => { Close(); ShowIntroduction(); }, 46));
        buttons.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var done = Ui.Button("Done", Close, 46);
        UiTheme.MakePrimary(done);
        done.AddThemeFontSizeOverride("font_size", 18);
        buttons.AddChild(done);
        box.AddChild(buttons);

        center.AddChild(Ui.Card(box, UiTheme.Panel, UiTheme.AccentDark));
        _overlayLayer.AddChild(dim);
        Ui.FocusLater(screen);
    }

    /// <summary>A short introduction, the first time a life begins (and from Settings → How to play).</summary>
    public void ShowIntroduction()
    {
        string[][] pages =
        {
            new[] { "Welcome to the family", "You live one life at a time – from birth to death – and then the story continues with someone you leave behind: a child, a sibling, a niece. The family is the real hero. See how far it goes." },
            new[] { "One year at a time", "Each year, things happen. Answer them on the This Year tab – there is no right answer, only a life. You also have time for a few things of your own: see people, work, study, love, fight, make money, or break the law." },
            new[] { "Who you are matters", "Your traits and gifts change the odds and what you notice. A charming person has other ways out than a hot-tempered one. People remember what you do to them – and they tell others." },
            new[] { "When you're ready", "Press Next Year (N, or Y on a controller). Some years bring the family newspaper. When your life ends, choose who carries the story on. Everything is saved as you go." },
        };
        int page = 0;
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.AddChild(center);
        var box = Ui.VBox(16);
        box.CustomMinimumSize = new Vector2(620, 0);
        var title = Ui.Label("", 30, UiTheme.Accent);
        var text = Ui.Label("", 19, UiTheme.Text, wrap: true);
        var count = UiTheme.HandLabel("", 20, UiTheme.Muted);
        box.AddChild(title);
        box.AddChild(text);
        var buttons = Ui.HBox(10);
        buttons.AddChild(count);
        buttons.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        Button next = null!;
        void Show()
        {
            title.Text = pages[page][0];
            text.Text = pages[page][1];
            count.Text = $"{page + 1} / {pages.Length}";
            next.Text = page == pages.Length - 1 ? "Begin" : "Next";
        }
        void Close()
        {
            Settings.MarkIntroSeen();
            _overlayLayer.RemoveChild(dim);
            dim.QueueFree();
            if (_screen is GameScreen g) g.FocusAfterNewspaper();
        }
        buttons.AddChild(Ui.Button("Skip", Close, 46));
        next = Ui.Button("Next", () => { if (page < pages.Length - 1) { page++; Sound.Play("page"); Show(); } else Close(); }, 46);
        UiTheme.MakePrimary(next);
        next.AddThemeFontSizeOverride("font_size", 18);
        next.CustomMinimumSize = new Vector2(160, 46);
        buttons.AddChild(next);
        box.AddChild(buttons);
        Show();
        center.AddChild(Ui.Card(box, UiTheme.Panel, UiTheme.AccentDark));
        _overlayLayer.AddChild(dim);
        Ui.FocusLater(next);
    }

    /// <summary>A new year begins: the family's newspaper, on top of the (already updated) game screen.</summary>
    public void ShowNewspaper(YearReport report)
    {
        if (Session == null || Settings.Newspaper == NewspaperMode.Never) return;
        // By default only years with front-page news: big family events or history.
        if (Settings.Newspaper == NewspaperMode.BigYears && !report.IsBigYear) return;

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.AddChild(center);
        bool closed = false;
        void Close()
        {
            if (closed) return;
            closed = true;
            _overlayLayer.RemoveChild(dim);
            dim.QueueFree();
            if (_screen is GameScreen game) game.FocusAfterNewspaper();
        }
        Sound.Play("paper");
        center.AddChild(Newspaper.Build(Session, report, Close));
        // A click anywhere outside the paper closes it too.
        dim.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) Close(); };
        _overlayLayer.AddChild(dim);
    }

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
            StartNewGame(1960, "12345");
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
            case 10: Shot("01_title"); StartNewGame(1970, "777"); break;
            case 20:
                for (int i = 0; i < 40 && Session is { } s; i++)
                {
                    var bot = new AutoPlayer((ulong)i);
                    bot.PlayYear(s);
                    if (s.NeedsSuccession || s.GameOver) break;
                }
                if (Session!.NeedsSuccession) ShowSuccession(); else ShowGame();
                break;
            case 24: ShowNewspaper(new YearReport(Session!.Year, Session.Player.Age(Session.Year), Session.NewsThisYear(), false)); break;
            case 27:
                Shot("02_newspaper");
                Ui.Clear(_overlayLayer);
                // Show passive checks and a trait choice in the year view.
                Session!.World.PendingEvents.Clear();
                Session.Player.Traits.Add("paranoid");
                Session.Player.Traits.Add("charming");
                OneMoreYear.Simulation.Systems.EventSystem.QueueSituation(Session.Ctx, "old_scam");
                OneMoreYear.Simulation.Systems.EventSystem.QueueSituation(Session.Ctx, "life_speeding");
                ShowGame();
                break;
            case 30: Shot("02_year"); break;
            case 32: if (_screen is GameScreen g1) g1.ShowTab(1); break;
            case 40: Shot("03_family"); break;
            case 42: if (_screen is GameScreen g2) g2.ShowTab(2); break;
            case 50: Shot("04_work"); break;
            case 52: if (_screen is GameScreen g3) g3.ShowTab(3); break;
            case 60: Shot("05_money"); break;
            case 62: if (_screen is GameScreen g4) g4.ShowTab(4); break;
            case 70: Shot("06_tree"); if (_screen is GameScreen gt) gt.FocusTreeOnGrandfather(); break;
            case 71: Shot("06b_tree_grandfather"); break;
            case 72: ShowContentSettings(Session); break;
            case 75: Shot("07_content"); foreach (var c in _overlayLayer.GetChildren()) c.QueueFree(); ShowSettings(Session); break;
            case 77: Shot("07b_settings"); foreach (var c in _overlayLayer.GetChildren()) c.QueueFree(); ShowIntroduction(); break;
            case 79: Shot("07c_intro"); foreach (var c in _overlayLayer.GetChildren()) c.QueueFree(); break;
            case 80: ShowSuccession(); break;
            case 82: Shot("08_succession"); _eraOverride = 1956; ShowGame(); break;
            case 86: Shot("09_era_1956"); _eraOverride = 1987; ShowGame(); break;
            case 90: Shot("10_era_1987"); GetTree().Quit(); break;
        }
    }

    private static T? FindChild<T>(Node root) where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T t) return t;
            if (FindChild<T>(child) is { } found) return found;
        }
        return null;
    }

    private static Button? FindButtonNamed(Node root, string prefix)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button b && b.Text.StartsWith(prefix)) return b;
            if (FindButtonNamed(child, prefix) is { } found) return found;
        }
        return null;
    }

    private static Button? FindButton(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            // Plain buttons only: pressing a drop-down (OptionButton) would just open it.
            if (child is Button { Disabled: false } b && b is not OptionButton) return b;
            if (FindButton(child) is { } found) return found;
        }
        return null;
    }
}
