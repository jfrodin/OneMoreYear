using System.Linq;
using Godot;
using OneMoreYear.Simulation;
using OneMoreYear.Simulation.Systems;

namespace OneMoreYear.Game;

/// <summary>
/// Development aid: --portraits=out.png draws a family's portraits and one person at different ages,
/// saves the image and quits. Used to check how generated faces and inheritance look.
/// </summary>
public partial class PortraitGallery : Control
{
    private readonly string _path;
    private int _frames;

    private readonly bool _big;

    public PortraitGallery(string path) { _path = path; _big = path.Contains("big"); }

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        var session = GameSession.NewGame(new NewGameOptions { Seed = 5, StartYear = 1940 });
        var bot = new AutoPlayer(5);
        for (int i = 0; i < 70; i++) bot.PlayYear(session);

        var col = Ui.VBox(8);
        col.Position = new Vector2(16, 16);
        AddChild(col);

        // Blood relatives first, so resemblance is easy to see.
        var family = session.Family(includeDead: false).Take(_big ? 4 : 16).ToList();
        var grid = new GridContainer { Columns = _big ? 4 : 8 };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 6);
        foreach (var p in family)
        {
            var cell = Ui.VBox(2);
            cell.AddChild(Portrait.Create(session.Portrait(p.Id), p.Id == session.Player.Id, _big ? 380 : 170));
            cell.AddChild(Ui.Label($"{p.FirstName}, {p.Age}", 13, UiTheme.Muted));
            cell.AddChild(Ui.Label(p.RoleLabel, 12, UiTheme.Faint));
            grid.AddChild(cell);
        }
        col.AddChild(grid);

        // The player through a lifetime.
        var ages = Ui.HBox(10);
        var me = session.Portrait(session.Player.Id);
        var person = session.World.Get(session.Player.Id);
        foreach (int age in _big ? new[] { 5, 22, 45, 80 } : new[] { 0, 5, 14, 22, 40, 60, 80, 92 })
        {
            var at = me with
            {
                Age = age, Alive = true, Grey = Faces.GreyAt(me.Face, age), Bald = Faces.BaldAt(me.Face, person.Sex, age),
                Glasses = age >= me.Face.GlassesFromAge,
            };
            var cell = Ui.VBox(2);
            cell.AddChild(Portrait.Create(at with { Year = person.BirthYear + age }, false, _big ? 380 : 170));
            cell.AddChild(Ui.Label($"age {age}", 13, UiTheme.Muted));
            ages.AddChild(cell);
        }
        col.AddChild(ages);
    }

    public override void _Process(double delta)
    {
        if (++_frames < 5) return;
        GetViewport().GetTexture().GetImage().SavePng(_path);
        GD.Print($"Portrait gallery saved to {_path}");
        GetTree().Quit();
    }
}
