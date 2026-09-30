using System.Linq;
using Godot;

namespace OneMoreYear.Game;

/// <summary>The end of a family: the chronicle and the numbers are the result.</summary>
public partial class GameOverScreen : Control
{
    private Main _main = null!;

    public void Init(Main main) => _main = main;

    public override void _Ready()
    {
        var s = _main.Session!;
        var stats = s.Stats();

        var col = Ui.VBox(14);
        col.CustomMinimumSize = new Vector2(900, 0);
        var scroll = Ui.Scroll(Ui.Margin(col, 40));
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(scroll);

        col.AddChild(Ui.Label("THE END OF A FAMILY", 16, UiTheme.Muted));
        col.AddChild(Ui.Label($"The {s.World.FamilyName} family", 48, UiTheme.Accent));
        col.AddChild(Ui.Label($"{s.World.StartYear} – {s.Year}", 24, UiTheme.Text));
        col.AddChild(Ui.Label($"Seed {s.SeedCode}, starting {s.World.StartYear} – give it to a friend and see how their family turns out.", 16, UiTheme.Faint, wrap: true));

        var chips = new HFlowContainer();
        chips.AddThemeConstantOverride("h_separation", 8);
        chips.AddThemeConstantOverride("v_separation", 8);
        chips.AddChild(Ui.Chip($"{stats.Years} years"));
        chips.AddChild(Ui.Chip($"{stats.Generations} generations"));
        chips.AddChild(Ui.Chip($"{stats.Characters} lives played"));
        chips.AddChild(Ui.Chip($"{stats.FamilyMembers} family members"));
        chips.AddChild(Ui.Chip($"{stats.Divorces} divorces"));
        chips.AddChild(Ui.Chip($"{stats.Affairs} affairs exposed"));
        chips.AddChild(Ui.Chip($"Greatest fortune: {stats.LargestFortuneOwner}, {stats.LargestFortune}"));
        col.AddChild(chips);

        var menu = Ui.Button("Back to the main menu", () =>
        {
            SaveSystem.Delete(SaveSystem.CurrentSlot);
            _main.ShowTitle();
        }, 58);
        UiTheme.MakePrimary(menu);
        col.AddChild(menu);

        var box = Ui.VBox(4);
        box.AddChild(Ui.Label("The family chronicle", 22, UiTheme.Text));
        int? year = null;
        foreach (var line in s.Chronicle(3))
        {
            if (line.Year != year)
            {
                year = line.Year;
                box.AddChild(Ui.Label(line.Year.ToString(), 19, UiTheme.Accent));
            }
            box.AddChild(Ui.Label("   " + line.Text, 17, UiTheme.Muted, wrap: true));
        }
        col.AddChild(Ui.Card(box));

        Ui.FocusLater(menu);
    }
}
