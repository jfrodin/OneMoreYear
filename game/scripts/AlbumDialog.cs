using Godot;
using OneMoreYear.Simulation;

namespace OneMoreYear.Game;

/// <summary>A life in photographs: a grid of portraits at each age, each tinted like a print of its year.</summary>
public static class AlbumDialog
{
    public static void Show(Main main, GameSession session, int personId, string name)
    {
        var box = Ui.VBox(12);
        box.CustomMinimumSize = new Vector2(980, 0);
        box.AddChild(UiTheme.HandLabel($"{name}'s album", 34, UiTheme.Accent));
        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 14);
        foreach (var photo in session.Album(personId))
        {
            var frame = Ui.VBox(4);
            frame.CustomMinimumSize = new Vector2(220, 0);
            var picture = new CenterContainer();
            var face = Portrait.Create(photo.Portrait, false, 150);
            face.Modulate = UiTheme.PhotoTintFor(photo.Year);
            picture.AddChild(face);
            frame.AddChild(picture);
            frame.AddChild(Ui.Label(photo.Age == 0 ? $"{photo.Year}" : $"{photo.Year}  ·  age {photo.Age}", 13, UiTheme.Faint));
            frame.AddChild(UiTheme.HandLabel(photo.Caption, 20, UiTheme.Text, wrap: true));
            grid.AddChild(Ui.Card(frame));
        }
        var scroll = Ui.Scroll(grid);
        scroll.CustomMinimumSize = new Vector2(0, Mathf.Min(620, main.GetViewportRect().Size.Y - 200));
        box.AddChild(scroll);
        System.Action close = () => { };
        var done = Ui.Button("Close the album", () => close(), 46);
        done.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        box.AddChild(done);
        close = main.ShowDialog(box, done);
    }
}
