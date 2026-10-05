using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// Shown when the game starts: first who made it ("... presents", once a name is set in Studio), then
/// tree rings grow one year at a time, the name of the game fades in, and the page fades away to the
/// title screen. Any key, button or click skips it.
/// </summary>
public partial class StartupScreen : CanvasLayer
{
    private const double GrowTime = 1.6, HoldTime = 0.7, FadeTime = 0.5;
    private const double StudioIn = 0.7, StudioHold = 1.5, StudioOut = 0.6;
    private double _t;
    private Control _page = null!;
    private Control _drawing = null!;
    private Control? _studio;
    /// <summary>How long the studio card takes before the rings begin (none without a name).</summary>
    private double _offset;
    /// <summary>Time on the rings' clock.</summary>
    private double T => _t - _offset;

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
        if (Studio.Developer != "") _studio = StudioCard();
        _offset = _studio == null ? 0 : StudioIn + StudioHold + StudioOut;
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
        if (T < GrowTime + HoldTime) _t = _offset + GrowTime + HoldTime;
        if (_studio != null) _studio.Visible = false;
    }

    /// <summary>"... presents": the maker's name in light letters on dark, over the page.</summary>
    private Control StudioCard()
    {
        var card = new ColorRect { Color = new Color("2a1d14"), MouseFilter = Control.MouseFilterEnum.Ignore };
        card.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var centre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        centre.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        card.AddChild(centre);
        var col = Ui.VBox(10);
        col.Alignment = BoxContainer.AlignmentMode.Center;
        centre.AddChild(col);
        var name = Ui.Label(Studio.Developer, 64, new Color("f1e4c8"));
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.AddThemeFontOverride("font", new FontVariation { BaseFont = UiTheme.Masthead, SpacingGlyph = 3 });
        col.AddChild(name);
        var presents = Ui.Label("presents", 28, new Color("c9b48f"));
        presents.HorizontalAlignment = HorizontalAlignment.Center;
        presents.AddThemeFontOverride("font", UiTheme.Hand);
        col.AddChild(presents);
        _page.AddChild(card);
        return card;
    }

    public override void _Process(double delta)
    {
        _t += delta;
        if (_studio is { Visible: true })
        {
            // The name fades in out of the dark, stays, and the dark lifts off the page.
            float text = (float)Mathf.Clamp(_t / StudioIn, 0, 1) * (float)Mathf.Clamp((StudioIn + StudioHold + StudioOut * 0.5 - _t) / (StudioOut * 0.5), 0, 1);
            foreach (var child in _studio.GetChildren()) if (child is Control c) c.Modulate = new Color(1, 1, 1, text);
            _studio.Modulate = new Color(1, 1, 1, (float)Mathf.Clamp((_offset - _t) / (StudioOut * 0.5), 0, 1));
            if (_t >= _offset) _studio.Visible = false;
        }
        _drawing.QueueRedraw();
        double fadeStart = GrowTime + HoldTime;
        if (T > fadeStart) _page.Modulate = new Color(1, 1, 1, (float)(1 - (T - fadeStart) / FadeTime));
        if (T > fadeStart + FadeTime) QueueFree();
    }

    private partial class RingsDrawing : Control
    {
        private readonly StartupScreen _owner;
        public RingsDrawing(StartupScreen owner) { _owner = owner; MouseFilter = MouseFilterEnum.Ignore; }

        public override void _Draw()
        {
            float grown = (float)Mathf.Clamp(_owner.T / GrowTime, 0, 1);
            var centre = new Vector2(Size.X / 2, Size.Y * 0.4f);
            float radius = Mathf.Min(Size.X, Size.Y) * 0.2f;
            int year = UiTheme.Year;
            Emblems.Rings(this, centre, radius, 1970, grown: Mathf.Max(0.05f, grown));
            UiTheme.SetYear(year);
            // The name arrives as the last ring closes.
            float text = (float)Mathf.Clamp((_owner.T - GrowTime * 0.6) / 0.6, 0, 1);
            var accent = new Color("c05a18") with { A = text };
            var font = new FontVariation { BaseFont = UiTheme.Masthead, SpacingGlyph = 5 };
            const string name = "ONE MORE YEAR";
            int size = (int)(radius * 0.48f);
            float width = font.GetStringSize(name, HorizontalAlignment.Left, -1, size).X;
            DrawString(font, new Vector2((Size.X - width) / 2, centre.Y + radius * 1.62f), name, HorizontalAlignment.Left, -1, size, accent);
        }
    }
}
