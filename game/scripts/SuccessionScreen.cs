using System.Linq;
using Godot;
using SimKinship = OneMoreYear.Simulation.Systems.Kinship;

namespace OneMoreYear.Game;

/// <summary>
/// Shown when the played character dies: their life on the left, the choice of who carries the
/// family on to the right.
/// </summary>
public partial class SuccessionScreen : Control
{
    private Main _main = null!;

    public void Init(Main main) => _main = main;

    public override void _Ready()
    {
        var s = _main.Session!;
        var dead = s.Player;
        var life = s.SummarizeLife(dead.Id);

        var columns = Ui.HBox(30);
        var margin = Ui.Margin(columns, 40);
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(margin);

        // Left: the life that ended.
        var left = Ui.VBox(14);
        var leftScroll = Ui.Scroll(left);
        leftScroll.SizeFlagsStretchRatio = 1.1f;
        columns.AddChild(leftScroll);

        left.AddChild(Ui.Label("IN MEMORIAM", 16, UiTheme.Muted));
        var header = Ui.HBox(20);
        header.AddChild(Portrait.Create(dead.Id, life.Name, false, true, 110));
        var names = Ui.VBox(4);
        names.AddChild(Ui.Label(life.Name, 40, UiTheme.Accent));
        names.AddChild(Ui.Label($"{life.BirthYear} – {life.DeathYear}", 24, UiTheme.Text));
        names.AddChild(Ui.Label($"Died of {life.Cause}, aged {life.Age}.", 19, UiTheme.Muted, wrap: true));
        header.AddChild(names);
        left.AddChild(header);

        var chips = new HFlowContainer();
        chips.AddThemeConstantOverride("h_separation", 8);
        chips.AddThemeConstantOverride("v_separation", 8);
        chips.AddChild(Ui.Chip(Plural(life.Children, "child", "children")));
        chips.AddChild(Ui.Chip(Plural(life.Grandchildren, "grandchild", "grandchildren")));
        chips.AddChild(Ui.Chip(Plural(life.Partners, "partner", "partners")));
        if (!string.IsNullOrEmpty(life.LastOccupation)) chips.AddChild(Ui.Chip(life.LastOccupation));
        chips.AddChild(Ui.Chip($"Wealthiest: {life.PeakWealth}"));
        left.AddChild(chips);

        var moments = life.Highlights.Where(h => h.Importance >= 3).TakeLast(16).ToList();
        if (moments.Count > 0)
        {
            var box = Ui.VBox(6);
            box.AddChild(Ui.Label("A life in moments", 20, UiTheme.Text));
            foreach (var m in moments)
                box.AddChild(Ui.Label($"{m.Year}   {m.Text}", 17, UiTheme.Muted, wrap: true));
            left.AddChild(Ui.Card(box));
        }

        // Right: who continues.
        var right = Ui.VBox(10);
        var rightScroll = Ui.Scroll(right);
        columns.AddChild(rightScroll);

        var heirs = s.HeirCandidates();
        Button? first = null;
        if (heirs.Count > 0)
        {
            right.AddChild(Ui.Label("Who carries the family forward?", 26, UiTheme.Text));
            right.AddChild(Ui.Label("You continue as someone who has already lived a life of their own – with their relationships, money, secrets and grudges.",
                17, UiTheme.Muted, wrap: true));
            string owner = SimKinship.Genitive(life.Name.Split(' ')[0]);
            foreach (var h in heirs.Take(12))
            {
                var id = h.Id;
                var b = new Button { CustomMinimumSize = new Vector2(0, 76), FocusMode = FocusModeEnum.All };
                var row = Ui.HBox(12);
                row.AddChild(Portrait.Create(h.Id, h.Name, true, false, 52));
                var col = Ui.VBox(2);
                col.AddChild(Ui.Label(h.Name, 19, UiTheme.Text));
                col.AddChild(Ui.Label($"{owner} {h.Relation.ToLowerInvariant()}, {h.Age}  ·  {h.Occupation}  ·  {h.Money}", 15, UiTheme.Muted));
                row.AddChild(col);
                var m = Ui.Margin(row, 10);
                m.SetAnchorsPreset(LayoutPreset.FullRect);
                b.AddChild(m);
                Ui.PassMouse(b);
                b.Pressed += () =>
                {
                    s.ChooseHeir(id);
                    _main.AutoSave();
                    _main.ShowGame();
                };
                right.AddChild(b);
                first ??= b;
            }
        }
        else
        {
            right.AddChild(Ui.Label("There is no one left to carry the family on.", 26, UiTheme.Text, wrap: true));
            first = Ui.Button("See how the story ended", () =>
            {
                s.EndGame();
                _main.AutoSave();
                _main.ShowGameOver();
            }, 58);
            UiTheme.MakePrimary(first);
            right.AddChild(first);
        }

        Ui.FocusLater(first);
    }

    private static string Plural(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";
}
