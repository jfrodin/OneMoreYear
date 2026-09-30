using System.Collections.Generic;
using System.Linq;
using Godot;
using OneMoreYear.Simulation;

namespace OneMoreYear.Game;

/// <summary>
/// The main play screen: the player's card on the left, tabs for this year, family, family tree and
/// chronicle in the middle. Every function is reachable with mouse, keyboard and controller;
/// hints for the focused control are shown in the hint bar, never only on hover.
/// </summary>
public partial class GameScreen : Control
{
    private Main _main = null!;
    private GameSession S => _main.Session!;

    private VBoxContainer _sidebar = null!;
    private Button _nextYear = null!;
    private TabContainer _tabs = null!;
    private VBoxContainer _yearContent = null!;
    private VBoxContainer _peopleList = null!;
    private VBoxContainer _personDetail = null!;
    private Tree _tree = null!;
    private VBoxContainer _chronicleList = null!;
    private OptionButton _chronicleFilter = null!;
    private Label _hint = null!;

    private YearReport? _lastReport;
    private int? _selectedId;
    private readonly Dictionary<int, Button> _personButtons = new();

    private const int TabYear = 0, TabFamily = 1, TabTree = 2, TabChronicle = 3;

    public void Init(Main main) => _main = main;

    public override void _Ready()
    {
        var root = Ui.HBox(18);
        var margin = Ui.Margin(root, 20);
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(margin);

        var sideCard = new PanelContainer { CustomMinimumSize = new Vector2(370, 0) };
        _sidebar = Ui.VBox(12);
        sideCard.AddChild(_sidebar);
        root.AddChild(sideCard);

        var center = Ui.VBox(8);
        center.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(center);

        _tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        center.AddChild(_tabs);

        _hint = Ui.Label(" ", 16, UiTheme.Muted, wrap: true);
        _hint.CustomMinimumSize = new Vector2(100, 26);
        center.AddChild(_hint);

        // This year
        _yearContent = Ui.VBox(16);
        _tabs.AddChild(Ui.Scroll(Ui.Margin(_yearContent, 4)));
        _tabs.SetTabTitle(TabYear, "This Year");

        // Family & friends
        var split = new HSplitContainer();
        _peopleList = Ui.VBox(6);
        var listScroll = Ui.Scroll(_peopleList);
        listScroll.CustomMinimumSize = new Vector2(400, 0);
        listScroll.SizeFlagsHorizontal = SizeFlags.Fill;
        split.AddChild(listScroll);
        _personDetail = Ui.VBox(14);
        split.AddChild(Ui.Scroll(Ui.Margin(_personDetail, 8)));
        _tabs.AddChild(split);
        _tabs.SetTabTitle(TabFamily, "Family & Friends");

        // Family tree
        _tree = new Tree { HideRoot = true, SizeFlagsVertical = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.All };
        _tree.ItemActivated += OnTreeActivated;
        _tabs.AddChild(_tree);
        _tabs.SetTabTitle(TabTree, "Family Tree");

        // Chronicle
        var chronicle = Ui.VBox(10);
        var filterRow = Ui.HBox(12);
        filterRow.AddChild(Ui.Label("Show", 17, UiTheme.Muted));
        _chronicleFilter = new OptionButton { CustomMinimumSize = new Vector2(240, 44) };
        _chronicleFilter.AddItem("Major events");
        _chronicleFilter.AddItem("Notable events");
        _chronicleFilter.AddItem("Everything");
        _chronicleFilter.Selected = 1;
        _chronicleFilter.ItemSelected += _ => RefreshChronicle();
        RegisterHint(_chronicleFilter, "Choose how much of the family's history to show.");
        filterRow.AddChild(_chronicleFilter);
        chronicle.AddChild(filterRow);
        _chronicleList = Ui.VBox(4);
        chronicle.AddChild(Ui.Scroll(_chronicleList));
        _tabs.AddChild(chronicle);
        _tabs.SetTabTitle(TabChronicle, "Chronicle");

        _tabs.TabChanged += OnTabChanged;

        RefreshAll();
        FocusDefault();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_main.HasModal) return;
        if (e.IsActionPressed("omy_next_year"))
        {
            OnNextYear();
            GetViewport().SetInputAsHandled();
        }
        else if (e.IsActionPressed("omy_tab_next") || e.IsActionPressed("omy_tab_prev"))
        {
            int dir = e.IsActionPressed("omy_tab_next") ? 1 : -1;
            _tabs.CurrentTab = (_tabs.CurrentTab + dir + _tabs.GetTabCount()) % _tabs.GetTabCount();
            GetViewport().SetInputAsHandled();
        }
    }

    // --- Refreshing -------------------------------------------------------------------------

    private void RefreshAll()
    {
        RefreshSidebar();
        RefreshYear();
        RefreshPeople();
        if (_tabs.CurrentTab == TabTree) RefreshTree();
        if (_tabs.CurrentTab == TabChronicle) RefreshChronicle();
    }

    private void OnTabChanged(long tab)
    {
        if (tab == TabTree) RefreshTree();
        if (tab == TabChronicle) RefreshChronicle();
        if (tab == TabFamily) RefreshPeople();
        FocusDefault();
    }

    private void FocusDefault()
    {
        Control? target = _tabs.CurrentTab switch
        {
            TabYear => FirstEnabledButton(_yearContent, "choice") ?? (_nextYear.Disabled ? null : _nextYear),
            TabFamily => _selectedId is { } id && _personButtons.TryGetValue(id, out var b) ? b : _personButtons.Values.FirstOrDefault(),
            TabTree => _tree,
            TabChronicle => _chronicleFilter,
            _ => null
        };
        Ui.FocusLater(target ?? _nextYear);
    }

    private static Button? FirstEnabledButton(Node root, string meta)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button b && !b.Disabled && b.HasMeta(meta)) return b;
            if (FirstEnabledButton(child, meta) is { } found) return found;
        }
        return null;
    }

    private void RegisterHint(Control control, string? hint)
    {
        control.FocusEntered += () => _hint.Text = hint ?? " ";
        control.MouseEntered += () => _hint.Text = hint ?? " ";
        if (!string.IsNullOrEmpty(hint)) control.TooltipText = hint;
    }

    // --- Sidebar ----------------------------------------------------------------------------

    private void RefreshSidebar()
    {
        Ui.Clear(_sidebar);
        var p = S.Describe(S.Player.Id);

        var top = Ui.HBox(14);
        top.AddChild(Portrait.Create(p.Id, p.Name, p.Alive, true, 84));
        var nameCol = Ui.VBox(2);
        nameCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        nameCol.AddChild(Ui.Label(p.Name, 24, UiTheme.Text, wrap: true));
        nameCol.AddChild(Ui.Label($"Age {p.Age}  ·  {S.Year}", 18, UiTheme.Accent));
        nameCol.AddChild(Ui.Label(p.Occupation, 16, UiTheme.Muted, wrap: true));
        top.AddChild(nameCol);
        _sidebar.AddChild(top);

        if (!string.IsNullOrEmpty(p.Partner)) _sidebar.AddChild(Ui.Label(p.Partner, 16, UiTheme.Muted, wrap: true));

        var traits = new HFlowContainer();
        traits.AddThemeConstantOverride("h_separation", 6);
        traits.AddThemeConstantOverride("v_separation", 6);
        foreach (var (name, description) in p.Traits)
        {
            var chip = Ui.Chip(name, UiTheme.Accent);
            chip.TooltipText = description;
            traits.AddChild(chip);
        }
        _sidebar.AddChild(traits);
        _sidebar.AddChild(Ui.Separator());

        _sidebar.AddChild(Ui.Bar("Health", p.Health, p.Health >= 50 ? UiTheme.Good : UiTheme.Bad, p.HealthLabel));
        _sidebar.AddChild(Ui.Bar("Happiness", p.Happiness, UiTheme.Info, $"{p.Happiness:0}"));

        _sidebar.AddChild(StatRow("Money", p.Money, S.Player.Money < 0 ? UiTheme.Bad : UiTheme.Text));
        _sidebar.AddChild(StatRow("Income", p.Income, UiTheme.Text));
        _sidebar.AddChild(StatRow("Education", p.Education, UiTheme.Text));
        _sidebar.AddChild(StatRow("Home", p.OwnsHome ? "Owns a home" : "Renting", UiTheme.Text));

        _sidebar.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

        int ap = S.World.ActionPoints;
        _sidebar.AddChild(Ui.Label(ap > 0 ? $"You have time for {ap} more thing{(ap == 1 ? "" : "s")} this year." : "No time left for more this year.",
            16, UiTheme.Muted, wrap: true));

        _nextYear = Ui.Button($"Next Year  ({S.Year + 1})", OnNextYear, 66);
        UiTheme.MakePrimary(_nextYear);
        _nextYear.Disabled = !S.CanAdvance;
        RegisterHint(_nextYear, "Let a year pass. [N] / (Y)");
        _sidebar.AddChild(_nextYear);
        if (S.HasUnresolvedEvents)
            _sidebar.AddChild(Ui.Label("Answer this year's events first.", 15, UiTheme.Accent));

        _sidebar.AddChild(Ui.Label("N / (Y) next year  ·  Q E / LB RB switch tabs", 13, UiTheme.Faint, wrap: true));
        var menu = Ui.Button("Save & exit to menu", () => { _main.AutoSave(); _main.ShowTitle(); }, 42);
        _sidebar.AddChild(menu);
    }

    private static Control StatRow(string label, string value, Color color)
    {
        var row = Ui.HBox(12);
        var l = Ui.Label(label, 16, UiTheme.Muted);
        l.CustomMinimumSize = new Vector2(110, 0);
        row.AddChild(l);
        var v = Ui.Label(value, 17, color, wrap: true);
        row.AddChild(v);
        return row;
    }

    // --- This year ----------------------------------------------------------------------------

    private void RefreshYear()
    {
        Ui.Clear(_yearContent);
        var player = S.Player;

        var header = Ui.HBox(16);
        header.AddChild(Ui.Label(S.Year.ToString(), 44, UiTheme.Accent));
        var sub = Ui.Label($"{player.FirstName} is {player.Age(S.Year)}.", 20, UiTheme.Muted);
        sub.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(sub);
        _yearContent.AddChild(header);

        // What happened
        var news = _lastReport?.Year == S.Year
            ? _lastReport.News
            : S.Chronicle().Where(l => l.Year == S.Year).ToList();
        if (news.Count > 0)
        {
            var newsBox = Ui.VBox(6);
            newsBox.AddChild(Ui.Label("What happened", 20, UiTheme.Text));
            foreach (var line in news.OrderByDescending(l => l.Importance))
            {
                var (size, color) = line.Importance switch
                {
                    3 => (19, UiTheme.Text),
                    2 => (18, new Color("cfc8ba")),
                    _ => (16, UiTheme.Muted)
                };
                if (line.Category == "world") color = UiTheme.Info;
                newsBox.AddChild(Ui.Label("•  " + line.Text, size, color, wrap: true));
            }
            _yearContent.AddChild(Ui.Card(newsBox));
        }

        // Decisions
        var events = S.CurrentEvents();
        foreach (var ev in events) _yearContent.AddChild(BuildEventCard(ev));
        if (events.Count == 0)
            _yearContent.AddChild(Ui.Label("A quiet year. Spend your time on something below, visit your family – or let the year pass.", 18, UiTheme.Muted, wrap: true));

        // Your own life
        var actions = S.Actions(null);
        if (actions.Count > 0)
        {
            var box = Ui.VBox(10);
            box.AddChild(Ui.Label("Your life", 20, UiTheme.Text));
            box.AddChild(ActionButtons(actions, null));
            _yearContent.AddChild(Ui.Card(box));
        }
    }

    private Control BuildEventCard(EventView ev)
    {
        var box = Ui.VBox(10);
        box.AddChild(Ui.Label(ev.Title, 24, UiTheme.Accent));
        if (ev.TargetId is { } tid)
        {
            var t = S.Describe(tid);
            var who = Ui.HBox(10);
            who.AddChild(Portrait.Create(t.Id, t.Name, t.Alive, false, 36));
            var whoLabel = Ui.Label($"{t.Name}  ·  {t.RoleLabel}, {t.Age}", 16, UiTheme.Muted);
            whoLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            who.AddChild(whoLabel);
            box.AddChild(who);
        }
        box.AddChild(Ui.Label(ev.Text, 19, UiTheme.Text, wrap: true));

        if (!ev.Resolved)
        {
            foreach (var c in ev.Choices)
            {
                string text = c.Text + (c.ChancePercent is { } pc ? $"     {pc}% chance" : "");
                var uid = ev.Uid;
                var index = c.Index;
                var b = Ui.Button(text, () => OnChoose(uid, index), 52);
                b.Alignment = HorizontalAlignment.Left;
                b.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                b.Disabled = !c.Available;
                b.SetMeta("choice", true);
                RegisterHint(b, c.Available ? c.Hint : "Not possible right now.");
                box.AddChild(b);
            }
        }
        else if (!string.IsNullOrWhiteSpace(ev.OutcomeText))
        {
            box.AddChild(Ui.Label(ev.OutcomeText, 18, UiTheme.Info, wrap: true));
        }

        return Ui.Card(box, ev.Resolved ? UiTheme.Panel : UiTheme.PanelAlt, ev.Resolved ? UiTheme.Border : UiTheme.AccentDark);
    }

    private void OnChoose(int uid, int index)
    {
        S.Choose(uid, index);
        _main.AutoSave();
        if (S.NeedsSuccession) { _main.ShowSuccession(); return; }
        RefreshAll();
        FocusDefault();
    }

    private void OnNextYear()
    {
        if (!S.CanAdvance)
        {
            if (S.HasUnresolvedEvents)
            {
                _tabs.CurrentTab = TabYear;
                FocusDefault();
            }
            return;
        }
        _lastReport = S.AdvanceYear();
        _main.AutoSave();
        if (S.NeedsSuccession) { _main.ShowSuccession(); return; }
        _tabs.CurrentTab = TabYear;
        RefreshAll();
        FocusDefault();
    }

    // --- Actions ------------------------------------------------------------------------------

    private Control ActionButtons(IReadOnlyList<ActionView> actions, int? targetId)
    {
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 8);
        flow.AddThemeConstantOverride("v_separation", 8);
        foreach (var a in actions)
        {
            string text = a.Title + (a.ChancePercent is { } pc ? $"  ·  {pc}%" : "");
            var id = a.Id;
            var b = Ui.Button(text, () => OnAction(id, targetId), 46);
            b.Disabled = !a.Enabled;
            b.SetMeta("action", true);
            string hint = a.Hint ?? a.Title;
            if (!a.Enabled) hint = S.World.ActionPoints <= 0 ? "You have no time left this year." : "Already done this year.";
            RegisterHint(b, hint);
            flow.AddChild(b);
        }
        return flow;
    }

    private void OnAction(string actionId, int? targetId)
    {
        var title = S.Actions(targetId).FirstOrDefault(a => a.Id == actionId)?.Title ?? "";
        var result = S.PerformAction(actionId, targetId);
        _main.AutoSave();
        _main.ShowMessage(title, result, () =>
        {
            if (S.NeedsSuccession) { _main.ShowSuccession(); return; }
            RefreshAll();
            FocusDefault();
        });
    }

    // --- Family & friends -------------------------------------------------------------------

    private void RefreshPeople()
    {
        Ui.Clear(_peopleList);
        _personButtons.Clear();
        var people = new List<PersonView> { S.Describe(S.Player.Id) };
        people.AddRange(S.Family());
        _selectedId ??= S.Player.Id;
        if (!S.World.TryGet(_selectedId)?.IsAlive ?? true) _selectedId = S.Player.Id;

        foreach (var p in people) _peopleList.AddChild(PersonRow(p));
        RefreshDetail();
    }

    private Button PersonRow(PersonView p)
    {
        var b = new Button { CustomMinimumSize = new Vector2(0, 70), FocusMode = FocusModeEnum.All };
        if (p.Id == _selectedId)
            b.AddThemeStyleboxOverride("normal", UiTheme.Box(UiTheme.PanelHover, 8, UiTheme.AccentDark, 1));

        var row = Ui.HBox(12);
        row.AddChild(Portrait.Create(p.Id, p.Name, p.Alive, p.Id == S.Player.Id, 48));
        var col = Ui.VBox(2);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        col.AddChild(Ui.Label(p.Name, 18, UiTheme.Text));
        var sub = Ui.HBox(8);
        sub.AddChild(Ui.Label($"{p.RoleLabel}  ·  {p.Age}", 15, UiTheme.Muted));
        if (p.TowardsPlayer is { } rel)
            sub.AddChild(Ui.Label(rel.OpinionLabel == "Neutral" ? "Neutral" : rel.OpinionLabel + " you", 15, Ui.OpinionColor(rel.Opinion)));
        col.AddChild(sub);
        row.AddChild(col);

        var margin = Ui.Margin(row, 10);
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        b.AddChild(margin);
        Ui.PassMouse(b);

        int id = p.Id;
        b.Pressed += () => SelectPerson(id);
        b.FocusEntered += () =>
        {
            _hint.Text = $"{p.Name} – press to see details and things you can do together.";
            if (_selectedId != id) SelectPerson(id, keepFocus: true);
        };
        _personButtons[p.Id] = b;
        return b;
    }

    private void SelectPerson(int id, bool keepFocus = false)
    {
        var previous = _selectedId;
        _selectedId = id;
        if (previous is { } prev && _personButtons.TryGetValue(prev, out var pb)) pb.RemoveThemeStyleboxOverride("normal");
        if (_personButtons.TryGetValue(id, out var nb))
            nb.AddThemeStyleboxOverride("normal", UiTheme.Box(UiTheme.PanelHover, 8, UiTheme.AccentDark, 1));
        RefreshDetail();
        if (!keepFocus && FirstEnabledButton(_personDetail, "action") is { } first) Ui.FocusLater(first);
    }

    private void RefreshDetail()
    {
        Ui.Clear(_personDetail);
        if (_selectedId is not { } id) return;
        var p = S.Describe(id);
        bool isPlayer = id == S.Player.Id;

        var header = Ui.HBox(16);
        header.AddChild(Portrait.Create(p.Id, p.Name, p.Alive, isPlayer, 96));
        var col = Ui.VBox(3);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        col.AddChild(Ui.Label(p.Name, 28, UiTheme.Text, wrap: true));
        col.AddChild(Ui.Label(p.Alive ? $"{p.RoleLabel}  ·  Age {p.Age}" : $"{p.RoleLabel}  ·  {p.BirthYear}–{p.DeathYear}", 18, UiTheme.Accent));
        col.AddChild(Ui.Label(p.Occupation, 16, UiTheme.Muted, wrap: true));
        if (!string.IsNullOrEmpty(p.Partner)) col.AddChild(Ui.Label(p.Partner, 16, UiTheme.Muted, wrap: true));
        header.AddChild(col);
        _personDetail.AddChild(header);

        // Personality – descriptions are always visible, not hidden in tooltips.
        var traitBox = Ui.VBox(4);
        foreach (var (name, description) in p.Traits)
        {
            var row = Ui.HBox(10);
            row.AddChild(Ui.Chip(name, UiTheme.Accent));
            var d = Ui.Label(description, 15, UiTheme.Muted, wrap: true);
            d.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(d);
            traitBox.AddChild(row);
        }
        _personDetail.AddChild(traitBox);

        if (!isPlayer && p.TowardsPlayer is { } r)
        {
            var relBox = Ui.VBox(6);
            relBox.AddChild(Ui.Label($"How {p.FirstName} feels about you:  {r.OpinionLabel}", 19, Ui.OpinionColor(r.Opinion)));
            relBox.AddChild(Ui.Bar("Closeness", r.Closeness, UiTheme.Good));
            relBox.AddChild(Ui.Bar("Trust", r.Trust, UiTheme.Info));
            relBox.AddChild(Ui.Bar("Respect", r.Respect, UiTheme.Accent));
            if (r.Attraction > 1) relBox.AddChild(Ui.Bar("Attraction", r.Attraction, new Color("d98cb3")));
            if (r.Bitterness > 1) relBox.AddChild(Ui.Bar("Bitterness", r.Bitterness, UiTheme.Bad));
            if (r.Envy > 1) relBox.AddChild(Ui.Bar("Envy", r.Envy, UiTheme.Bad));
            if (r.Fear > 1) relBox.AddChild(Ui.Bar("Fear", r.Fear, UiTheme.Bad));
            if (p.FromPlayer is { } mine)
                relBox.AddChild(Ui.Label($"You feel:  {mine.OpinionLabel}", 16, Ui.OpinionColor(mine.Opinion)));
            _personDetail.AddChild(Ui.Card(relBox));
        }
        else if (isPlayer)
        {
            _personDetail.AddChild(Ui.Label($"Money {p.Money}  ·  Income {p.Income}", 16, UiTheme.Muted));
        }

        if (p.Memories.Count > 0)
        {
            var memBox = Ui.VBox(4);
            memBox.AddChild(Ui.Label(isPlayer ? "Your memories" : $"{p.FirstName} remembers", 19, UiTheme.Text));
            foreach (var m in p.Memories)
            {
                var color = m.Impact < -3 ? UiTheme.Bad : m.Impact > 3 ? UiTheme.Good : UiTheme.Muted;
                memBox.AddChild(Ui.Label($"{m.Year}   {m.Text}", 16, color, wrap: true));
            }
            _personDetail.AddChild(Ui.Card(memBox));
        }

        if (p.Alive)
        {
            var actions = S.Actions(isPlayer ? null : id);
            if (actions.Count > 0)
            {
                _personDetail.AddChild(Ui.Label(isPlayer ? "Your life" : $"With {p.FirstName}", 19, UiTheme.Text));
                _personDetail.AddChild(ActionButtons(actions, isPlayer ? null : id));
            }
        }
    }

    // --- Family tree ------------------------------------------------------------------------

    private void RefreshTree()
    {
        _tree.Clear();
        var root = _tree.CreateItem();
        foreach (var node in S.FamilyTree()) AddTreeNode(root, node);
    }

    private void AddTreeNode(TreeItem parent, TreeNode node)
    {
        var item = _tree.CreateItem(parent);
        string text = node.Label + (node.IsPlayer ? "   (you)" : node.Played ? "   (played)" : "");
        if (node.Partners.Count > 0) text += "     with " + string.Join(", ", node.Partners);
        item.SetText(0, text);
        item.SetMetadata(0, node.Id);
        var color = node.IsPlayer ? UiTheme.Accent : node.Played ? new Color("f0d9a8") : node.Alive ? UiTheme.Text : UiTheme.Muted;
        item.SetCustomColor(0, color);
        foreach (var child in node.Children) AddTreeNode(item, child);
    }

    private void OnTreeActivated()
    {
        var item = _tree.GetSelected();
        if (item == null) return;
        int id = item.GetMetadata(0).AsInt32();
        if (S.World.TryGet(id) is not { IsAlive: true }) return;
        _selectedId = id;
        _tabs.CurrentTab = TabFamily;
    }

    // --- Smoke test (automated run through the real UI, see Main) ------------------------

    public void ShowTab(int tab) => _tabs.CurrentTab = tab;

    public void SmokeStep(int step)
    {
        _tabs.CurrentTab = step % 4;
        if (FirstEnabledButton(_yearContent, "choice") is { } choice) { choice.EmitSignal(BaseButton.SignalName.Pressed); return; }
        if (step % 3 == 0 && FirstEnabledButton(_personDetail, "action") is { } action) { action.EmitSignal(BaseButton.SignalName.Pressed); return; }
        OnNextYear();
    }

    // --- Chronicle ----------------------------------------------------------------------------

    private void RefreshChronicle()
    {
        Ui.Clear(_chronicleList);
        int minImportance = 3 - _chronicleFilter.Selected;
        var lines = S.Chronicle(minImportance).Reverse().Take(1500).ToList();
        int? year = null;
        foreach (var line in lines)
        {
            if (line.Year != year)
            {
                year = line.Year;
                _chronicleList.AddChild(Ui.Spacer(6));
                _chronicleList.AddChild(Ui.Label(line.Year.ToString(), 22, UiTheme.Accent));
            }
            var color = line.Category == "world" ? UiTheme.Info : line.Importance >= 3 ? UiTheme.Text : UiTheme.Muted;
            _chronicleList.AddChild(Ui.Label("   " + line.Text, 17, color, wrap: true));
        }
    }
}
