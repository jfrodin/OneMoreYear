using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// Development aid: --splash=PATH draws the start screen image (the title on the album's paper, in
/// the game's own typefaces), saves it and quits. The engine shows it while the game loads.
/// </summary>
public partial class SplashMaker : Control
{
    private readonly string _path;
    private int _frames;
    private const int W = 960, H = 400;

    public SplashMaker(string path) => _path = path;

    public override void _Ready()
    {
        UiTheme.SetYear(1970);
        var paper = new ColorRect { Color = new Color(0.925f, 0.831f, 0.659f), Size = new Vector2(W, H) };
        AddChild(paper);
        var col = Ui.VBox(4);
        col.Position = new Vector2(0, 70);
        col.Size = new Vector2(W, H - 70);
        var title = Ui.Label("ONE MORE YEAR", 96, UiTheme.Accent);
        title.AddThemeFontOverride("font", UiTheme.Masthead);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(title);
        var tagline = UiTheme.HandLabel("Live a life. Build a family. Leave a legacy.", 36, UiTheme.Muted);
        tagline.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(tagline);
        if (Studio.Developer != "")
        {
            var by = Ui.Label(Studio.Developer, 20, UiTheme.Faint);
            by.HorizontalAlignment = HorizontalAlignment.Center;
            col.AddChild(Ui.Spacer(30));
            col.AddChild(by);
        }
        AddChild(col);
    }

    public override void _Process(double delta)
    {
        if (++_frames < 6) return;
        var image = GetViewport().GetTexture().GetImage().GetRegion(new Rect2I(0, 0, W, H));
        image.SavePng(_path);
        GD.Print($"Splash saved to {_path}");
        GetTree().Quit();
    }
}
