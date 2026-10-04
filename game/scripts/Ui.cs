using System;
using Godot;

namespace OneMoreYear.Game;

/// <summary>Small helpers for building UI in code.</summary>
public static class Ui
{
    public static Label Label(string text, int size = UiTheme.FontSize, Color? color = null, bool wrap = false)
    {
        var l = new Label { Text = text };
        if (size != UiTheme.FontSize) l.AddThemeFontSizeOverride("font_size", size);
        // Headings use the era's typeface (UiTheme); body text stays in the book face.
        if (size >= 22) l.AddThemeFontOverride("font", UiTheme.Heading);
        if (color is { } c) l.AddThemeColorOverride("font_color", c);
        if (wrap)
        {
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            l.CustomMinimumSize = new Vector2(100, 0);
        }
        return l;
    }

    public static Button Button(string text, Action onPressed, int minHeight = 48)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, minHeight), FocusMode = Control.FocusModeEnum.All };
        b.Pressed += () => Sound.Play("click");
        b.Pressed += onPressed;
        return b;
    }

    public static VBoxContainer VBox(int separation = 10)
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", separation);
        return v;
    }

    public static HBoxContainer HBox(int separation = 10)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", separation);
        return h;
    }

    public static MarginContainer Margin(Control child, int all)
    {
        var m = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            m.AddThemeConstantOverride(side, all);
        m.AddChild(child);
        return m;
    }

    public static PanelContainer Card(Control content, Color? background = null, Color? border = null)
    {
        var p = new PanelContainer();
        if (background != null || border != null)
            p.AddThemeStyleboxOverride("panel", UiTheme.Box(background ?? UiTheme.Panel, 10, border ?? UiTheme.Border, 1, 16));
        p.AddChild(content);
        return p;
    }

    public static ScrollContainer Scroll(Control content)
    {
        var s = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            // Scrolling to the focus is done by FollowFocus() below, only when the player moves it.
            FollowFocus = false,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        s.AddChild(content);
        return s;
    }

    public static Control Spacer(float height)
    {
        return new Control { CustomMinimumSize = new Vector2(0, height) };
    }

    public static Control Separator()
    {
        var r = new ColorRect { Color = UiTheme.Border, CustomMinimumSize = new Vector2(0, 1) };
        return r;
    }

    /// <summary>A labelled 0–100 bar ("Health ████░░").</summary>
    public static Control Bar(string label, double value, Color color, string? valueText = null)
    {
        var row = HBox(12);
        var l = Label(label, 16, UiTheme.Muted);
        l.CustomMinimumSize = new Vector2(110, 0);
        row.AddChild(l);
        var bar = new ProgressBar
        {
            MinValue = 0, MaxValue = 100, Value = value, ShowPercentage = false,
            CustomMinimumSize = new Vector2(120, 12), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        bar.AddThemeStyleboxOverride("fill", UiTheme.Box(color, 5, null, 0, 0));
        row.AddChild(bar);
        if (valueText != null)
        {
            var v = Label(valueText, 16, UiTheme.Text);
            v.CustomMinimumSize = new Vector2(80, 0);
            row.AddChild(v);
        }
        return row;
    }

    public static Control Chip(string text, Color? color = null)
    {
        var p = new PanelContainer();
        var sb = UiTheme.Box(UiTheme.PanelAlt, 12, color ?? UiTheme.Border, 1, 4);
        sb.ContentMarginLeft = sb.ContentMarginRight = 10;
        p.AddThemeStyleboxOverride("panel", sb);
        p.AddChild(Label(text, 15, color ?? UiTheme.Text));
        return p;
    }

    /// <summary>Grabs focus at the end of the frame, if the control still exists and is visible by then.</summary>
    private static bool _quietFocus;

    /// <summary>
    /// Keeps the focused control in view when the player moves the focus (keyboard, gamepad), but not
    /// when the game sets it: a page must not jump to a button the game picked for convenience.
    /// </summary>
    public static void FollowFocus(Viewport viewport)
    {
        viewport.GuiFocusChanged += control =>
        {
            if (_quietFocus) return;
            for (var n = control.GetParent(); n != null; n = n.GetParent())
                if (n is ScrollContainer scroll) { scroll.EnsureControlVisible(control); break; }
        };
    }

    public static void FocusLater(Control? control)
    {
        if (control == null) return;
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(control) || !control.IsInsideTree() || !control.IsVisibleInTree()) return;
            _quietFocus = true;
            control.GrabFocus();
            _quietFocus = false;
        }).CallDeferred();
    }

    public static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>Makes every child ignore the mouse so a button with rich content still gets the clicks.</summary>
    public static void PassMouse(Control root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Control c)
            {
                c.MouseFilter = Control.MouseFilterEnum.Ignore;
                PassMouse(c);
            }
        }
    }

    /// <summary>Trait chip colour: green for good sides, red for dark ones, blue for the grey zone in between.</summary>
    public static Color ToneColor(string tone) => tone switch
    {
        "dark" => UiTheme.Bad,
        "odd" => UiTheme.Info,
        _ => UiTheme.Good
    };

    public static Color OpinionColor(double opinion) => opinion switch
    {
        >= 25 => UiTheme.Good,
        > -15 => UiTheme.Muted,
        _ => UiTheme.Bad
    };
}
