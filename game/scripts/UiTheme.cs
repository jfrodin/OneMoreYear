using System;
using System.Collections.Generic;
using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// The visual identity: a family album on paper whose colours and headings follow the decade being
/// played: sepia and typewriter in the fifties, olive in the sixties, brown and orange in the seventies,
/// magenta in the eighties, teal in the nineties, clean and flat after 2000 (docs/design-decisions.md, "The look").
/// Each decade has its own look, which changes when the decade begins; call <see cref="SetYear"/> and rebuild the theme when
/// the year changes. Focus outlines are strong on purpose – the game must be playable with a controller.
/// </summary>
public static class UiTheme
{
    /// <summary>
    /// One decade's look. Each decade has its own, and it changes when the decade begins: a new
    /// chapter in the album (the chapter page marks it). Fonts and colours are chosen to feel like the time.
    /// </summary>
    private sealed record Era(int Year, string Heading, Color Paper, Color Card, Color Ink, Color Accent, Color Good, Color Bad, Color Info, Color PhotoTint);

    private static readonly Era[] Eras =
    {
        // Sepia and typewriter.
        new(1950, "SpecialElite", new("e3d4b2"), new("f2e8d0"), new("3a2a1a"), new("8a4a1f"), new("4d7535"), new("9b3526"), new("3d5b78"), new(1f, 0.87f, 0.68f)),
        // Olive, mustard and an elegant serif.
        new(1960, "PlayfairDisplay", new("e6dcbc"), new("f6efda"), new("2f2a1e"), new("5f7f2a"), new("4d7535"), new("9b3526"), new("3d6b8a"), new(1f, 0.92f, 0.8f)),
        // Brown and orange.
        new(1970, "Fraunces", new("ecd4a8"), new("f8e7c7"), new("47280f"), new("c05a18"), new("5a7a22"), new("a83a22"), new("2f6a82"), new(1f, 0.9f, 0.76f)),
        // Cool, with magenta.
        new(1980, "Rajdhani", new("dcd8e6"), new("f3f1f8"), new("201a38"), new("c42a78"), new("2f7d5c"), new("b3303c"), new("2a7fb8"), new(0.97f, 0.95f, 1.02f)),
        // Teal and purple.
        new(1990, "Rajdhani", new("dbe4e0"), new("f3f7f5"), new("1d2b2a"), new("23807d"), new("2f7d5c"), new("b3303c"), new("5a4f9e"), new(0.98f, 1f, 1f)),
        // Clean and flat, blue.
        new(2000, "Inter", new("e6e9ed"), new("fafbfc"), new("1e2731"), new("2a6fd0"), new("2f8a4e"), new("c0392b"), new("2a7fb8"), new(1f, 1f, 1f)),
        // Warm white and green.
        new(2010, "Inter", new("efece5"), new("fffdf9"), new("23262b"), new("0f7f73"), new("2f8a4e"), new("c0392b"), new("3a6fb0"), new(1f, 1f, 1f)),
        // A serif comes back; coral.
        new(2020, "Fraunces", new("f1ebe3"), new("fffaf4"), new("2a2420"), new("c9563a"), new("3f8a50"), new("b83a2e"), new("3a6fb0"), new(1f, 0.99f, 0.97f)),
        // The warm decade: sand and sage.
        new(2030, "Inter", new("ece6d6"), new("fbf8ef"), new("26291f"), new("5f7a3a"), new("3f8a50"), new("b5452e"), new("3d6f8a"), new(1f, 0.98f, 0.93f)),
        // Machines that answer: slate and electric cyan.
        new(2040, "Rajdhani", new("dde3e8"), new("f5f8fa"), new("17222b"), new("0f8fb0"), new("2f8a6e"), new("c03a4a"), new("4a5fc0"), new(0.97f, 1f, 1.02f)),
        // Quiet towns and long lives: moss and soft paper.
        new(2060, "Fraunces", new("e3e6da"), new("f7f9f1"), new("232a22"), new("4f7d5a"), new("3f8a50"), new("a8473a"), new("466f96"), new(0.98f, 1f, 0.96f)),
        // The great repair: earth and terracotta.
        new(2080, "PlayfairDisplay", new("eadfcf"), new("fbf5ec"), new("2e241c"), new("b0603a"), new("4f8040"), new("a83a2e"), new("3f6a8a"), new(1f, 0.95f, 0.88f)),
        // A new century: ivory and gold.
        new(2100, "PlayfairDisplay", new("efe9da"), new("fffcf3"), new("2a251a"), new("a8822a"), new("4a7f45"), new("a8402e"), new("3f6590"), new(1f, 0.97f, 0.9f)),
        // The old stories: blue grey, like an archive.
        new(2130, "Inter", new("e2e6ea"), new("f8fafb"), new("1f2630"), new("5a6f8f"), new("3f7f60"), new("a8443a"), new("3a6fb0"), new(0.98f, 0.99f, 1.02f)),
        // Back to the beginning: sepia and typewriter, the album closing its circle.
        new(2160, "SpecialElite", new("e3d4b2"), new("f2e8d0"), new("3a2a1a"), new("8a4a1f"), new("4d7535"), new("9b3526"), new("3d5b78"), new(1f, 0.87f, 0.68f)),
    };

    // Declared first: the fonts below are loaded through it during static initialisation.
    private static readonly Dictionary<string, Font> FontCache = new();

    private static int _year = 1970;

    // Current colours (the decade's).
    public static Color Background { get; private set; }
    public static Color Panel { get; private set; }
    public static Color PanelAlt { get; private set; }
    public static Color PanelHover { get; private set; }
    public static Color Border { get; private set; }
    public static Color Text { get; private set; }
    public static Color Muted { get; private set; }
    public static Color Faint { get; private set; }
    public static Color Accent { get; private set; }
    public static Color AccentDark { get; private set; }
    public static Color Good { get; private set; }
    public static Color Bad { get; private set; }
    public static Color Info { get; private set; }
    /// <summary>Photos look like prints of their time: sepia, faded, or true colour.</summary>
    public static Color PhotoTint { get; private set; }
    /// <summary>The card behind a portrait.</summary>
    public static Color PhotoBackdrop { get; private set; }

    // Fonts: the body and the handwriting stay; headings follow the era.
    public static Font Body { get; } = Load("Lora");
    public static Font Hand { get; } = Load("Caveat");
    public static Font Masthead { get; } = Load("PlayfairDisplay");
    public static Font Heading { get; private set; } = Load("Fraunces");

    public const int FontSize = 18;

    static UiTheme() => SetYear(1970);


    private static Font Load(string name)
    {
        if (FontCache.TryGetValue(name, out var cached)) return cached;
        var file = GD.Load<FontFile>($"res://fonts/{name}.ttf");
        Font font = file ?? ThemeDB.FallbackFont;
        FontCache[name] = font;
        return font;
    }

    /// <summary>The decades that have a look of their own (for choosing one in the settings).</summary>
    public static IReadOnlyList<int> LookYears => Array.ConvertAll(Eras, e => e.Year);

    /// <summary>Sets the look for a year. Returns true if anything changed (then rebuild the theme).</summary>
    public static bool SetYear(int year)
    {
        _year = year;
        // The decade's look, unchanged until the next decade begins.
        int i = 0;
        while (i < Eras.Length - 1 && year >= Eras[i + 1].Year) i++;
        var a = Eras[i];
        Color Mix(Func<Era, Color> c) => c(a);

        var old = Background;
        Background = Mix(e => e.Paper);
        Panel = Mix(e => e.Card);
        Text = Mix(e => e.Ink);
        Accent = Mix(e => e.Accent);
        Good = Mix(e => e.Good);
        Bad = Mix(e => e.Bad);
        Info = Mix(e => e.Info);
        PhotoTint = Mix(e => e.PhotoTint);
        PanelAlt = Panel.Lerp(Background, 0.55f);
        PanelHover = Background.Lerp(Accent, 0.12f);
        Border = Background.Lerp(Text, 0.25f);
        Muted = Text.Lerp(Background, 0.4f);
        Faint = Text.Lerp(Background, 0.58f);
        AccentDark = Accent.Darkened(0.35f);
        PhotoBackdrop = Background.Darkened(0.12f);
        Heading = Load(a.Heading);
        return old != Background;
    }

    /// <summary>How a photograph taken in a year looks (sepia in the fifties, faded in the seventies).</summary>
    public static Color PhotoTintFor(int year)
    {
        int i = 0;
        while (i < Eras.Length - 1 && year >= Eras[i + 1].Year) i++;
        return Eras[i].PhotoTint;
    }

    public static int Year => _year;

    public static StyleBoxFlat Box(Color bg, int radius = 4, Color? border = null, int borderWidth = 0, float margin = 12)
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

    /// <summary>A card on the page: paper with a soft shadow, like something glued into an album.</summary>
    public static StyleBoxFlat PaperCard(float margin = 16)
    {
        var sb = Box(Panel, 3, Border.Lerp(Panel, 0.4f), 1, margin);
        sb.ShadowColor = new Color(0.2f, 0.12f, 0.05f, 0.18f);
        sb.ShadowSize = 6;
        sb.ShadowOffset = new Vector2(2, 3);
        return sb;
    }

    public static StyleBoxFlat FocusRing()
    {
        // The ink colour, not the accent: in some decades the accent is red, and a red ring reads as a warning.
        var sb = new StyleBoxFlat { DrawCenter = false, BorderColor = new Color(Text, 0.7f) };
        sb.SetBorderWidthAll(3);
        sb.SetCornerRadiusAll(6);
        sb.SetExpandMarginAll(3);
        return sb;
    }

    public static Theme Build()
    {
        var t = new Theme { DefaultFontSize = FontSize, DefaultFont = Body };

        t.SetColor("font_color", "Label", Text);

        // Buttons: paper tabs with ink edges.
        var normal = Box(PanelAlt, 4, Border, 1);
        normal.ContentMarginLeft = normal.ContentMarginRight = 18;
        normal.ContentMarginTop = normal.ContentMarginBottom = 10;
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = PanelHover;
        hover.BorderColor = Accent;
        var pressed = (StyleBoxFlat)normal.Duplicate();
        pressed.BgColor = Background.Lerp(Accent, 0.22f);
        var disabled = (StyleBoxFlat)normal.Duplicate();
        disabled.BgColor = Background.Lerp(Panel, 0.3f);
        disabled.BorderColor = Border.Lerp(Background, 0.5f);
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
            t.SetColor("font_pressed_color", type, AccentDark);
            t.SetColor("font_hover_pressed_color", type, AccentDark);
            t.SetColor("font_disabled_color", type, Faint);
        }

        // Panels are cards on the page.
        t.SetStylebox("panel", "PanelContainer", PaperCard());
        t.SetStylebox("panel", "Panel", PaperCard());

        // Tabs: index tabs on the album's edge.
        var tabSelected = Box(Panel, 4, Border, 1, 12);
        tabSelected.BorderColor = Accent;
        tabSelected.BorderWidthTop = 3;
        tabSelected.BorderWidthBottom = 0;
        tabSelected.CornerRadiusBottomLeft = tabSelected.CornerRadiusBottomRight = 0;
        tabSelected.ContentMarginLeft = tabSelected.ContentMarginRight = 22;
        var tabUnselected = Box(Background.Darkened(0.04f), 4, null, 0, 12);
        tabUnselected.CornerRadiusBottomLeft = tabUnselected.CornerRadiusBottomRight = 0;
        tabUnselected.ContentMarginLeft = tabUnselected.ContentMarginRight = 22;
        var tabHover = (StyleBoxFlat)tabUnselected.Duplicate();
        tabHover.BgColor = PanelHover;
        foreach (var type in new[] { "TabContainer", "TabBar" })
        {
            t.SetStylebox("tab_selected", type, tabSelected);
            t.SetStylebox("tab_unselected", type, tabUnselected);
            t.SetStylebox("tab_hovered", type, tabHover);
            t.SetStylebox("tab_focus", type, FocusRing());
            t.SetColor("font_selected_color", type, AccentDark);
            t.SetColor("font_unselected_color", type, Muted);
            t.SetColor("font_hovered_color", type, Text);
            t.SetFont("font", type, Heading);
        }
        var tabPanel = PaperCard(18);
        tabPanel.CornerRadiusTopLeft = 0;
        t.SetStylebox("panel", "TabContainer", tabPanel);

        // Inputs
        var input = Box(Panel.Lightened(0.3f), 3, Border, 1, 10);
        var inputFocus = Box(Panel.Lightened(0.3f), 3, Accent, 2, 10);
        foreach (var type in new[] { "LineEdit", "SpinBox", "TextEdit" })
        {
            t.SetStylebox("normal", type, input);
            t.SetStylebox("focus", type, inputFocus);
            t.SetColor("font_color", type, Text);
            t.SetColor("font_placeholder_color", type, Faint);
            t.SetColor("caret_color", type, Text);
        }

        // Progress bars: ink on paper.
        t.SetStylebox("background", "ProgressBar", Box(Background.Darkened(0.1f), 3, null, 0, 0));
        t.SetStylebox("fill", "ProgressBar", Box(Accent, 3, null, 0, 0));

        // Tree
        t.SetStylebox("panel", "Tree", Box(Panel, 4, null, 0, 8));
        t.SetStylebox("focus", "Tree", FocusRing());
        t.SetStylebox("selected", "Tree", Box(PanelHover, 3, null, 0, 2));
        t.SetStylebox("selected_focus", "Tree", Box(PanelHover, 3, Accent, 1, 2));
        t.SetStylebox("cursor", "Tree", Box(new Color(0, 0, 0, 0), 3, Accent, 2, 2));
        t.SetStylebox("cursor_unfocused", "Tree", Box(new Color(0, 0, 0, 0), 3, Border, 1, 2));
        t.SetColor("font_color", "Tree", Text);
        t.SetColor("font_selected_color", "Tree", AccentDark);
        t.SetColor("children_hl_line_color", "Tree", Border);
        t.SetColor("relationship_line_color", "Tree", Border);
        t.SetConstant("v_separation", "Tree", 8);

        // Popups (OptionButton lists)
        t.SetStylebox("panel", "PopupMenu", Box(Panel, 4, Border, 1, 8));
        t.SetStylebox("hover", "PopupMenu", Box(PanelHover, 3, null, 0, 6));
        t.SetColor("font_color", "PopupMenu", Text);
        t.SetColor("font_hover_color", "PopupMenu", AccentDark);

        // Scroll bars
        t.SetStylebox("grabber", "VScrollBar", Box(Border, 4, null, 0, 4));
        t.SetStylebox("grabber_highlight", "VScrollBar", Box(Accent, 4, null, 0, 4));
        t.SetStylebox("scroll", "VScrollBar", Box(new Color(0, 0, 0, 0), 4, null, 0, 4));

        // Tooltips (never the only way to see information – see UI rules in the spec)
        t.SetStylebox("panel", "TooltipPanel", Box(Panel, 4, Border, 1, 10));
        t.SetColor("font_color", "TooltipLabel", Text);

        return t;
    }

    /// <summary>The call-to-action button (Next Year, New Life): the era's accent colour.</summary>
    public static void MakePrimary(Button b)
    {
        var normal = Box(Accent, 5, null, 0, 14);
        var hover = Box(Accent.Lightened(0.12f), 5, null, 0, 14);
        var pressed = Box(AccentDark, 5, null, 0, 14);
        var disabled = Box(Background.Darkened(0.08f), 5, Border, 1, 14);
        foreach (var box in new[] { normal, hover, pressed })
        {
            box.ShadowColor = new Color(0.2f, 0.12f, 0.05f, 0.25f);
            box.ShadowSize = 4;
            box.ShadowOffset = new Vector2(1, 2);
        }
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("pressed", pressed);
        b.AddThemeStyleboxOverride("hover_pressed", pressed);
        b.AddThemeStyleboxOverride("disabled", disabled);
        var focus = FocusRing();
        focus.BorderColor = Text;
        b.AddThemeStyleboxOverride("focus", focus);
        var light = Panel.Lightened(0.2f);
        b.AddThemeColorOverride("font_color", light);
        b.AddThemeColorOverride("font_hover_color", light);
        b.AddThemeColorOverride("font_focus_color", light);
        b.AddThemeColorOverride("font_pressed_color", light);
        b.AddThemeColorOverride("font_hover_pressed_color", light);
        b.AddThemeColorOverride("font_disabled_color", Faint);
        b.AddThemeFontOverride("font", Heading);
        b.AddThemeFontSizeOverride("font_size", 24);
    }

    /// <summary>A heading in the era's typeface.</summary>
    public static Label HeadingLabel(string text, int size, Color? color = null)
    {
        var l = Ui.Label(text, size, color ?? Text);
        l.AddThemeFontOverride("font", Heading);
        return l;
    }

    /// <summary>Handwriting, for memories and notes in the album's margins.</summary>
    public static Label HandLabel(string text, int size, Color? color = null, bool wrap = false)
    {
        var l = Ui.Label(text, size, color ?? Text, wrap);
        l.AddThemeFontOverride("font", Hand);
        return l;
    }
}
