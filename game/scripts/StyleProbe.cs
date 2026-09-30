using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OneMoreYear.Simulation;

namespace OneMoreYear.Game;

/// <summary>
/// Development aid: --styleprobe=DIR draws the same content (an event, portraits, the chronicle) in
/// three visual directions and saves one image of each, so the producer can compare styles.
/// Uses Windows system fonts as stand-ins; a chosen style would ship with open fonts.
/// </summary>
public partial class StyleProbe : Control
{
    private readonly string _dir;
    private int _frames, _variant;
    private Control? _current;
    private GameSession _s = null!;
    private PortraitView _me = null!, _other = null!;
    private List<ChronicleLine> _lines = new();
    private string _otherName = "", _family = "";

    private static readonly string[] Names = { "A_album", "B_decades", "C_newspaper" };

    public StyleProbe(string dir) => _dir = dir;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _s = GameSession.NewGame(new NewGameOptions { SeedCode = "ALBUM", StartYear = 1950 });
        var bot = new AutoPlayer(3);
        for (int i = 0; i < 26; i++) bot.PlayYear(_s);
        var other = _s.Family().FirstOrDefault(p => p.RoleLabel is "mother" or "father" or "sister" or "brother") ?? _s.Family().First();
        _me = _s.Portrait(_s.Player.Id);
        _other = _s.Portrait(other.Id);
        _otherName = other.FirstName;
        _family = _s.World.FamilyName;
        _lines = _s.Chronicle(minImportance: 2).Where(l => l.Category != "world").TakeLast(5).ToList();
        Show(0);
    }

    private void Show(int variant)
    {
        _current?.QueueFree();
        _current = variant switch { 0 => Album(), 1 => Decades(), _ => Newspaper() };
        _current.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_current);
        _frames = 0;
    }

    public override void _Process(double delta)
    {
        if (++_frames < 6) return;
        GetViewport().GetTexture().GetImage().SavePng($"{_dir}/{Names[_variant]}.png");
        if (++_variant >= Names.Length) { GD.Print($"Style probes saved to {_dir}"); GetTree().Quit(); return; }
        Show(_variant);
    }

    // --- Shared content ----------------------------------------------------------------------

    private string EventTitle => "A spark";
    private string EventText => $"At a friend's party you end up talking to someone all evening. {_otherName} notices before you do: \"You're smiling like an idiot.\"";
    private readonly string[] _choices = { "Ask for their number   · 60 %", "Go home early", "Tell your best friend everything" };

    private static Font Sys(params string[] names) => new SystemFont { FontNames = names };

    private static Label L(string text, Font font, int size, Color color, bool wrap = false)
    {
        var l = Ui.Label(text, size, color, wrap);
        l.AddThemeFontOverride("font", font);
        return l;
    }

    private static StyleBoxFlat Box(Color bg, int radius = 0, Color? border = null, int borderWidth = 0, int pad = 16, int shadow = 0)
    {
        var b = new StyleBoxFlat { BgColor = bg, ShadowSize = shadow, ShadowColor = new Color(0, 0, 0, 0.35f), ShadowOffset = new Vector2(3, 5) };
        b.SetCornerRadiusAll(radius);
        b.SetBorderWidthAll(borderWidth);
        if (border is { } c) b.BorderColor = c;
        b.SetContentMarginAll(pad);
        return b;
    }

    private static PanelContainer Panel(Control content, StyleBox style)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", style);
        p.AddChild(content);
        return p;
    }

    // --- A: The family album -------------------------------------------------------------------

    private Control Album()
    {
        var root = new Paper();
        var serif = Sys("Georgia", "Times New Roman");
        var hand = Sys("Segoe Print", "Ink Free");
        var ink = new Color("3b2f25");
        var faded = new Color("7a6a55");

        var cols = Ui.HBox(60);
        var margin = Ui.Margin(cols, 60);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.AddChild(margin);

        // Left page: the player as a photo, with a handwritten caption.
        var left = Ui.VBox(18);
        left.CustomMinimumSize = new Vector2(420, 0);
        left.AddChild(L($"The {_family} family", serif, 44, ink));
        left.AddChild(L("Volume III · 1950 –", serif, 18, faded));
        left.AddChild(Ui.Spacer(10));
        left.AddChild(Photo(_me, $"{_s.Player.FirstName}, {_s.Player.Age(_s.Year)}", hand, -2.5f));
        var note = L($"\"{_s.Year}. Still don't know what I want to be.\"", hand, 22, new Color("2c4a7a"), wrap: true);
        left.AddChild(note);
        cols.AddChild(left);

        // Right page: the event as a letter pinned in the album, the chronicle as handwritten dates.
        var right = Ui.VBox(22);
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var letter = Ui.VBox(12);
        letter.AddChild(L(EventTitle, serif, 34, ink));
        letter.AddChild(L(EventText, serif, 21, ink, wrap: true));
        foreach (var c in _choices)
        {
            var row = Ui.HBox(10);
            row.AddChild(L("☐", serif, 22, faded));
            row.AddChild(L(c, serif, 20, ink));
            letter.AddChild(row);
        }
        var card = Panel(letter, Box(new Color("fbf6ea"), 2, new Color("d8ccb4"), 1, 28, shadow: 10));
        card.Rotation = Mathf.DegToRad(0.8f);
        right.AddChild(card);

        var memories = Ui.VBox(6);
        memories.AddChild(L("From the family chronicle", serif, 20, faded));
        foreach (var line in _lines)
            memories.AddChild(L($"{line.Year} – {line.Text}", hand, 17, ink, wrap: true));
        right.AddChild(memories);
        var small = Ui.HBox(24);
        small.AddChild(Photo(_other, _otherName, hand, 3f, 110));
        right.AddChild(small);
        cols.AddChild(right);
        return root;
    }

    /// <summary>A portrait as a printed photo with a white border and a caption.</summary>
    private static Control Photo(PortraitView view, string caption, Font hand, float degrees, float size = 190)
    {
        var box = Ui.VBox(6);
        var picture = new ColorRect { Color = new Color("c9b99a"), CustomMinimumSize = new Vector2(size, size) };
        var portrait = Portrait.Create(view, false, size * 1.35f);
        portrait.Position = new Vector2(-size * 0.175f, -size * 0.12f);
        picture.ClipContents = true;
        picture.AddChild(portrait);
        box.AddChild(picture);
        box.AddChild(L(caption, hand, (int)(size / 9 + 6), new Color("3b2f25")));
        var frame = Panel(box, Box(new Color("fdfbf5"), 1, null, 0, (int)(size / 14), shadow: 8));
        frame.Rotation = Mathf.DegToRad(degrees);
        frame.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        return frame;
    }

    /// <summary>Warm paper with faint fibres.</summary>
    private partial class Paper : Control
    {
        public override void _Ready() => Resized += QueueRedraw;

        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), new Color("ece2cc"));
            var rnd = new Random(7);
            for (int i = 0; i < 900; i++)
            {
                var p = new Vector2((float)rnd.NextDouble() * Size.X, (float)rnd.NextDouble() * Size.Y);
                DrawLine(p, p + new Vector2((float)rnd.NextDouble() * 14 - 7, (float)rnd.NextDouble() * 3), new Color(0.45f, 0.35f, 0.2f, 0.06f), 1);
            }
            // The fold between the pages.
            float x = Size.X * 0.36f;
            for (int i = 0; i < 18; i++) DrawLine(new Vector2(x + i, 0), new Vector2(x + i, Size.Y), new Color(0.3f, 0.2f, 0.1f, 0.012f * (18 - i)), 1);
        }
    }

    // --- B: The decades --------------------------------------------------------------------

    private sealed record Era(string Year, Color Bg, Color Card, Color Text, Color Accent, Font Title, Font Body, Color PhotoTint, int Radius, string Label);

    private Control Decades()
    {
        var root = new PanelContainer();
        root.AddThemeStyleboxOverride("panel", Box(new Color("1a1a1a"), 0, null, 0, 0));
        var eras = new[]
        {
            new Era("1958", new Color("d9c7a0"), new Color("efe3c6"), new Color("4a3521"), new Color("8a5a2b"), Sys("Times New Roman"), Sys("Times New Roman"), new Color(1f, 0.88f, 0.68f), 0, "Sepia, typewriter serif"),
            new Era("1976", new Color("e7c48f"), new Color("f6e2bd"), new Color("4b2a12"), new Color("d2691e"), Sys("Cooper Black", "Georgia"), Sys("Georgia"), new Color(1f, 0.85f, 0.7f), 18, "Brown and orange, round and soft"),
            new Era("1987", new Color("140f33"), new Color("211a52"), new Color("f1ecff"), new Color("ff3fa4"), Sys("Bahnschrift", "Consolas"), Sys("Bahnschrift", "Segoe UI"), new Color(0.9f, 0.85f, 1.1f), 4, "Neon on navy, sharp edges"),
            new Era("2012", new Color("f3f4f6"), new Color("ffffff"), new Color("1f2933"), new Color("2f7de1"), Sys("Segoe UI Light", "Segoe UI"), Sys("Segoe UI"), Colors.White, 12, "Clean, flat, lots of air"),
        };
        var row = Ui.HBox(0);
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var era in eras) row.AddChild(EraColumn(era));
        root.AddChild(row);
        return root;
    }

    private Control EraColumn(Era e)
    {
        var col = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        col.AddThemeStyleboxOverride("panel", Box(e.Bg, 0, null, 0, 26));
        var v = Ui.VBox(14);
        v.AddChild(L(e.Year, e.Title, 56, e.Accent));
        v.AddChild(L(e.Label, e.Body, 15, e.Text.Lerp(e.Bg, 0.35f), wrap: true));
        var card = Ui.VBox(10);
        var head = Ui.HBox(12);
        var portrait = Portrait.Create(_me, false, 72);
        portrait.Modulate = e.PhotoTint;
        head.AddChild(portrait);
        head.AddChild(L(EventTitle, e.Title, 30, e.Text));
        card.AddChild(head);
        card.AddChild(L(EventText, e.Body, 17, e.Text, wrap: true));
        foreach (var c in _choices.Take(2))
        {
            var b = Panel(L(c, e.Body, 16, e.Accent == new Color("ff3fa4") ? e.Accent : e.Text), Box(e.Bg.Lerp(e.Card, 0.4f), e.Radius, e.Accent, 2, 12));
            card.AddChild(b);
        }
        v.AddChild(Panel(card, Box(e.Card, e.Radius, e.Accent.Lerp(e.Card, 0.6f), 1, 20, shadow: e.Radius > 10 ? 6 : 0)));
        foreach (var line in _lines.Take(3))
            v.AddChild(L($"{line.Year}  {line.Text}", e.Body, 14, e.Text.Lerp(e.Bg, 0.25f), wrap: true));
        col.AddChild(v);
        return col;
    }

    // --- C: The newspaper ------------------------------------------------------------------

    private Control Newspaper()
    {
        var root = new PanelContainer();
        root.AddThemeStyleboxOverride("panel", Box(new Color("f2eee4"), 0, null, 0, 0));
        var serif = Sys("Georgia", "Times New Roman");
        var head = Sys("Times New Roman");
        var black = new Color("1b1b1b");
        var grey = new Color("5d5a52");

        var v = Ui.VBox(8);
        var m = Ui.Margin(v, 40);
        m.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.AddChild(m);

        var mast = L($"THE {_family.ToUpperInvariant()} CHRONICLE", head, 64, black);
        mast.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(mast);
        var dateline = L($"Founded 1950  ·  No. {_s.Year - 1949}  ·  {_s.Year}  ·  Price: one more year", serif, 16, grey);
        dateline.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(dateline);
        v.AddChild(Rule(black, 3));

        var cols = Ui.HBox(30);
        cols.SizeFlagsVertical = SizeFlags.ExpandFill;
        // Lead story: this year's event, with the choices as boxed "notices".
        var lead = Ui.VBox(10);
        lead.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        lead.SizeFlagsStretchRatio = 2;
        lead.AddChild(L(EventTitle.ToUpperInvariant() + " AT A PARTY", head, 46, black, wrap: true));
        lead.AddChild(L($"By our correspondent · {_s.Player.FullName}, {_s.Player.Age(_s.Year)}", serif, 15, grey));
        var body = Ui.HBox(20);
        var grey1 = _me with { Alive = false };
        var pic = Ui.VBox(4);
        pic.AddChild(Portrait.Create(grey1, false, 170));
        pic.AddChild(L($"{_s.Player.FirstName}: \"No comment.\"", serif, 13, grey));
        body.AddChild(pic);
        body.AddChild(L(EventText + " Friends describe the evening as \"a turning point, probably\". What happens next is up to the reader.", serif, 19, black, wrap: true));
        lead.AddChild(body);
        lead.AddChild(Rule(grey, 1));
        lead.AddChild(L("YOUR DECISION", head, 20, black));
        var notices = Ui.HBox(12);
        foreach (var c in _choices)
            notices.AddChild(Panel(L(c, serif, 16, black, wrap: true), Box(new Color("f2eee4"), 0, black, 2, 12)));
        foreach (var n in notices.GetChildren().OfType<Control>()) n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        lead.AddChild(notices);
        cols.AddChild(lead);

        // Side column: family news in short.
        var side = Ui.VBox(10);
        side.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        side.AddChild(L("FAMILY NEWS IN BRIEF", head, 22, black));
        side.AddChild(Rule(black, 1));
        foreach (var line in _lines)
        {
            side.AddChild(L(line.Year.ToString(), head, 18, black));
            side.AddChild(L(line.Text, serif, 15, grey, wrap: true));
        }
        side.AddChild(Rule(black, 1));
        var obit = Ui.HBox(10);
        obit.AddChild(Portrait.Create(_other with { Alive = false }, false, 64));
        obit.AddChild(L($"{_otherName} sends regards and asks why you never call.", serif, 15, grey, wrap: true));
        side.AddChild(obit);
        cols.AddChild(side);
        v.AddChild(cols);
        return root;
    }

    private static Control Rule(Color color, int thickness) =>
        new ColorRect { Color = color, CustomMinimumSize = new Vector2(0, thickness) };
}
