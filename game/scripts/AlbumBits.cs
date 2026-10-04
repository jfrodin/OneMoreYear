using System;
using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// Small pieces of the family album, used across screens: a photograph glued in with a strip of
/// tape, a line written in the margin, a link that is only ink until you point at it.
/// </summary>
public static partial class AlbumBits
{
    /// <summary>
    /// A print with a paper border and a shadow, a little crooked, held by a strip of tape. The
    /// content (usually a square portrait) keeps its own size.
    /// </summary>
    public static Control Print(Control content, Vector2 contentSize, float degrees = -2.5f, bool tape = true, int border = 9)
    {
        var size = contentSize + new Vector2(border * 2, border * 2 + 6);
        var holder = new Control { CustomMinimumSize = size + new Vector2(14, 16), MouseFilter = Control.MouseFilterEnum.Ignore };
        var paper = new StyleBoxFlat { BgColor = new Color("f8f3e8") };
        paper.SetContentMarginAll(border);
        paper.ContentMarginBottom = border + 6;
        paper.ShadowColor = new Color(0.15f, 0.09f, 0.03f, 0.3f);
        paper.ShadowSize = 8;
        paper.ShadowOffset = new Vector2(2, 4);
        var print = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(7, 8), Size = size };
        print.AddThemeStyleboxOverride("panel", paper);
        print.AddChild(content);
        print.PivotOffset = size / 2;
        print.RotationDegrees = degrees;
        holder.AddChild(print);
        if (tape)
        {
            var strip = new Tape { Size = new Vector2(size.X * 0.42f, 22), MouseFilter = Control.MouseFilterEnum.Ignore };
            strip.Position = new Vector2(7 + size.X / 2 - strip.Size.X / 2, 0);
            strip.PivotOffset = strip.Size / 2;
            strip.RotationDegrees = degrees * -1.6f + 2;
            holder.AddChild(strip);
        }
        return holder;
    }

    /// <summary>A strip of yellowed, slightly see-through tape with frayed ends.</summary>
    public partial class Tape : Control
    {
        public override void _Draw()
        {
            var c = new Color(0.96f, 0.91f, 0.74f, 0.62f);
            var pts = new[]
            {
                new Vector2(3, 0), new Vector2(Size.X - 2, 1), new Vector2(Size.X, Size.Y * 0.35f), new Vector2(Size.X - 3, Size.Y * 0.6f),
                new Vector2(Size.X - 1, Size.Y), new Vector2(2, Size.Y - 1), new Vector2(0, Size.Y * 0.62f), new Vector2(3, Size.Y * 0.3f),
            };
            DrawColoredPolygon(pts, c);
            DrawLine(new Vector2(4, 2), new Vector2(Size.X - 4, 3), new Color(1, 1, 1, 0.25f), 1);
        }
    }

    /// <summary>A link written in ink: no box, the accent colour and a line under it when pointed at.</summary>
    public static Button Link(string text, Action pressed, int size = 15)
    {
        var b = new Button { Text = text, Flat = true, FocusMode = Control.FocusModeEnum.All, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        var empty = new StyleBoxEmpty { ContentMarginLeft = 2, ContentMarginRight = 2, ContentMarginTop = 2, ContentMarginBottom = 2 };
        var under = new StyleBoxFlat { DrawCenter = false, BorderColor = UiTheme.Accent, BorderWidthBottom = 1, ContentMarginLeft = 2, ContentMarginRight = 2, ContentMarginTop = 2, ContentMarginBottom = 2 };
        foreach (var state in new[] { "normal", "pressed", "disabled" }) b.AddThemeStyleboxOverride(state, empty);
        foreach (var state in new[] { "hover", "focus", "hover_pressed" }) b.AddThemeStyleboxOverride(state, under);
        b.AddThemeColorOverride("font_color", UiTheme.Muted);
        b.AddThemeColorOverride("font_hover_color", UiTheme.Accent);
        b.AddThemeColorOverride("font_focus_color", UiTheme.Accent);
        b.AddThemeColorOverride("font_pressed_color", UiTheme.AccentDark);
        b.AddThemeFontSizeOverride("font_size", size);
        b.Pressed += pressed;
        return b;
    }
}

/// <summary>A sheet of paper laid on top of everything (a dialog): a deep shadow and two strips of tape.</summary>
public partial class PaperSheet : PanelContainer
{
    public PaperSheet()
    {
        var paper = UiTheme.Box(UiTheme.Panel, 3, null, 0, 24);
        paper.ShadowColor = new Color(0.08f, 0.04f, 0.01f, 0.45f);
        paper.ShadowSize = 26;
        paper.ShadowOffset = new Vector2(0, 10);
        AddThemeStyleboxOverride("panel", paper);
    }

    /// <summary>What lies on the sheet.</summary>
    public Control Content { set => AddChild(value); }

    public override void _Draw()
    {
        // Two strips of tape at the top corners, drawn over the paper's edge.
        foreach (var (x, angle) in new[] { (34f, -0.5f), (Size.X - 34f, 0.5f) })
        {
            DrawSetTransform(new Vector2(x, 2), angle, Vector2.One);
            var c = new Color(0.96f, 0.91f, 0.74f, 0.6f);
            DrawColoredPolygon(new[] { new Vector2(-38, -11), new Vector2(38, -12), new Vector2(40, 0), new Vector2(37, 11), new Vector2(-37, 12), new Vector2(-40, 1) }, c);
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}
