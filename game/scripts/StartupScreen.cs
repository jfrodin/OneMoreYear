using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// Shown when the game starts: tree rings grow one year at a time, the name fades in, and the page
/// fades away to the title screen. Any key, button or click skips it.
/// </summary>
public partial class StartupScreen : CanvasLayer
{
    private const double GrowTime = 1.6, HoldTime = 0.7, FadeTime = 0.5;
    private double _t;
    private Control _page = null!;
    private Control _drawing = null!;

    public override void _Ready()
    {
        Layer = 100;
        _page = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        _page.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_page);
        var paper = new AlbumPaper();
        paper.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _page.AddChild(paper);
        _drawing = new RingsDrawing(this);
        _drawing.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _page.AddChild(_drawing);
        _page.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) Skip(); };
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true } or InputEventJoypadButton { Pressed: true })
        {
            GetViewport().SetInputAsHandled();
            Skip();
        }
    }

    private void Skip()
    {
        if (_t < GrowTime + HoldTime) _t = GrowTime + HoldTime;
    }

    public override void _Process(double delta)
    {
        _t += delta;
        _drawing.QueueRedraw();
        double fadeStart = GrowTime + HoldTime;
        if (_t > fadeStart) _page.Modulate = new Color(1, 1, 1, (float)(1 - (_t - fadeStart) / FadeTime));
        if (_t > fadeStart + FadeTime) QueueFree();
    }

    private partial class RingsDrawing : Control
    {
        private readonly StartupScreen _owner;
        public RingsDrawing(StartupScreen owner) { _owner = owner; MouseFilter = MouseFilterEnum.Ignore; }

        public override void _Draw()
        {
            float grown = (float)Mathf.Clamp(_owner._t / GrowTime, 0, 1);
            var centre = new Vector2(Size.X / 2, Size.Y * 0.4f);
            float radius = Mathf.Min(Size.X, Size.Y) * 0.2f;
            int year = UiTheme.Year;
            Emblems.Rings(this, centre, radius, 1970, grown: Mathf.Max(0.05f, grown));
            UiTheme.SetYear(year);
            // The name arrives as the last ring closes.
            float text = (float)Mathf.Clamp((_owner._t - GrowTime * 0.6) / 0.6, 0, 1);
            var accent = new Color("c05a18") with { A = text };
            var font = new FontVariation { BaseFont = UiTheme.Masthead, SpacingGlyph = 5 };
            const string name = "ONE MORE YEAR";
            int size = (int)(radius * 0.48f);
            float width = font.GetStringSize(name, HorizontalAlignment.Left, -1, size).X;
            DrawString(font, new Vector2((Size.X - width) / 2, centre.Y + radius * 1.62f), name, HorizontalAlignment.Left, -1, size, accent);
        }
    }
}
