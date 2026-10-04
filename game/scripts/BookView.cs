using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// The play screen as an open family album: a left page (you), a right page (the year, or whatever
/// bookmark is open), a spine in the middle with its shadow, and the edges of the pages beneath.
/// Children are laid out with anchors: <see cref="Left"/> and <see cref="Right"/> hold the pages.
/// </summary>
public partial class BookView : Control
{
    /// <summary>Where the spine is: wide enough for the left page's content at any window size.</summary>
    public float Spine => Single ? Size.X : Mathf.Max(MinLeft, Size.X * Share);
    /// <summary>The left page's share of the book, and its smallest width in pixels.</summary>
    public float Share { get; set; } = 0.31f;
    public float MinLeft { get; set; } = 490;
    /// <summary>One wide page (a last page, a certificate) instead of two.</summary>
    public bool Single { get; set; }

    /// <summary>The desk and an open album filling a screen, with room on the right for bookmarks if wanted.</summary>
    public static BookView OpenOn(Control screen, float share, float right = 26, bool single = false)
    {
        var desk = new Desk();
        desk.SetAnchorsPreset(LayoutPreset.FullRect);
        screen.AddChild(desk);
        var book = new BookView { Share = share, MinLeft = 360, Single = single };
        book.SetAnchorsPreset(LayoutPreset.FullRect);
        book.OffsetLeft = 26;
        book.OffsetTop = 22;
        book.OffsetBottom = -26;
        book.OffsetRight = -right;
        screen.AddChild(book);
        return book;
    }

    public MarginContainer Left { get; } = new();
    public MarginContainer Right { get; } = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Left.SetAnchorsPreset(LayoutPreset.FullRect);
        Left.AnchorRight = 0;
        Left.ClipContents = true;
        Pad(Left, 34, 34, 30, 26);
        AddChild(Left);
        Right.SetAnchorsPreset(LayoutPreset.FullRect);
        Right.AnchorLeft = 0;
        Right.ClipContents = true;
        Pad(Right, 44, 34, 26, 18);
        AddChild(Right);
        Resized += Layout;
        Layout();
    }

    private void Layout()
    {
        Left.OffsetLeft = 0;
        Left.OffsetRight = Spine;
        Right.OffsetLeft = Spine;
        Right.OffsetRight = 0;
        QueueRedraw();
    }

    private static void Pad(MarginContainer m, int left, int right, int top, int bottom)
    {
        m.AddThemeConstantOverride("margin_left", left);
        m.AddThemeConstantOverride("margin_right", right);
        m.AddThemeConstantOverride("margin_top", top);
        m.AddThemeConstantOverride("margin_bottom", bottom);
    }

    public override void _Draw()
    {
        var size = Size;
        float spine = Spine;
        var page = UiTheme.Panel;
        var ink = new Color(0.18f, 0.1f, 0.04f);

        // The book's shadow on the desk, soft.
        for (int i = 6; i >= 1; i--)
            DrawRect(new Rect2(new Vector2(4 + i, 8 + i), size + new Vector2(i * 2, i)), new Color(ink, 0.035f));

        // The pages beneath, at the outer edges.
        for (int i = 3; i >= 1; i--)
        {
            var edge = page.Darkened(0.05f * i);
            DrawRect(new Rect2(-i * 3, i * 2, 8, size.Y - i * 4), edge);
            DrawRect(new Rect2(size.X - 8 + i * 3, i * 2, 8, size.Y - i * 4), edge);
        }

        // The two pages.
        DrawRect(new Rect2(0, 0, spine, size.Y), page);
        DrawRect(new Rect2(spine, 0, size.X - spine, size.Y), page.Lightened(0.02f));

        // The spine: a fold that falls into shadow on both sides.
        if (Single) return;
        for (int i = 0; i < 26; i++)
        {
            float a = 0.13f * (1 - i / 26f) * (1 - i / 26f);
            DrawRect(new Rect2(spine - i - 1, 0, 1, size.Y), new Color(ink, a));
            DrawRect(new Rect2(spine + i, 0, 1, size.Y), new Color(ink, a * 1.2f));
        }
        DrawLine(new Vector2(spine, 0), new Vector2(spine, size.Y), new Color(ink, 0.25f), 1);
    }

    /// <summary>The page before lifts and turns over the spine, showing the new year beneath.</summary>
    public void TurnPage()
    {
        var sheet = new TurningSheet { MouseFilter = MouseFilterEnum.Ignore };
        float spine = Spine;
        sheet.Position = new Vector2(spine, 0);
        sheet.Size = new Vector2(Size.X - spine, Size.Y);
        sheet.PivotOffset = new Vector2(0, Size.Y / 2);
        AddChild(sheet);
        var tween = CreateTween();
        tween.TweenProperty(sheet, "scale:x", 0f, 0.55).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(sheet.QueueFree));
    }

    /// <summary>The old page while it turns: paper that darkens towards the fold as it lifts.</summary>
    private partial class TurningSheet : Control
    {
        public override void _Process(double delta) => QueueRedraw();

        public override void _Draw()
        {
            var page = UiTheme.Panel.Lightened(0.02f);
            DrawRect(new Rect2(Vector2.Zero, Size), page);
            // The more it has turned, the more of it is in shadow.
            float lift = 1 - Scale.X;
            DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.1f, 0.06f, 0.02f, 0.25f * lift));
            for (int i = 0; i < 40; i++)
                DrawRect(new Rect2(Size.X - i - 1, 0, 1, Size.Y), new Color(0.1f, 0.06f, 0.02f, 0.12f * (1 - i / 40f)));
        }
    }
}

/// <summary>The desk the album lies on: dark wood with a grain, darker towards the edges of the light.</summary>
public partial class Desk : Control
{
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        var size = Size;
        // Wood that takes a little of the decade's colour, so it belongs to the same room.
        var wood = new Color("5a3d28").Lerp(UiTheme.Accent.Darkened(0.55f), 0.18f);
        DrawRect(new Rect2(Vector2.Zero, size), wood);
        var rng = new System.Random(7);
        for (int i = 0; i < 90; i++)
        {
            float y = (float)rng.NextDouble() * size.Y;
            float amp = 2 + (float)rng.NextDouble() * 6;
            float freq = 0.002f + (float)rng.NextDouble() * 0.004f;
            float phase = (float)rng.NextDouble() * 10;
            var pts = new Vector2[48];
            for (int k = 0; k < pts.Length; k++)
            {
                float x = size.X * k / (pts.Length - 1);
                pts[k] = new Vector2(x, y + Mathf.Sin(x * freq + phase) * amp);
            }
            bool light = rng.NextDouble() < 0.4;
            DrawPolyline(pts, light ? new Color(1, 0.9f, 0.75f, 0.035f) : new Color(0.1f, 0.05f, 0.02f, 0.09f), 1 + (float)rng.NextDouble() * 2.5f, true);
        }
        // A lamp somewhere above: lighter in the middle, dark in the corners.
        var g = new Gradient();
        g.Offsets = new[] { 0f, 0.55f, 1f };
        g.Colors = new[] { new Color(1, 0.95f, 0.85f, 0.08f), new Color(0, 0, 0, 0), new Color(0, 0, 0, 0.45f) };
        var tex = new GradientTexture2D { Gradient = g, Width = 128, Height = 128, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.45f), FillTo = new Vector2(1.15f, 0.45f) };
        DrawTextureRect(tex, new Rect2(Vector2.Zero, size), false);
    }
}
