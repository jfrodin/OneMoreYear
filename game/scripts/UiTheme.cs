using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// The visual identity: a dark, warm palette with a gold accent. Everything is built in code so the
/// look lives in one place. Focus outlines are strong on purpose – the game must be fully playable
/// with a controller.
/// </summary>
public static class UiTheme
{
    public static readonly Color Background = new("111318");
    public static readonly Color Panel = new("1a1d25");
    public static readonly Color PanelAlt = new("232733");
    public static readonly Color PanelHover = new("2c3141");
    public static readonly Color Border = new("323848");
    public static readonly Color Text = new("ece6da");
    public static readonly Color Muted = new("9b9588");
    public static readonly Color Faint = new("6b675f");
    public static readonly Color Accent = new("e0a84e");
    public static readonly Color AccentDark = new("7a5a24");
    public static readonly Color Good = new("7cc38a");
    public static readonly Color Bad = new("e07a6e");
    public static readonly Color Info = new("7aa7d9");

    public const int FontSize = 18;

    public static StyleBoxFlat Box(Color bg, int radius = 8, Color? border = null, int borderWidth = 0, float margin = 12)
    {
        var sb = new StyleBoxFlat { BgColor = bg };
        sb.SetCornerRadiusAll(radius);
        if (border is { } b)
        {
            sb.BorderColor = b;
            sb.SetBorderWidthAll(borderWidth);
        }
        sb.SetContentMarginAll(margin);
        return sb;
    }

    public static StyleBoxFlat FocusRing()
    {
        var sb = new StyleBoxFlat { DrawCenter = false, BorderColor = Accent };
        sb.SetBorderWidthAll(3);
        sb.SetCornerRadiusAll(9);
        sb.SetExpandMarginAll(2);
        return sb;
    }

    public static Theme Build()
    {
        var t = new Theme { DefaultFontSize = FontSize };

        // Labels
        t.SetColor("font_color", "Label", Text);

        // Buttons
        var normal = Box(PanelAlt, 8, Border, 1);
        normal.ContentMarginLeft = normal.ContentMarginRight = 18;
        normal.ContentMarginTop = normal.ContentMarginBottom = 10;
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = PanelHover;
        hover.BorderColor = Muted;
        var pressed = (StyleBoxFlat)normal.Duplicate();
        pressed.BgColor = new Color("363d52");
        var disabled = (StyleBoxFlat)normal.Duplicate();
        disabled.BgColor = new Color("1b1e26");
        disabled.BorderColor = new Color("262a35");
        foreach (var type in new[] { "Button", "OptionButton" })
        {
            t.SetStylebox("normal", type, normal);
            t.SetStylebox("hover", type, hover);
            t.SetStylebox("pressed", type, pressed);
            t.SetStylebox("hover_pressed", type, pressed);
            t.SetStylebox("disabled", type, disabled);
            t.SetStylebox("focus", type, FocusRing());
            t.SetColor("font_color", type, Text);
            t.SetColor("font_hover_color", type, Text);
            t.SetColor("font_focus_color", type, Text);
            t.SetColor("font_pressed_color", type, Accent);
            t.SetColor("font_hover_pressed_color", type, Accent);
            t.SetColor("font_disabled_color", type, Faint);
        }

        // Panels
        t.SetStylebox("panel", "PanelContainer", Box(Panel, 10, Border, 1, 16));
        t.SetStylebox("panel", "Panel", Box(Panel, 10, Border, 1, 16));

        // Tabs
        var tabSelected = Box(Panel, 8, null, 0, 12);
        tabSelected.BorderColor = Accent;
        tabSelected.BorderWidthTop = 3;
        tabSelected.CornerRadiusBottomLeft = tabSelected.CornerRadiusBottomRight = 0;
        tabSelected.ContentMarginLeft = tabSelected.ContentMarginRight = 22;
        var tabUnselected = Box(Background, 8, null, 0, 12);
        tabUnselected.CornerRadiusBottomLeft = tabUnselected.CornerRadiusBottomRight = 0;
        tabUnselected.ContentMarginLeft = tabUnselected.ContentMarginRight = 22;
        var tabHover = (StyleBoxFlat)tabUnselected.Duplicate();
        tabHover.BgColor = PanelAlt;
        foreach (var type in new[] { "TabContainer", "TabBar" })
        {
            t.SetStylebox("tab_selected", type, tabSelected);
            t.SetStylebox("tab_unselected", type, tabUnselected);
            t.SetStylebox("tab_hovered", type, tabHover);
            t.SetStylebox("tab_focus", type, FocusRing());
            t.SetColor("font_selected_color", type, Accent);
            t.SetColor("font_unselected_color", type, Muted);
            t.SetColor("font_hovered_color", type, Text);
        }
        var tabPanel = Box(Panel, 10, Border, 1, 18);
        tabPanel.CornerRadiusTopLeft = 0;
        t.SetStylebox("panel", "TabContainer", tabPanel);

        // Inputs
        var input = Box(PanelAlt, 6, Border, 1, 10);
        var inputFocus = Box(PanelAlt, 6, Accent, 2, 10);
        foreach (var type in new[] { "LineEdit", "SpinBox" })
        {
            t.SetStylebox("normal", type, input);
            t.SetStylebox("focus", type, inputFocus);
            t.SetColor("font_color", type, Text);
        }

        // Progress bars
        t.SetStylebox("background", "ProgressBar", Box(new Color("0d0f13"), 5, null, 0, 0));
        t.SetStylebox("fill", "ProgressBar", Box(Accent, 5, null, 0, 0));

        // Tree
        t.SetStylebox("panel", "Tree", Box(Panel, 8, null, 0, 8));
        t.SetStylebox("focus", "Tree", FocusRing());
        t.SetStylebox("selected", "Tree", Box(PanelHover, 4, null, 0, 2));
        t.SetStylebox("selected_focus", "Tree", Box(PanelHover, 4, Accent, 1, 2));
        t.SetStylebox("cursor", "Tree", Box(new Color(0, 0, 0, 0), 4, Accent, 2, 2));
        t.SetStylebox("cursor_unfocused", "Tree", Box(new Color(0, 0, 0, 0), 4, Border, 1, 2));
        t.SetColor("font_color", "Tree", Text);
        t.SetColor("font_selected_color", "Tree", Accent);
        t.SetConstant("v_separation", "Tree", 8);

        // Popups (OptionButton lists)
        t.SetStylebox("panel", "PopupMenu", Box(PanelAlt, 6, Border, 1, 8));
        t.SetStylebox("hover", "PopupMenu", Box(PanelHover, 4, null, 0, 6));
        t.SetColor("font_color", "PopupMenu", Text);
        t.SetColor("font_hover_color", "PopupMenu", Accent);

        // Tooltips (never the only way to see information – see UI rules in the spec)
        t.SetStylebox("panel", "TooltipPanel", Box(PanelAlt, 6, Border, 1, 10));
        t.SetColor("font_color", "TooltipLabel", Text);

        return t;
    }

    /// <summary>Gold call-to-action button (Next Year, Start).</summary>
    public static void MakePrimary(Button b)
    {
        var normal = Box(Accent, 10, null, 0, 14);
        var hover = Box(new Color("f0bd66"), 10, null, 0, 14);
        var pressed = Box(new Color("c48f3a"), 10, null, 0, 14);
        var disabled = Box(new Color("3a3526"), 10, null, 0, 14);
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("pressed", pressed);
        b.AddThemeStyleboxOverride("hover_pressed", pressed);
        b.AddThemeStyleboxOverride("disabled", disabled);
        var focus = FocusRing();
        focus.BorderColor = Text;
        b.AddThemeStyleboxOverride("focus", focus);
        var dark = new Color("1a1408");
        b.AddThemeColorOverride("font_color", dark);
        b.AddThemeColorOverride("font_hover_color", dark);
        b.AddThemeColorOverride("font_focus_color", dark);
        b.AddThemeColorOverride("font_pressed_color", dark);
        b.AddThemeColorOverride("font_hover_pressed_color", dark);
        b.AddThemeColorOverride("font_disabled_color", Muted);
        b.AddThemeFontSizeOverride("font_size", 22);
    }
}
