using System;
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
    private VBoxContainer _treeTab = null!;
    private FamilyTreeView? _treeView;
    private int? _treeFocusId;
    private VBoxContainer _chronicleList = null!;
    private OptionButton _chronicleFilter = null!;
    private Label _hint = null!;

    private int? _selectedId;
    private readonly Dictionary<int, Button> _personButtons = new();

    private const int TabYear = 0, TabFamily = 1, TabWork = 2, TabMoney = 3, TabTree = 4, TabChronicle = 5;
    private VBoxContainer _workContent = null!;
    private VBoxContainer _moneyContent = null!;

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
        _tabs.SetTabTitle(TabFamily, "People");

        // School & work
        _workContent = Ui.VBox(14);
        _tabs.AddChild(Ui.Scroll(Ui.Margin(_workContent, 4)));
        _tabs.SetTabTitle(TabWork, "School & Work");

        // Money
        _moneyContent = Ui.VBox(14);
        _tabs.AddChild(Ui.Scroll(Ui.Margin(_moneyContent, 4)));
        _tabs.SetTabTitle(TabMoney, "Money");

        // Family tree
        _treeTab = Ui.VBox(8);
        _tabs.AddChild(_treeTab);
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
        if (_tabs.CurrentTab == TabWork) RefreshWork();
        if (_tabs.CurrentTab == TabMoney) RefreshMoney();
        if (_tabs.CurrentTab == TabTree) RefreshTree();
        if (_tabs.CurrentTab == TabChronicle) RefreshChronicle();
    }

    private void OnTabChanged(long tab)
    {
        Sound.Play("page");
        if (tab == TabTree) RefreshTree();
        if (tab == TabChronicle) RefreshChronicle();
        if (tab == TabFamily) RefreshPeople();
        if (tab == TabWork) RefreshWork();
        if (tab == TabMoney) RefreshMoney();
        FocusDefault();
    }

    private void FocusDefault()
    {
        Control? target = _tabs.CurrentTab switch
        {
            TabYear => FirstEnabledButton(_yearContent, "choice") ?? (_nextYear.Disabled ? null : _nextYear),
            TabFamily => _selectedId is { } id && _personButtons.TryGetValue(id, out var b) ? b : _personButtons.Values.FirstOrDefault(),
            TabWork => FirstEnabledButton(_workContent, "action") ?? (Control?)_nextYear,
            TabMoney => FirstEnabledButton(_moneyContent, "action") ?? (Control?)_nextYear,
            TabTree => _treeView?.FocusCard,
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
        top.AddChild(Portrait.Create(S.Portrait(p.Id), true, 84));
        var nameCol = Ui.VBox(2);
        nameCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        nameCol.AddChild(Ui.Label(p.Name, 24, UiTheme.Text, wrap: true));
        nameCol.AddChild(Ui.Label($"Age {p.Age}  ·  {S.Year}", 18, UiTheme.Accent));
        nameCol.AddChild(Ui.Label(p.Occupation, 16, UiTheme.Muted, wrap: true));
        if (p.Fame != null) nameCol.AddChild(Ui.Label(p.Fame, 15, UiTheme.Accent));
        top.AddChild(nameCol);
        _sidebar.AddChild(top);

        if (!string.IsNullOrEmpty(p.Partner)) _sidebar.AddChild(Ui.Label(p.Partner, 16, UiTheme.Muted, wrap: true));
        if (p.Dream != null) _sidebar.AddChild(UiTheme.HandLabel(p.Dream, 21, UiTheme.Accent, wrap: true));
        if (S.Wish() is { } wish)
        {
            var wishLabel = Ui.Label(wish.Kept ? $"Wish kept: {wish.Text}" : $"This year's wish: {wish.Text}", 15, wish.Kept ? UiTheme.Good : UiTheme.Muted, wrap: true);
            wishLabel.TooltipText = "A small thing you want this year. Doing it makes you a little happier.";
            _sidebar.AddChild(wishLabel);
        }
        if (p.Hobby != null) _sidebar.AddChild(Ui.Label($"Hobby: {p.Hobby}", 14, UiTheme.Faint));
        foreach (var pet in p.Pets) _sidebar.AddChild(Ui.Label(pet, 14, UiTheme.Faint, wrap: true));
        if (p.Condition != null) _sidebar.AddChild(Ui.Label(p.Condition, 16, UiTheme.Bad, wrap: true));

        var traits = new HFlowContainer();
        traits.AddThemeConstantOverride("h_separation", 6);
        traits.AddThemeConstantOverride("v_separation", 6);
        foreach (var (name, description, tone) in p.Traits)
        {
            var chip = Ui.Chip(name, Ui.ToneColor(tone));
            chip.TooltipText = description;
            traits.AddChild(chip);
        }
        _sidebar.AddChild(traits);
        _sidebar.AddChild(Ui.Separator());

        _sidebar.AddChild(Ui.Bar("Health", p.Health, p.Health >= 50 ? UiTheme.Good : UiTheme.Bad, p.HealthLabel));
        _sidebar.AddChild(Ui.Bar("Happiness", p.Happiness, UiTheme.Info, $"{p.Happiness:0}"));
        _sidebar.AddChild(Ui.Bar("Smarts", p.Smarts, new Color("7b68c4"), $"{p.Smarts:0}"));
        _sidebar.AddChild(Ui.Bar("Looks", p.Looks, new Color("c46b98"), $"{p.Looks:0}"));
        _sidebar.AddChild(Ui.Bar("Fitness", p.Fitness, new Color("3f998b"), $"{p.Fitness:0}"));

        _sidebar.AddChild(StatRow("Money", p.Money, S.Player.Money < 0 ? UiTheme.Bad : UiTheme.Text));
        _sidebar.AddChild(StatRow("Income", p.Income, UiTheme.Text));
        _sidebar.AddChild(StatRow("Home", p.Home, UiTheme.Text));

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
        _sidebar.AddChild(Ui.Label($"Seed {S.SeedCode}  ·  started {S.World.StartYear}", 13, UiTheme.Faint));
        var bottom = Ui.HBox(8);
        var feedback = Ui.Button("Feedback  (F1)", () => _main.ShowFeedback(), 42);
        feedback.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        RegisterHint(feedback, "Write a playtest note. It is saved with a screenshot and the current situation.");
        bottom.AddChild(feedback);
        var content = Ui.Button("Settings", () => _main.ShowSettings(S), 42);
        RegisterHint(content, "Screen, text size, sound, the newspaper, and how dark themes are handled.");
        bottom.AddChild(content);
        var menu = Ui.Button("Save & exit", () => { _main.AutoSave(); _main.ShowTitle(); }, 42);
        menu.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        bottom.AddChild(menu);
        _sidebar.AddChild(bottom);
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
        var news = S.NewsThisYear();
        if (news.Count > 0)
        {
            var newsBox = Ui.VBox(6);
            newsBox.AddChild(Ui.Label("What happened", 20, UiTheme.Text));
            foreach (var line in news.OrderByDescending(l => l.Importance))
            {
                var (size, color) = line.Importance switch
                {
                    3 => (19, UiTheme.Text),
                    2 => (18, UiTheme.Text.Lerp(UiTheme.Muted, 0.35f)),
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
            _yearContent.AddChild(Ui.Label("A quiet year. Spend your time on something below, visit your family, or let the year pass.", 18, UiTheme.Muted, wrap: true));

        // Your own life
        var all = S.Actions(null);
        foreach (var (category, title, note) in new[]
        {
            ("prison", "Life inside", "You can't do much from a cell. Things outside go on without you."),
            ("life", "Your life", "School, work and money have their own tabs. People are in the People tab."),
            ("crime", "Outside the law", "Anyone can do these. Your personality decides how risky they are, and how you feel afterwards."),
        })
        {
            var actions = all.Where(a => a.Category == category).ToList();
            if (actions.Count == 0) continue;
            var box = Ui.VBox(10);
            box.AddChild(Ui.Label(title, 20, category == "crime" ? UiTheme.Bad : UiTheme.Text));
            box.AddChild(ActionButtons(actions, null));
            box.AddChild(Ui.Label(note, 15, UiTheme.Faint, wrap: true));
            _yearContent.AddChild(Ui.Card(box));
        }
    }

    // --- School & work ------------------------------------------------------------------------

    private void RefreshWork()
    {
        Ui.Clear(_workContent);
        var c = S.Career();

        var header = Ui.HBox(16);
        header.AddChild(Ui.Label("School & Work", 32, UiTheme.Accent));
        var status = Ui.Label(c.Status, 20, UiTheme.Muted);
        status.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(status);
        _workContent.AddChild(header);

        // How hard you work or study: free to change, it shows next year.
        if (c.CanChooseEffort)
        {
            var effortBox = Ui.VBox(8);
            effortBox.AddChild(Ui.Label("How hard do you push?", 19, UiTheme.Text));
            var row = Ui.HBox(8);
            var group = new ButtonGroup();
            bool working = c.JobTitle != null && c.Programme == null;
            foreach (var (value, label, hint) in new[]
                     {
                         (-1, "Take it easy", "More time for life: a little happier, but " + (working ? "your performance drops." : "your grades drop.")),
                         (0, working ? "Do your job" : "Do the work", "The usual. Nothing gained, nothing lost."),
                         (1, "Give it everything", (working ? "Performance rises, and with it the chance of promotion. " : "Grades rise. ") + "It costs happiness and health, and there is a risk of burning out."),
                     })
            {
                int v = value;
                var b = Ui.Button(label, () => { S.SetEffort(v); _main.AutoSave(); RefreshWork(); }, 44);
                b.ToggleMode = true;
                b.ButtonGroup = group;
                b.ButtonPressed = c.Effort == value;
                b.SetMeta("action", true);
                RegisterHint(b, hint);
                row.AddChild(b);
            }
            effortBox.AddChild(row);
            _workContent.AddChild(Ui.Card(effortBox));
        }

        // Education
        var edu = Ui.VBox(8);
        edu.AddChild(Ui.Label("Education", 21, UiTheme.Text));
        edu.AddChild(StatRow("Level", c.EducationLevel, UiTheme.Text));
        if (c.Programme != null)
        {
            edu.AddChild(StatRow("Studying", $"{c.Programme}  ·  {c.YearsLeft} year{(c.YearsLeft == 1 ? "" : "s")} left", UiTheme.Text));
            if (c.ProgrammeDescription != null) edu.AddChild(Ui.Label(c.ProgrammeDescription, 15, UiTheme.Muted, wrap: true));
        }
        if (c.Degrees.Count > 0) edu.AddChild(StatRow("Diplomas", string.Join(", ", c.Degrees), UiTheme.Text));
        if (c.Grades is { } grades)
        {
            bool graded = S.Player.Age(S.Year) >= c.GradesFromAge;
            edu.AddChild(Ui.Bar(graded ? "Grades" : "How school goes", grades, grades >= 60 ? UiTheme.Good : grades >= 40 ? UiTheme.Accent : UiTheme.Bad, $"{grades:0}"));
            edu.AddChild(Ui.Label(!graded
                ? $"No grades yet. They come at {c.GradesFromAge}, and how you do now is where they will start."
                : "Grades decide which programmes you can get into. Medicine needs about 85, Law 75.", 15, UiTheme.Faint, wrap: true));
            if (c.GradesNote != null) edu.AddChild(Ui.Label(c.GradesNote, 15, UiTheme.Muted, wrap: true));
        }
        if (c.PartTimeJob) edu.AddChild(Ui.Label("You have a part-time job next to your studies.", 15, UiTheme.Muted));
        _workContent.AddChild(Ui.Card(edu));

        // Work
        var work = Ui.VBox(8);
        work.AddChild(Ui.Label("Work", 21, UiTheme.Text));
        if (c.JobTitle != null)
        {
            work.AddChild(StatRow("Job", $"{c.JobTitle}  ·  {c.Field}", UiTheme.Text));
            if (c.Employer != null) work.AddChild(StatRow("Workplace", c.Employer, UiTheme.Text));
            work.AddChild(StatRow("Salary", c.Salary ?? "", UiTheme.Text));
            work.AddChild(StatRow("In the job", $"{c.YearsInJob} year{(c.YearsInJob == 1 ? "" : "s")}", UiTheme.Text));
            if (c.Performance is { } perf)
                work.AddChild(Ui.Bar("Performance", perf, perf >= 60 ? UiTheme.Good : perf >= 35 ? UiTheme.Accent : UiTheme.Bad, $"{perf:0}"));
            if (c.PerformanceNote != null) work.AddChild(Ui.Label(c.PerformanceNote, 15, UiTheme.Muted, wrap: true));
            string promo = c.PromotionChancePercent > 0 ? $"Chance of promotion this year: about {c.PromotionChancePercent}%. " : "";
            if (c.PromotionNote != null || promo != "")
                work.AddChild(Ui.Label(promo + (c.PromotionNote ?? ""), 15, UiTheme.Muted, wrap: true));

            work.AddChild(Ui.Spacer(4));
            work.AddChild(Ui.Label("Career path", 18, UiTheme.Text));
            foreach (var step in c.Ladder)
            {
                var row = Ui.HBox(12);
                var marker = Ui.Label(step.IsCurrent ? "You" : "", 15, UiTheme.Accent);
                marker.CustomMinimumSize = new Vector2(44, 0);
                row.AddChild(marker);
                var title = Ui.Label(step.Title, 17, step.IsCurrent ? UiTheme.Accent : step.Qualified ? UiTheme.Text : UiTheme.Muted);
                title.CustomMinimumSize = new Vector2(240, 0);
                row.AddChild(title);
                var salary = Ui.Label(step.Salary, 16, UiTheme.Muted);
                salary.CustomMinimumSize = new Vector2(190, 0);
                row.AddChild(salary);
                row.AddChild(Ui.Label(step.Requirement, 15, step.Qualified ? UiTheme.Faint : UiTheme.Bad, wrap: true));
                work.AddChild(row);
            }
        }
        else
        {
            work.AddChild(Ui.Label(c.Status == "Looking for work"
                ? "You don't have a job. Look for one below. Offers depend on your education and grades."
                : "No job right now.", 16, UiTheme.Muted, wrap: true));
        }
        _workContent.AddChild(Ui.Card(work));

        if (c.CriminalRecord.Count > 0)
        {
            var rec = Ui.VBox(6);
            rec.AddChild(Ui.Label("Criminal record", 21, UiTheme.Bad));
            foreach (var line in c.CriminalRecord) rec.AddChild(Ui.Label(line, 16, UiTheme.Muted, wrap: true));
            rec.AddChild(Ui.Label("Employers check. A record makes job offers rarer.", 15, UiTheme.Faint));
            _workContent.AddChild(Ui.Card(rec));
        }

        var actions = S.Actions(null).Where(a => a.Category == "career").ToList();
        if (actions.Count > 0)
        {
            var box = Ui.VBox(10);
            box.AddChild(Ui.Label("What do you want to do?", 19, UiTheme.Text));
            box.AddChild(ActionButtons(actions, null));
            _workContent.AddChild(Ui.Card(box));
        }
    }

    // --- Money --------------------------------------------------------------------------------

    private void RefreshMoney()
    {
        Ui.Clear(_moneyContent);
        var m = S.Money();

        _moneyContent.AddChild(Ui.Label("Money", 32, UiTheme.Accent));

        var summary = Ui.VBox(8);
        summary.AddChild(StatRow(m.InDebt ? "Debt" : "In the bank", m.Money, m.InDebt ? UiTheme.Bad : UiTheme.Text));
        foreach (var (label, amount) in m.Assets)
            summary.AddChild(StatRow(label, amount, amount.StartsWith("-") ? UiTheme.Bad : UiTheme.Text));
        summary.AddChild(StatRow("Net worth", m.NetWorth, UiTheme.Accent));
        summary.AddChild(StatRow("Income", m.YearlyIncome, UiTheme.Text));
        if (m.Home != null) summary.AddChild(Ui.Label(m.Home, 16, UiTheme.Muted, wrap: true));
        summary.AddChild(Ui.Label(m.MarketNote, 15, UiTheme.Muted, wrap: true));
        summary.AddChild(Ui.Label(
            $"How it works: {m.TaxPercent}% of your income goes to tax. Living costs and the home are paid first. Of what is left, you save about " +
            $"{m.SaveRatePercent}%, depending on your personality. Money in the bank roughly keeps its value. Funds and shares " +
            $"grow more over time but can crash. If your income does not cover the basics, savings pay first, then welfare pays {m.WelfareShare} of the gap " +
            "and the rest becomes debt.", 15, UiTheme.Faint, wrap: true));
        _moneyContent.AddChild(Ui.Card(summary));

        _moneyContent.AddChild(InvestmentsCard(m));
        _moneyContent.AddChild(HomeCard(m));
        if (S.Businesses() is { Count: > 0 } businesses) _moneyContent.AddChild(BusinessCard(businesses));
        if (S.Rentals().Count > 0 || S.RentalOptions().Count > 0) _moneyContent.AddChild(RentalCard(S.Rentals()));
        _heirloomCard = S.Heirlooms() is { Count: > 0 } heirlooms ? HeirloomsCard(heirlooms) : null;
        if (_heirloomCard != null) _moneyContent.AddChild(_heirloomCard);

        _moneyContent.AddChild(LedgerCard($"This year ({m.Year})", m.ThisYear, m.ThisYearTotal));
        if (m.LastYear.Count > 0) _moneyContent.AddChild(LedgerCard($"Last year ({m.Year - 1})", m.LastYear, m.LastYearTotal));

        var actions = S.Actions(null).Where(a => a.Category == "money").ToList();
        if (actions.Count > 0)
        {
            var box = Ui.VBox(10);
            box.AddChild(Ui.Label("Also", 19, UiTheme.Text));
            box.AddChild(ActionButtons(actions, null));
            _moneyContent.AddChild(Ui.Card(box));
        }
    }

    // --- Investments --------------------------------------------------------------------------

    private static string Pct(int v) => (v > 0 ? "+" : "") + v + " %";
    private static Color PctColor(int v) => v > 0 ? UiTheme.Good : v < 0 ? UiTheme.Bad : UiTheme.Muted;

    private static Label Cell(string text, float width, Color color, int size = 16)
    {
        var l = Ui.Label(text, size, color);
        l.CustomMinimumSize = new Vector2(width, 0);
        l.HorizontalAlignment = HorizontalAlignment.Right;
        return l;
    }

    private Control InvestmentsCard(MoneyView m)
    {
        var box = Ui.VBox(8);
        var head = Ui.HBox(12);
        var title = Ui.Label("Your investments", 20, UiTheme.Text);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        head.AddChild(Ui.Label(m.InvestedTotal, 19, UiTheme.Accent));
        box.AddChild(head);

        if (m.Holdings.Count == 0)
            box.AddChild(Ui.Label("Nothing invested yet. Funds spread the risk over many companies; a single company can double, or go bankrupt.", 15, UiTheme.Muted, wrap: true));
        else
        {
            var columns = Ui.HBox(10);
            var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            columns.AddChild(spacer);
            foreach (var (text, width) in new[] { ("Paid in", 120f), ("Worth now", 130f), ("Change", 90f), ("Last year", 90f), ("", 90f) })
                columns.AddChild(Cell(text, width, UiTheme.Faint, 14));
            box.AddChild(columns);
            foreach (var h in m.Holdings)
            {
                var row = Ui.HBox(10);
                var name = Ui.VBox(0);
                name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                name.AddChild(Ui.Label(h.Name, 17, UiTheme.Text));
                name.AddChild(Ui.Label($"{(h.Kind == "fund" ? "Fund" : "Shares")}  ·  {h.Risk} risk  ·  since {h.SinceYear}" +
                                       (h.Dividend != null ? $"  ·  dividend {h.Dividend}" : ""), 13, UiTheme.Muted));
                row.AddChild(name);
                row.AddChild(Cell(h.Invested, 120, UiTheme.Muted));
                row.AddChild(Cell(h.Value, 130, UiTheme.Text, 17));
                row.AddChild(Cell(Pct(h.ChangePercent), 90, PctColor(h.ChangePercent)));
                row.AddChild(Cell(Pct(h.LastYearPercent), 90, PctColor(h.LastYearPercent)));
                var id = h.AssetId;
                string holdingName = h.Name;
                var sell = Ui.Button("Sell", () => ShowSellDialog(id, holdingName), 40);
                sell.CustomMinimumSize = new Vector2(90, 40);
                sell.SetMeta("action", true);
                RegisterHint(sell, $"Sell some or all of your {h.Name}.");
                row.AddChild(sell);
                box.AddChild(row);
            }
        }
        var invest = Ui.Button("Invest…", ShowInvestDialog, 46);
        invest.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        invest.Disabled = m.CannotInvest != null;
        invest.SetMeta("action", true);
        RegisterHint(invest, m.CannotInvest ?? "Choose a fund or a company, and how much of your savings to put in.");
        box.AddChild(invest);
        return Ui.Card(box);
    }

    private void ShowInvestDialog()
    {
        var options = S.InvestmentOptions();
        var box = Ui.VBox(12);
        box.CustomMinimumSize = new Vector2(860, 0);
        box.AddChild(Ui.Label("Invest", 28, UiTheme.Accent));
        box.AddChild(Ui.Label($"You have {EconomySystemFormat(S.Savings)} in the bank.", 17, UiTheme.Muted));

        var list = Ui.VBox(6);
        var group = new ButtonGroup();
        var description = Ui.Label("", 15, UiTheme.Muted, wrap: true);
        description.CustomMinimumSize = new Vector2(0, 44);
        Button? first = null;
        foreach (var a in options)
        {
            string stats = $"Last year {(a.LastYearPercent is { } ly ? Pct(ly) : "new")}  ·  five years {(a.FiveYearPercent is { } fy ? Pct(fy) : "too new to say")}" +
                           (a.DividendPercent > 0 ? $"  ·  dividend about {a.DividendPercent} %" : "");
            var b = new Button
            {
                Text = $"{a.Name}   ({(a.Kind == "fund" ? "fund" : "shares")}, {a.Risk.ToLowerInvariant()} risk)\n{stats}",
                ToggleMode = true, ButtonGroup = group, Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, 58), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            b.SetMeta("asset", a.Id);
            string text = a.Description;
            b.FocusEntered += () => description.Text = text;
            b.MouseEntered += () => description.Text = text;
            b.Toggled += on => { if (on) description.Text = text; };
            list.AddChild(b);
            first ??= b;
        }
        if (first != null) first.ButtonPressed = true;
        var scroll = Ui.Scroll(list);
        scroll.CustomMinimumSize = new Vector2(0, Mathf.Min(420, options.Count * 64 + 8));
        box.AddChild(scroll);
        box.AddChild(description);

        double max = Math.Max(1, Math.Floor(S.Savings));
        var amountLabel = Ui.Label("", 18, UiTheme.Text);
        var slider = new HSlider { MinValue = 0, MaxValue = max, Step = Math.Max(1, Math.Round(max / 200)), Value = Math.Round(max * 0.25),
            CustomMinimumSize = new Vector2(0, 36), SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.All };
        void ShowAmount() => amountLabel.Text = $"Amount: {EconomySystemFormat(slider.Value)}";
        slider.ValueChanged += _ => ShowAmount();
        ShowAmount();
        var amountRow = Ui.HBox(10);
        amountLabel.CustomMinimumSize = new Vector2(240, 0);
        amountRow.AddChild(amountLabel);
        amountRow.AddChild(slider);
        box.AddChild(amountRow);
        var quick = Ui.HBox(8);
        foreach (var (label, share) in new[] { ("10 %", 0.1), ("25 %", 0.25), ("50 %", 0.5), ("All", 1.0) })
        {
            double s = share;
            quick.AddChild(Ui.Button(label, () => slider.Value = Math.Floor(max * s), 40));
        }
        box.AddChild(quick);

        System.Action close = () => { };
        var buttons = Ui.HBox(10);
        buttons.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        buttons.AddChild(Ui.Button("Cancel", () => close(), 46));
        var confirm = Ui.Button("Invest", () =>
        {
            if (group.GetPressedButton() is not { } chosen || slider.Value < 1) return;
            string result = S.BuyInvestment(chosen.GetMeta("asset").AsString(), slider.Value);
            close();
            _main.AutoSave();
            RefreshAll();
            _main.ShowMessage("Invested", result);
        }, 46);
        UiTheme.MakePrimary(confirm);
        confirm.CustomMinimumSize = new Vector2(160, 46);
        buttons.AddChild(confirm);
        box.AddChild(buttons);
        close = _main.ShowDialog(box, first ?? (Control)confirm);
    }

    private void ShowSellDialog(string assetId, string name)
    {
        var box = Ui.VBox(14);
        box.CustomMinimumSize = new Vector2(520, 0);
        box.AddChild(Ui.Label($"Sell {name}", 26, UiTheme.Accent));
        box.AddChild(Ui.Label("The money goes into your bank account.", 17, UiTheme.Muted, wrap: true));
        System.Action close = () => { };
        void Sell(double share)
        {
            string result = S.SellInvestment(assetId, share);
            close();
            _main.AutoSave();
            RefreshAll();
            _main.ShowMessage("Sold", result);
        }
        var buttons = Ui.HBox(10);
        var half = Ui.Button("Sell half", () => Sell(0.5), 46);
        buttons.AddChild(half);
        buttons.AddChild(Ui.Button("Sell all", () => Sell(1), 46));
        buttons.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        buttons.AddChild(Ui.Button("Cancel", () => close(), 46));
        box.AddChild(buttons);
        close = _main.ShowDialog(box, half);
    }

    private string EconomySystemFormat(double nominal) => OneMoreYear.Simulation.Systems.EconomySystem.Format(S.Ctx, nominal);

    // --- Home ---------------------------------------------------------------------------------

    /// <summary>Screenshot tour: the bottom of the money tab.</summary>
    private Control? _heirloomCard;

    /// <summary>The player's own business: its worth, last year, and a way to sell.</summary>
    private Control BusinessCard(IReadOnlyList<BusinessView> businesses)
    {
        var box = Ui.VBox(10);
        box.AddChild(Ui.Label(businesses.Count == 1 ? "Your business" : "Your businesses", 20, UiTheme.Text));
        foreach (var b in businesses)
        {
            var item = Ui.VBox(3);
            item.AddChild(UiTheme.HandLabel(b.Name, 26, UiTheme.Accent));
            item.AddChild(Ui.Label($"A {b.Kind}, since {b.Since}" + (b.Owners > 1 ? $", run by {b.Owners} of the family so far" : ""), 15, UiTheme.Muted, wrap: true));
            item.AddChild(Ui.Label($"Worth about {b.Value}. Last year: {(b.LastYearGood ? "a profit of " : "a loss, ")}{b.LastProfit.TrimStart('-')}.", 15,
                b.LastYearGood ? UiTheme.Text : UiTheme.Bad, wrap: true));
            int id = b.Id;
            var sell = Ui.Button($"Sell {b.Name}", () => _main.ShowMessage(b.Name, S.SellBusiness(id), RefreshAll), 42);
            sell.SetMeta("action", true);
            sell.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            RegisterHint(sell, "Money now, and the end of a family business.");
            item.AddChild(sell);
            box.AddChild(item);
        }
        return Ui.Card(box);
    }

    /// <summary>Homes the player lets out: worth, loan, last year's rent, and a way to buy or sell.</summary>
    private Control RentalCard(IReadOnlyList<RentalView> rentals)
    {
        var box = Ui.VBox(10);
        box.AddChild(Ui.Label("Homes to let", 20, UiTheme.Text));
        if (rentals.Count == 0)
            box.AddChild(Ui.Label("Buy a flat or a house and let it out: rent every year, a value that follows the housing market, and something to leave the family. Tenants come with it.",
                15, UiTheme.Muted, wrap: true));
        foreach (var r in rentals)
        {
            var item = Ui.VBox(3);
            item.AddChild(Ui.Label(r.Name, 18, UiTheme.Accent, wrap: true));
            item.AddChild(Ui.Label($"In the family since {r.Since}" + (r.Owners > 1 ? $", owned by {r.Owners} of the family so far" : "") + $". Worth about {r.Value}" +
                (r.Loan != null ? $", with {r.Loan} left on the loan." : ", and paid off."), 15, UiTheme.Muted, wrap: true));
            if (r.HasYear)
                item.AddChild(Ui.Label(r.LastYearGood ? $"Last year it brought in {r.LastNet} after costs and interest." : $"Last year it cost you {r.LastNet} more than it brought in.",
                    15, r.LastYearGood ? UiTheme.Text : UiTheme.Bad, wrap: true));
            int id = r.Id;
            string name = r.Name;
            var sell = Ui.Button("Sell it", () => _main.ShowConfirm($"Sell {name.ToLowerInvariant()}?", $"It is worth about {r.Value}. The loan is paid off from the price.", () =>
            {
                string result = S.SellRental(id);
                _main.AutoSave();
                RefreshAll();
                _main.ShowMessage("Sold", result);
            }), 42);
            sell.SetMeta("action", true);
            sell.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            item.AddChild(sell);
            box.AddChild(item);
        }
        if (S.RentalOptions().Count > 0)
        {
            var buy = Ui.Button("Buy a home to let…", ShowRentalDialog, 46);
            buy.SetMeta("action", true);
            buy.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            RegisterHint(buy, "See what a home to let costs in your city.");
            box.AddChild(buy);
        }
        return Ui.Card(box);
    }

    /// <summary>A life in photographs: a grid of portraits at each age, with a handwritten caption.</summary>
    public void ShowAlbum(int personId, string name) => AlbumDialog.Show(_main, S, personId, name);

    private void ShowHomeProjectsDialog()
    {
        var box = Ui.VBox(10);
        box.CustomMinimumSize = new Vector2(860, 0);
        box.AddChild(Ui.Label("Do up your home", 28, UiTheme.Accent));
        box.AddChild(Ui.Label("Part of what you spend comes back in the home's value, and a home that has been looked after makes everyone in it a little happier, every year. Kitchens and bathrooms wear out in time.",
            15, UiTheme.Muted, wrap: true));
        System.Action close = () => { };
        Control? first = null;
        var list = Ui.VBox(10);
        foreach (var h in S.HomeProjects())
        {
            var row = Ui.HBox(12);
            var col = Ui.VBox(2);
            col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            col.AddChild(Ui.Label(h.Name, 18, h.DoneYear != null ? UiTheme.Muted : UiTheme.Text));
            col.AddChild(Ui.Label(h.DoneYear is { } year ? $"Done in {year}." : h.Text, 14, UiTheme.Faint, wrap: true));
            row.AddChild(col);
            string id = h.Id, name = h.Name;
            var b = Ui.Button(h.DoneYear != null ? "Done" : h.Cost, () =>
            {
                string result = S.DoHomeProject(id);
                close();
                _main.AutoSave();
                RefreshAll();
                _main.ShowMessage(name, result);
            }, 42);
            b.CustomMinimumSize = new Vector2(170, 42);
            b.Disabled = h.DoneYear != null || !h.CanAfford;
            RegisterHint(b, h.DoneYear != null ? "Already done." : h.CanAfford ? $"Costs about {h.Cost}." : $"You need about {h.Cost}.");
            row.AddChild(b);
            list.AddChild(row);
            first ??= b.Disabled ? null : b;
        }
        var scroll = Ui.Scroll(list);
        scroll.CustomMinimumSize = new Vector2(0, Mathf.Min(520, GetViewportRect().Size.Y - 260));
        box.AddChild(scroll);
        var cancel = Ui.Button("Not now", () => close(), 46);
        cancel.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        box.AddChild(cancel);
        close = _main.ShowDialog(box, first ?? cancel);
    }

    private void ShowRentalDialog()
    {
        var box = Ui.VBox(10);
        box.CustomMinimumSize = new Vector2(760, 0);
        box.AddChild(Ui.Label("Buy a home to let", 28, UiTheme.Accent));
        box.AddChild(Ui.Label("Prices are for your city. The bank lends the rest, but wants a quarter of the price from you, and there are fees. " +
                              "The rent pays the loan's interest and the running costs, most years. Some years a tenant does not pay, or a pipe bursts.",
            15, UiTheme.Muted, wrap: true));
        System.Action close = () => { };
        Control? first = null;
        foreach (var o in S.RentalOptions())
        {
            string typeId = o.TypeId, typeName = o.Name.ToLowerInvariant();
            var b = Ui.Button($"{o.Name}  ·  {o.Price}  ·  {o.CashNeeded} from you", () => _main.ShowConfirm($"Buy {typeName} to let?",
                $"It costs about {o.Price}. You pay {o.CashNeeded} now; the bank lends the rest.", () =>
                {
                    string result = S.BuyRental(typeId);
                    close();
                    _main.AutoSave();
                    RefreshAll();
                    _main.ShowMessage("A home to let", result);
                }), 44);
            b.Disabled = !o.CanAfford;
            RegisterHint(b, o.CanAfford ? $"You pay {o.CashNeeded} now." : $"You need {o.CashNeeded} in the bank.");
            box.AddChild(b);
            first ??= b.Disabled ? null : b;
        }
        var cancel = Ui.Button("Not now", () => close(), 46);
        cancel.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        box.AddChild(cancel);
        close = _main.ShowDialog(box, first ?? cancel);
    }

    public void ScrollMoneyToEnd()
    {
        if (_moneyContent.GetParent()?.GetParent() is ScrollContainer scroll && _heirloomCard != null) scroll.EnsureControlVisible(_heirloomCard);
    }

    /// <summary>The family's things the player keeps, with their story; selling takes them out of the family.</summary>
    private Control HeirloomsCard(IReadOnlyList<HeirloomView> heirlooms)
    {
        var box = Ui.VBox(10);
        box.AddChild(Ui.Label("Things that stay in the family", 20, UiTheme.Text));
        foreach (var h in heirlooms)
        {
            var item = Ui.VBox(3);
            item.AddChild(UiTheme.HandLabel(h.Name, 24, UiTheme.Accent));
            item.AddChild(Ui.Label(h.Text, 15, UiTheme.Muted, wrap: true));
            item.AddChild(Ui.Label(h.History, 14, UiTheme.Faint, wrap: true));
            item.AddChild(Ui.Label(h.PromisedTo != null ? $"Promised to {h.PromisedTo}. A dealer would pay about {h.Value}." : $"A dealer would pay about {h.Value}.", 14, UiTheme.Faint, wrap: true));
            int id = h.Id;
            var sell = Ui.Button($"Sell {h.Name}", () => _main.ShowMessage(h.Name, S.SellHeirloom(id) + " It is no longer in the family.", RefreshAll), 42);
            sell.SetMeta("action", true);
            sell.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            RegisterHint(sell, "Money now. The story leaves the family with it.");
            item.AddChild(sell);
            box.AddChild(item);
        }
        return Ui.Card(box);
    }

    private Control HomeCard(MoneyView m)
    {
        var box = Ui.VBox(8);
        box.AddChild(Ui.Label("Your home", 20, UiTheme.Text));
        box.AddChild(Ui.Label(m.HomeDescription + ".", 17, UiTheme.Text, wrap: true));
        if (m.HousingPerMonth != null) box.AddChild(Ui.Label($"It costs about {m.HousingPerMonth} a month to live there.", 15, UiTheme.Muted, wrap: true));
        if (m.Home != null) box.AddChild(Ui.Label(m.Home, 15, UiTheme.Muted, wrap: true));
        if (m.Cottage != null) box.AddChild(Ui.Label($"Your summer cottage is worth about {m.Cottage}.", 15, UiTheme.Muted, wrap: true));

        var buttons = new HFlowContainer();
        buttons.AddThemeConstantOverride("h_separation", 8);
        buttons.AddThemeConstantOverride("v_separation", 8);
        var find = Ui.Button("Find a new home…", ShowHomeDialog, 46);
        find.Disabled = !m.CanMove;
        find.SetMeta("action", true);
        RegisterHint(find, m.CanMove ? "See what homes in your city cost to rent or buy." : "You cannot move right now.");
        buttons.AddChild(find);
        if (S.HomeProjects().Count > 0)
        {
            var improve = Ui.Button("Do up your home…", ShowHomeProjectsDialog, 46);
            improve.SetMeta("action", true);
            RegisterHint(improve, "A new kitchen, a garden, new windows. It costs money, adds to the home's value, and makes it nicer to live in.");
            buttons.AddChild(improve);
        }
        if (m.HasMortgage)
        {
            var repay = Ui.Button("Pay extra on the loan", ShowRepayDialog, 46);
            repay.SetMeta("action", true);
            RegisterHint(repay, "A smaller loan means less interest every year.");
            buttons.AddChild(repay);
        }
        if (m.Cottage != null)
        {
            var sell = Ui.Button("Sell the summer cottage", () => _main.ShowConfirm("Sell the cottage?", $"It is worth about {m.Cottage}.", () =>
            {
                string result = S.SellCottage();
                _main.AutoSave();
                RefreshAll();
                _main.ShowMessage("Sold", result);
            }), 46);
            sell.SetMeta("action", true);
            buttons.AddChild(sell);
        }
        box.AddChild(buttons);
        return Ui.Card(box);
    }

    private void ShowHomeDialog()
    {
        var options = S.HomeOptions();
        var box = Ui.VBox(10);
        box.CustomMinimumSize = new Vector2(900, 0);
        box.AddChild(Ui.Label("Find a new home", 28, UiTheme.Accent));
        box.AddChild(Ui.Label("Prices are for your city. Buying needs a down payment and an income the bank will lend on; if you own a home now, it is sold first.",
            15, UiTheme.Muted, wrap: true));
        System.Action close = () => { };
        Control? firstButton = null;
        var list = Ui.VBox(10);
        foreach (var o in options)
        {
            var row = Ui.VBox(4);
            var top = Ui.HBox(10);
            var name = Ui.Label(o.Name + (o.Current ? "  (where you live now)" : ""), 18, o.Current ? UiTheme.Accent : UiTheme.Text);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            top.AddChild(name);
            top.AddChild(Ui.Label(o.Sleeps == 1 ? "for one" : $"fits {o.Sleeps}", 14, UiTheme.Muted));
            row.AddChild(top);
            var buttons = Ui.HBox(8);
            string typeId = o.TypeId, typeName = o.Name.ToLowerInvariant();
            if (o.RentPerMonth != null)
            {
                var rent = Ui.Button($"Rent  ·  {o.RentPerMonth} a month", () => _main.ShowConfirm($"Rent {typeName}?",
                    $"The rent is about {o.RentPerMonth} a month.", () => ChooseHome(typeId, false, close)), 42);
                rent.Disabled = o.Current;
                buttons.AddChild(rent);
                firstButton ??= rent.Disabled ? null : rent;
            }
            if (o.Price != null)
            {
                var buy = Ui.Button($"Buy  ·  {o.Price}  (about {o.OwnPerMonth} a month)", () => _main.ShowConfirm($"Buy {typeName}?",
                    $"It costs about {o.Price}. With the loan and running costs, about {o.OwnPerMonth} a month.", () => ChooseHome(typeId, true, close)), 42);
                buy.Disabled = !o.CanBuy;
                RegisterHint(buy, o.CannotBuyReason ?? $"Costs about {o.Price}.");
                buttons.AddChild(buy);
                firstButton ??= buy.Disabled ? null : buy;
            }
            row.AddChild(buttons);
            if (!o.CanBuy && o.CannotBuyReason != null && o.Price != null) row.AddChild(Ui.Label(o.CannotBuyReason, 13, UiTheme.Faint, wrap: true));
            list.AddChild(row);
        }
        var scroll = Ui.Scroll(list);
        scroll.CustomMinimumSize = new Vector2(0, Mathf.Min(520, GetViewportRect().Size.Y - 260));
        box.AddChild(scroll);
        var cancel = Ui.Button("Stay where you are", () => close(), 46);
        cancel.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        box.AddChild(cancel);
        close = _main.ShowDialog(box, firstButton ?? cancel);
    }

    private void ChooseHome(string typeId, bool buy, System.Action closeDialog)
    {
        string result = S.ChooseHome(typeId, buy);
        closeDialog();
        _main.AutoSave();
        RefreshAll();
        _main.ShowMessage(buy ? "A new home" : "Moving", result);
    }

    private void ShowRepayDialog()
    {
        var box = Ui.VBox(14);
        box.CustomMinimumSize = new Vector2(520, 0);
        box.AddChild(Ui.Label("Pay extra on the loan", 26, UiTheme.Accent));
        box.AddChild(Ui.Label("Money from your savings goes straight to the bank.", 17, UiTheme.Muted, wrap: true));
        System.Action close = () => { };
        void Repay(double share)
        {
            string result = S.RepayMortgage(share);
            close();
            _main.AutoSave();
            RefreshAll();
            _main.ShowMessage("The loan", result);
        }
        var buttons = Ui.HBox(10);
        var quarter = Ui.Button("A quarter of your savings", () => Repay(0.25), 46);
        buttons.AddChild(quarter);
        buttons.AddChild(Ui.Button("As much as you can", () => Repay(1), 46));
        buttons.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        buttons.AddChild(Ui.Button("Cancel", () => close(), 46));
        box.AddChild(buttons);
        close = _main.ShowDialog(box, quarter);
    }

    private static Control LedgerCard(string title, IReadOnlyList<LedgerView> lines, string total)
    {
        var box = Ui.VBox(6);
        box.AddChild(Ui.Label(title, 20, UiTheme.Text));
        if (lines.Count == 0) box.AddChild(Ui.Label("Nothing yet.", 16, UiTheme.Muted));
        foreach (var line in lines)
        {
            var row = Ui.HBox(12);
            var label = Ui.Label(line.Label, 17, UiTheme.Text, wrap: true);
            row.AddChild(label);
            var amount = Ui.Label(line.Amount, 17, line.Raw >= 0 ? UiTheme.Good : UiTheme.Bad);
            amount.HorizontalAlignment = HorizontalAlignment.Right;
            amount.CustomMinimumSize = new Vector2(160, 0);
            row.AddChild(amount);
            box.AddChild(row);
        }
        box.AddChild(Ui.Separator());
        var totalRow = Ui.HBox(12);
        totalRow.AddChild(Ui.Label("Change in your money", 17, UiTheme.Text, wrap: true));
        var t = Ui.Label(total, 18, total.StartsWith("+") ? UiTheme.Good : UiTheme.Bad);
        t.HorizontalAlignment = HorizontalAlignment.Right;
        t.CustomMinimumSize = new Vector2(160, 0);
        totalRow.AddChild(t);
        box.AddChild(totalRow);
        return Ui.Card(box);
    }

    private Control BuildEventCard(EventView ev)
    {
        var box = Ui.VBox(10);
        box.AddChild(Ui.Label(ev.Title, 24, UiTheme.Accent));
        if (ev.TargetId is { } tid)
        {
            var t = S.Describe(tid);
            var who = Ui.HBox(10);
            who.AddChild(Portrait.Create(S.Portrait(t.Id), false, 36));
            var whoLabel = Ui.Label($"{t.Name}  ·  {t.RoleLabel}, {t.Age}", 16, UiTheme.Muted);
            whoLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            who.AddChild(whoLabel);
            box.AddChild(who);
        }
        box.AddChild(Ui.Label(ev.Text, 19, UiTheme.Text, wrap: true));

        // What your personality lets you notice – written in the margin, in the trait's colour.
        foreach (var insight in ev.Insights ?? System.Array.Empty<InsightView>())
        {
            var row = Ui.HBox(10);
            var tag = Ui.Label(insight.Label.ToUpperInvariant(), 13, Ui.ToneColor(insight.Tone));
            tag.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            tag.CustomMinimumSize = new Vector2(110, 0);
            row.AddChild(tag);
            row.AddChild(UiTheme.HandLabel(insight.Text, 21, Ui.ToneColor(insight.Tone).Darkened(0.2f), wrap: true));
            box.AddChild(row);
        }

        if (!ev.Resolved)
        {
            foreach (var c in ev.Choices)
            {
                string text = (c.Tag != null ? $"[{c.Tag}]  " : "") + c.Text + (c.ChancePercent is { } pc ? $"     {pc}% chance" : "");
                var uid = ev.Uid;
                var index = c.Index;
                var b = Ui.Button(text, () => OnChoose(uid, index), 52);
                b.Alignment = HorizontalAlignment.Left;
                b.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                b.Disabled = !c.Available;
                b.SetMeta("choice", true);
                if (c.Tag != null) b.AddThemeColorOverride("font_color", UiTheme.AccentDark);
                string? hint = c.Factors == null ? c.Hint : (c.Hint == null ? c.Factors : $"{c.Hint}   ({c.Factors})");
                RegisterHint(b, c.Available ? hint : c.Hint ?? "Not possible right now.");
                box.AddChild(b);
                // The chance is explained right under the choice, not only in the hint.
                if (c.Factors != null && c.Available) box.AddChild(Ui.Label("      " + c.Factors, 13, UiTheme.Muted));
                // And why a choice is out of reach, so nobody has to guess.
                if (!c.Available && c.Hint != null) box.AddChild(Ui.Label("      " + c.Hint, 13, UiTheme.Faint, wrap: true));
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
            // Never leave the player stuck on a stale screen: show what is waiting.
            RefreshAll();
            if (S.HasUnresolvedEvents)
            {
                _tabs.CurrentTab = TabYear;
                FocusDefault();
            }
            return;
        }
        var report = S.AdvanceYear();
        Sound.Play("year");
        _main.AutoSave();
        if (S.NeedsSuccession) { _main.ShowSuccession(); return; }
        // The whole screen is rebuilt so the look follows the new year, then the paper arrives.
        _main.ShowGame();
        // A new decade opens with a chapter page, then the paper.
        if (S.IsNewDecade) _main.ShowChapter(S.Chapter(), () => _main.ShowNewspaper(report));
        else _main.ShowNewspaper(report);
    }

    public void FocusAfterNewspaper()
    {
        _tabs.CurrentTab = TabYear;
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
            if (a.Locked != null) hint = a.Locked;
            else if (!a.Enabled) hint = S.World.ActionPoints <= 0 ? "You have no time left this year." : "Already done this year.";
            RegisterHint(b, hint);
            flow.AddChild(b);
        }
        return flow;
    }

    private void OnAction(string actionId, int? targetId)
    {
        var title = S.Actions(targetId).FirstOrDefault(a => a.Id == actionId)?.Title ?? "";
        bool wishKept = S.Wish() is { Kept: true };
        var result = S.PerformAction(actionId, targetId);
        if (!wishKept && S.Wish() is { Kept: true }) result += "\n\nIt was what you wished for this year.";
        _main.AutoSave();
        _main.ShowMessage(title, result, () =>
        {
            if (S.NeedsSuccession) { _main.ShowSuccession(); return; }
            // Some actions lead to a decision (job offers, university applications).
            if (S.HasUnresolvedEvents) _tabs.CurrentTab = TabYear;
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
        row.AddChild(Portrait.Create(S.Portrait(p.Id), p.Id == S.Player.Id, 48));
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
            _hint.Text = $"{p.Name}: press to see details and things you can do together.";
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
        header.AddChild(Portrait.Create(S.Portrait(p.Id), isPlayer, 96));
        var col = Ui.VBox(3);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        col.AddChild(Ui.Label(p.Name, 28, UiTheme.Text, wrap: true));
        col.AddChild(Ui.Label(p.Alive ? $"{p.RoleLabel}  ·  Age {p.Age}" : $"{p.RoleLabel}  ·  {p.BirthYear}–{p.DeathYear}", 18, UiTheme.Accent));
        col.AddChild(Ui.Label(p.Occupation, 16, UiTheme.Muted, wrap: true));
        if (p.Fame != null) col.AddChild(Ui.Label(p.Fame, 15, UiTheme.Accent));
        if (!string.IsNullOrEmpty(p.Partner)) col.AddChild(Ui.Label(p.Partner, 16, UiTheme.Muted, wrap: true));
        if (p.Dream != null) col.AddChild(UiTheme.HandLabel(p.Dream, 21, UiTheme.Accent, wrap: true));
        col.AddChild(Ui.Label(p.AppearanceText, 15, UiTheme.Faint, wrap: true));
        if (p.Condition != null) col.AddChild(Ui.Label(p.Condition, 16, UiTheme.Bad));
        header.AddChild(col);
        _personDetail.AddChild(header);

        // Close family as buttons: jump straight to a partner, parent, sibling or child.
        if (p.Links.Count > 0)
        {
            var links = new HFlowContainer();
            links.AddThemeConstantOverride("h_separation", 6);
            links.AddThemeConstantOverride("v_separation", 6);
            foreach (var link in p.Links)
            {
                int linkId = link.Id;
                var b = Ui.Button($"{link.Relation}: {link.Name}{(link.Alive ? "" : " †")}", () => SelectPerson(linkId), 38);
                b.AddThemeFontSizeOverride("font_size", 15);
                if (!link.Alive) b.AddThemeColorOverride("font_color", UiTheme.Muted);
                RegisterHint(b, $"Show {link.Name}.");
                links.AddChild(b);
            }
            _personDetail.AddChild(links);
        }

        // Personality – descriptions are always visible, not hidden in tooltips.
        var traitBox = Ui.VBox(4);
        foreach (var (name, description, tone) in p.Traits)
        {
            var row = Ui.HBox(10);
            row.AddChild(Ui.Chip(name, Ui.ToneColor(tone)));
            var d = Ui.Label(description, 15, UiTheme.Muted, wrap: true);
            d.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(d);
            traitBox.AddChild(row);
        }
        if (p.Traits.Count > 0)
            traitBox.AddChild(UiTheme.HandLabel("green: a good side   ·   red: a dark side   ·   blue: neither, it depends", 17, UiTheme.Faint));
        _personDetail.AddChild(traitBox);

        if (!isPlayer && p.TowardsPlayer is { } r)
        {
            var relBox = Ui.VBox(6);
            relBox.AddChild(Ui.Label($"How {p.FirstName} feels about you:  {r.OpinionLabel}", 19, Ui.OpinionColor(r.Opinion)));
            relBox.AddChild(Ui.Bar("Closeness", r.Closeness, UiTheme.Good));
            relBox.AddChild(Ui.Bar("Trust", r.Trust, UiTheme.Info));
            relBox.AddChild(Ui.Bar("Respect", r.Respect, UiTheme.Accent));
            if (r.Attraction > 1) relBox.AddChild(Ui.Bar("Attraction", r.Attraction, new Color("c46b98")));
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
            var memHead = Ui.HBox(10);
            var memTitle = Ui.Label(isPlayer ? "Your memories" : $"{p.FirstName} remembers", 19, UiTheme.Text);
            memTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            memHead.AddChild(memTitle);
            string albumName = p.FirstName;
            var album = Ui.Button("Photo album…", () => ShowAlbum(id, albumName), 38);
            album.SetMeta("action", true);
            RegisterHint(album, "A life in photographs.");
            memHead.AddChild(album);
            memBox.AddChild(memHead);
            foreach (var m in p.Memories)
            {
                var color = m.Impact < -3 ? UiTheme.Bad : m.Impact > 3 ? UiTheme.Good : UiTheme.Muted;
                memBox.AddChild(UiTheme.HandLabel($"{m.Year}   {m.Text}", 23, color, wrap: true));
            }
            _personDetail.AddChild(Ui.Card(memBox));
        }

        // Your own child at home: how you raise them shapes the adult they become.
        if (S.Upbringing().FirstOrDefault(u => u.ChildId == id) is { } raising)
        {
            var box = Ui.VBox(8);
            box.AddChild(Ui.Label($"How you raise {raising.Name}", 19, UiTheme.Text));
            var row = new HFlowContainer();
            row.AddThemeConstantOverride("h_separation", 8);
            foreach (var style in OneMoreYear.Simulation.Systems.UpbringingSystem.Styles)
            {
                string s = style;
                var b = Ui.Button(char.ToUpperInvariant(s[0]) + s[1..], () => { S.SetUpbringing(id, s); RefreshDetail(); }, 42);
                if (raising.Style == s) UiTheme.MakePrimary(b);
                b.SetMeta("action", true);
                RegisterHint(b, OneMoreYear.Simulation.Systems.UpbringingSystem.Describe(s));
                row.AddChild(b);
            }
            box.AddChild(row);
            box.AddChild(Ui.Label(OneMoreYear.Simulation.Systems.UpbringingSystem.Describe(raising.Style)
                + (raising.Chosen ? "" : " (as you are by nature; choose to change it)"), 15, UiTheme.Muted, wrap: true));
            if (raising.Shaping != null) box.AddChild(UiTheme.HandLabel(raising.Shaping, 22, UiTheme.Accent));
            _personDetail.AddChild(Ui.Card(box));
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
        Ui.Clear(_treeTab);
        int focus = _treeFocusId is { } id && S.World.TryGet(id) != null ? id : S.Player.Id;
        var person = S.World.Get(focus);

        var header = Ui.HBox(12);
        var title = Ui.Label(focus == S.Player.Id ? "Your family" : $"The family of {person.FullName}", 26, UiTheme.Text);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);
        if (focus != S.Player.Id)
        {
            var back = Ui.Button("Back to you", () => { _treeFocusId = null; RefreshTree(); FocusDefault(); }, 42);
            RegisterHint(back, "Show the family around yourself again.");
            header.AddChild(back);
        }
        _treeTab.AddChild(header);
        // What the family has become known for (hidden until it happens).
        var known = S.FamilyTraits();
        if (known.Count > 0)
            _treeTab.AddChild(UiTheme.HandLabel($"The {S.World.FamilyName} family: " + string.Join(", ", known.Select(t => t.Name.ToLowerInvariant())), 24, UiTheme.Accent, wrap: true));
        _treeTab.AddChild(Ui.Label("Choose someone to see the family from their place in it. Choose the person in the middle to open their page.",
            15, UiTheme.Muted, wrap: true));

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill, FollowFocus = true };
        _treeView = new FamilyTreeView(S, focus,
            refocus: newFocus => { _treeFocusId = newFocus; RefreshTree(); FocusDefault(); },
            open: OpenPerson,
            hint: (control, text) => RegisterHint(control, text));
        var centre = new CenterContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        centre.AddChild(_treeView);
        scroll.AddChild(centre);
        _treeTab.AddChild(scroll);
    }

    /// <summary>Opens someone's page on the People tab (living or dead).</summary>
    private void OpenPerson(int id)
    {
        _selectedId = id;
        _tabs.CurrentTab = TabFamily;
    }

    // --- Smoke test (automated run through the real UI, see Main) ------------------------

    public void ShowTab(int tab) => _tabs.CurrentTab = tab;

    // Screenshot tour only.
    public void Refresh() => RefreshAll();
    public void TourInvestDialog() => ShowInvestDialog();
    public void TourHomeDialog() => ShowHomeDialog();

    /// <summary>Screenshot tour: the tree around the oldest played ancestor's father.</summary>
    public void FocusTreeOnGrandfather()
    {
        var father = S.World.Player.ParentIds.Select(S.World.Get).FirstOrDefault();
        _treeFocusId = father?.ParentIds.FirstOrDefault() is { } g and > 0 ? g : father?.Id;
        RefreshTree();
    }

    public string CurrentTabName => _tabs.GetTabTitle(_tabs.CurrentTab);

    public void SmokeStep(int step)
    {
        _tabs.CurrentTab = step % _tabs.GetTabCount();
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
