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
        box.AddChild(UiTheme.CaptionLabel($"{name}'s album", 36, UiTheme.Accent));
        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 14);
        foreach (var photo in session.Album(personId))
        {
            var frame = Ui.VBox(4);
            frame.CustomMinimumSize = new Vector2(220, 0);
            var picture = new CenterContainer();
            var faces = Ui.HBox(4);
            var face = Portrait.Create(photo.Portrait, false, photo.With == null ? 150 : 104);
            face.Modulate = UiTheme.PhotoTintFor(photo.Year);
            faces.AddChild(face);
            if (photo.With != null)
            {
                var other = Portrait.Create(photo.With, false, 104);
                other.Modulate = UiTheme.PhotoTintFor(photo.Year);
                faces.AddChild(other);
            }
            picture.AddChild(faces);
            frame.AddChild(picture);
            // Who is in the picture, so a caption about a child is not read as a photo of the child alone.
            string who = photo.WithName != null ? $"{photo.Name} and {photo.WithName}" : photo.Name;
            string when = photo.Age == 0 ? $"{photo.Year}" : $"{photo.Year}  ·  {photo.Name} {photo.Age}";
            frame.AddChild(Ui.Label(photo.WithName != null ? $"{when}  ·  {who}" : when, 13, UiTheme.Faint, wrap: true));
            frame.AddChild(UiTheme.CaptionLabel(photo.Caption, 20, UiTheme.Text, wrap: true));
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
