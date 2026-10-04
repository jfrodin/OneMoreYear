using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// The play screen as an open family album: a left page (you), a right page (the year, or whatever
/// bookmark is open), a spine in the middle with its shadow, and the edges of the pages beneath.
/// Children are laid out with anchors: <see cref="Left"/> and <see cref="Right"/> hold the pages.
/// </summary>
public partial class BookView : Control
{
    /// <summary>How much of the book the left page takes.</summary>
    public const float Split = 0.29f;

    public MarginContainer Left { get; } = new();
    public MarginContainer Right { get; } = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Left.SetAnchorsPreset(LayoutPreset.FullRect);
        Left.AnchorRight = Split;
        Pad(Left, 34, 34, 30, 26);
        AddChild(Left);
        Right.SetAnchorsPreset(LayoutPreset.FullRect);
        Right.AnchorLeft = Split;
        Pad(Right, 44, 34, 26, 18);
        AddChild(Right);
        Resized += QueueRedraw;
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
        float spine = size.X * Split;
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
        float spine = Size.X * Split;
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
