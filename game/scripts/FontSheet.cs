using System.Collections.Generic;
using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// Development aid: --fonts=OUT.png --fontdir=DIR draws the same album page in a few sets of
/// typefaces side by side (the current set first), saves the image and quits. Used to choose fonts.
/// </summary>
public partial class FontSheet : Control
{
    private readonly string _path, _dir;
    private int _frames;

    public FontSheet(string path, string dir) { _path = path; _dir = dir; }

    private record TypeSet(string Name, Font Display, Font Heading, Font Body, Font Note, Font Caption, bool UpperTitle);

    private Font Load(string file, int weight = 0)
    {
        var path = file.StartsWith("res://") ? file : $"{_dir}/{file}";
        Font font;
        if (file.StartsWith("res://")) font = GD.Load<FontFile>(path);
        else { var f = new FontFile(); font = f.LoadDynamicFont(path) == Error.Ok ? f : ThemeDB.FallbackFont; }
        if (weight == 0) return font;
        return new FontVariation { BaseFont = font, VariationOpentype = new Godot.Collections.Dictionary { [TextServerManager.GetPrimaryInterface().NameToTag("wght")] = weight } };
    }

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        var desk = new Desk();
        desk.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(desk);

        var sets = new List<TypeSet>
        {
            new("Now", Load("res://fonts/PlayfairDisplay.ttf"), Load("res://fonts/Fraunces.ttf"), Load("res://fonts/Lora.ttf"), Load("res://fonts/Caveat.ttf"), Load("res://fonts/Caveat.ttf"), true),
            new("A · Old album", Load("CormorantGaramond.ttf", 600), Load("CormorantGaramond.ttf", 700), Load("EBGaramond.ttf", 420), Load("EBGaramond-Italic.ttf", 420), Load("LaBelleAurore.ttf"), true),
            new("B · Warm book", Load("res://fonts/PlayfairDisplay.ttf"), Load("LibreCaslonText.ttf", 700), Load("res://fonts/Lora.ttf"), Load("res://fonts/Lora-Italic.ttf"), Load("Cedarville-Cursive.ttf"), false),
            new("C · Quiet modern", Load("LibreCaslonText.ttf", 400), Load("SourceSerif4.ttf", 620), Load("SourceSerif4.ttf", 400), Load("SourceSerif4-Italic.ttf", 400), Load("HomemadeApple-Regular.ttf"), true),
        };

        var row = new HBoxContainer { Position = new Vector2(20, 20) };
        row.AddThemeConstantOverride("separation", 14);
        AddChild(row);
        foreach (var set in sets) row.AddChild(Page(set));
    }

    private Control Page(TypeSet s)
    {
        var paper = new StyleBoxFlat { BgColor = new Color("f5e6c4") };
        paper.SetContentMarginAll(22);
        paper.ShadowColor = new Color(0, 0, 0, 0.35f);
        paper.ShadowSize = 10;
        var page = new PanelContainer { CustomMinimumSize = new Vector2(362, 1000) };
        page.AddThemeStyleboxOverride("panel", paper);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        page.AddChild(col);
        var ink = new Color("3a2a1a"); var accent = new Color("b5541c"); var muted = new Color("7a6a55");

        void Text(string text, Font font, int size, Color colour, bool wrap = false)
        {
            var l = new Label { Text = text, AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off, CustomMinimumSize = new Vector2(wrap ? 318 : 0, 0) };
            l.AddThemeFontOverride("font", font);
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", colour);
            col.AddChild(l);
        }
        void Gap(int h) => col.AddChild(new Control { CustomMinimumSize = new Vector2(0, h) });

        Text(s.Name, ThemeDB.FallbackFont, 14, muted);
        Gap(8);
        Text(s.UpperTitle ? "ONE MORE\nYEAR" : "One More\nYear", s.Display, 54, accent);
        Text("Live a life. Build a family. Leave a legacy.", s.Note, 19, muted, wrap: true);
        Gap(14);
        foreach (var item in new[] { "Continue", "New life", "Achievements", "Settings" }) Text(item, s.Heading, 26, item == "Continue" ? accent : ink);
        Gap(18);
        Text("Karolina Pettersson", s.Heading, 28, ink);
        Text("Age 3, 1973", s.Caption, 24, accent);
        Text("Lives with parents in Stockholm", s.Note, 19, ink);
        Gap(14);
        Text("Your family", s.Heading, 26, ink);
        Text("Choose someone to see the family from their place in it. Your grandmother kept every letter she was ever sent, tied with string, in a box under the bed.", s.Body, 17, muted, wrap: true);
        Gap(14);
        Text("Ingrid, 1953", s.Caption, 26, ink);
        Text("Lennart and Birgitta, summer 1966", s.Caption, 22, ink);
        Gap(10);
        Text("Health  Excellent    Money  2 400 kr", s.Body, 17, ink);
        return page;
    }

    public override void _Process(double delta)
    {
        if (++_frames < 6) return;
        GetViewport().GetTexture().GetImage().SavePng(_path);
        GD.Print($"Font sheet saved to {_path}");
        GetTree().Quit();
    }
}
