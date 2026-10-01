using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OneMoreYear.Simulation;

namespace OneMoreYear.Game;

/// <summary>
/// Development aid: --brand=DIR renders logo, icon, splash and store art proposals as PNG files with
/// the game's own fonts and colours, for the producer to compare (docs/brand/index.html shows them).
/// The emblems are drawn in code (<see cref="Emblems"/>), so the startup screen can use them too.
/// </summary>
public partial class BrandSheet : Node
{
    private readonly string _dir;
    private readonly Queue<(string Name, Vector2I Size, bool Transparent, Action<Control> Build)> _jobs = new();
    private SubViewport? _viewport;
    private string _current = "";
    private int _frames;
    private GameSession _family = null!;

    public BrandSheet(string dir) => _dir = dir;

    public override void _Ready()
    {
        System.IO.Directory.CreateDirectory(_dir);
        _family = GameSession.NewGame(new NewGameOptions { SeedCode = "ALBUM", StartYear = 1950 });
        var bot = new AutoPlayer(5);
        for (int i = 0; i < 62 && bot.PlayYear(_family); i++) { }

        // --- Emblems as app icons -------------------------------------------------------------
        foreach (var (id, draw) in new (string, Action<Control, Vector2, float>)[]
                 {
                     ("rings", (c, p, r) => Emblems.Rings(c, p, r, 1970)),
                     ("candle", (c, p, r) => Emblems.Candle(c, p, r)),
                     ("photo", (c, p, r) => Emblems.Photo(c, p, r)),
                     ("sprout", (c, p, r) => Emblems.RingsWithSprout(c, p, r)),
                 })
        {
            foreach (int size in new[] { 256, 64, 32 })
                Add($"icon_{id}_{size}", new Vector2I(size, size), true, c => Canvas(c, cv =>
                {
                    Emblems.IconTile(cv, new Rect2(Vector2.Zero, cv.Size));
                    draw(cv, cv.Size / 2, cv.Size.X * 0.36f);
                }));
        }

        // --- Wordmarks ------------------------------------------------------------------------
        Add("wordmark_playfair", new Vector2I(1400, 320), true, c => Canvas(c, cv => Wordmark(cv, UiTheme.Masthead, 120, 0.06f, "ONE MORE YEAR", new Color("c05a18"))));
        Add("wordmark_fraunces", new Vector2I(1400, 320), true, c => Canvas(c, cv => Wordmark(cv, Font("Fraunces"), 124, 0f, "One More Year", new Color("47280f"))));
        Add("wordmark_hand", new Vector2I(1400, 320), true, c => Canvas(c, cv => Wordmark(cv, UiTheme.Hand, 150, 0f, "one more year", new Color("8a4a1f"))));
        Add("wordmark_eras", new Vector2I(1600, 900), false, Eras);

        // --- Lockups --------------------------------------------------------------------------
        Add("lockup_rings_horizontal", new Vector2I(1600, 520), false, c => Paper(c, 1970, cv =>
        {
            Emblems.Rings(cv, new Vector2(330, 260), 170, 1970);
            Text(cv, UiTheme.Masthead, "ONE MORE YEAR", new Vector2(560, 250), 112, new Color("c05a18"), 0.05f);
            Text(cv, UiTheme.Hand, "Live a life. Build a family. Leave a legacy.", new Vector2(566, 340), 50, new Color("47280f") with { A = 0.75f });
        }));
        Add("lockup_rings_stacked", new Vector2I(1000, 1000), false, c => Paper(c, 1970, cv =>
        {
            Emblems.Rings(cv, new Vector2(500, 360), 230, 1970);
            Centered(cv, UiTheme.Masthead, "ONE MORE YEAR", 700, 96, new Color("c05a18"), 0.05f);
            Centered(cv, UiTheme.Hand, "Live a life. Build a family. Leave a legacy.", 790, 44, new Color("47280f") with { A = 0.75f });
        }));
        Add("lockup_candle", new Vector2I(1000, 1000), false, c => Paper(c, 1970, cv =>
        {
            Emblems.Candle(cv, new Vector2(500, 380), 240);
            Centered(cv, Font("Fraunces"), "One More Year", 760, 100, new Color("47280f"));
        }));
        Add("lockup_photo", new Vector2I(1000, 1000), false, c => Paper(c, 1955, cv =>
        {
            Emblems.Photo(cv, new Vector2(500, 380), 250);
            Centered(cv, Font("SpecialElite"), "ONE MORE YEAR", 760, 80, new Color("3a2a1a"), 0.08f);
        }));

        // --- Splash and loading ---------------------------------------------------------------
        Add("splash", new Vector2I(1600, 900), false, c => Paper(c, 1970, cv =>
        {
            Emblems.Rings(cv, new Vector2(800, 330), 190, 1970);
            Centered(cv, UiTheme.Masthead, "ONE MORE YEAR", 640, 92, new Color("c05a18"), 0.05f);
            Centered(cv, UiTheme.Hand, "Live a life. Build a family. Leave a legacy.", 720, 40, new Color("47280f") with { A = 0.7f });
        }));
        Add("loading_frames", new Vector2I(1600, 500), false, c => Paper(c, 1970, cv =>
        {
            // The startup screen grows the rings one year at a time; here are five moments of it.
            for (int i = 0; i < 5; i++)
                Emblems.Rings(cv, new Vector2(180 + i * 310, 230), 120, 1970, grown: (i + 1) / 5f);
            Centered(cv, UiTheme.Hand, "Opening the family album…", 440, 34, new Color("47280f") with { A = 0.6f });
        }));

        // --- Store art (Steam sizes) ------------------------------------------------------------
        Add("store_header_920x430", new Vector2I(920, 430), false, c => StoreArt(c, wide: true));
        Add("store_capsule_616x353", new Vector2I(616, 353), false, c => StoreArt(c, wide: true));
        Add("store_small_462x174", new Vector2I(462, 174), false, c => Paper(c, 1970, cv =>
        {
            Emblems.Rings(cv, new Vector2(88, 87), 62, 1970);
            Text(cv, UiTheme.Masthead, "ONE MORE YEAR", new Vector2(168, 104), 40, new Color("c05a18"), 0.04f);
        }));
        Add("store_library_600x900", new Vector2I(600, 900), false, c => StoreArt(c, wide: false));
        // --- The UI icon set (docs/brand/icons/*.svg) in each era's colours ---------------------
        string icons = System.IO.Path.GetFullPath(System.IO.Path.Combine(_dir, "..", "icons"));
        if (System.IO.Directory.Exists(icons))
        {
            var files = System.IO.Directory.GetFiles(icons, "*.svg").OrderBy(f => f).ToList();
            Add("icons_sheet", new Vector2I(1600, 120 + 4 * 190), false, c => IconSheet(c, files));
        }

        Add("avatar_512", new Vector2I(512, 512), true, c => Canvas(c, cv =>
        {
            cv.DrawCircle(new Vector2(256, 256), 250, new Color("f8e7c7"));
            Emblems.Rings(cv, new Vector2(256, 256), 200, 1970);
        }));

        Next();
    }

    // --- Pieces --------------------------------------------------------------------------------

    private void Eras(Control c)
    {
        var rows = new (int Year, string Font, string Label)[]
        {
            (1950, "SpecialElite", "1950s"), (1970, "Fraunces", "1970s"), (1985, "Rajdhani", "1980s"), (2015, "Inter", "2010s"),
        };
        Paper(c, 1970, cv =>
        {
            for (int i = 0; i < rows.Length; i++)
            {
                var (year, font, label) = rows[i];
                UiTheme.SetYear(year);
                var band = new Rect2(0, i * 225, cv.Size.X, 225);
                cv.DrawRect(band, UiTheme.Background);
                Emblems.Rings(cv, new Vector2(150, band.Position.Y + 112), 80, year);
                bool upper = font is "SpecialElite" or "Rajdhani";
                Text(cv, Font(font), upper ? "ONE MORE YEAR" : "One More Year", new Vector2(280, band.Position.Y + 135), 88, UiTheme.Accent, upper ? 0.06f : 0);
                Text(cv, UiTheme.Hand, label, new Vector2(1380, band.Position.Y + 135), 44, UiTheme.Muted);
            }
            UiTheme.SetYear(1970);
        });
    }

    private void StoreArt(Control c, bool wide)
    {
        var size = c.Size;
        Paper(c, 1970, cv =>
        {
            if (wide)
            {
                Emblems.Rings(cv, new Vector2(size.Y * 0.3f, size.Y * 0.36f), size.Y * 0.2f, 1970);
                Text(cv, UiTheme.Masthead, "ONE MORE", new Vector2(size.X * 0.05f, size.Y * 0.74f), size.Y * 0.16f, new Color("c05a18"), 0.04f);
                Text(cv, UiTheme.Masthead, "YEAR", new Vector2(size.X * 0.05f, size.Y * 0.9f), size.Y * 0.16f, new Color("c05a18"), 0.04f);
            }
            else
            {
                Emblems.Rings(cv, new Vector2(size.X / 2, size.Y * 0.14f), size.X * 0.14f, 1970);
                Centered(cv, UiTheme.Masthead, "ONE MORE YEAR", size.Y * 0.3f, size.X * 0.105f, new Color("c05a18"), 0.04f);
                Centered(cv, UiTheme.Hand, "Live a life. Build a family. Leave a legacy.", size.Y * 0.94f, size.X * 0.05f, new Color("47280f") with { A = 0.75f });
            }
        });
        // One family in four ages, as album prints: the happiest living person of each age group.
        var living = _family.World.People.Where(p => p.IsAlive && p.InFamily).ToList();
        var people = new[] { (65, 110), (35, 64), (14, 34), (0, 13) }
            .Select(r => living.Where(p => p.Age(_family.Year) >= r.Item1 && p.Age(_family.Year) <= r.Item2).OrderByDescending(p => p.Happiness).FirstOrDefault())
            .OfType<Simulation.Model.Person>().ToList();
        float photo = wide ? size.Y * 0.36f : size.X * 0.3f;
        for (int i = 0; i < people.Count; i++)
        {
            var pos = wide
                ? new Vector2(size.X * 0.5f + i * photo * 0.68f, size.Y * 0.16f + (i % 2) * photo * 0.4f)
                : new Vector2(size.X * 0.14f + (i % 2) * photo * 1.25f, size.Y * 0.38f + (i / 2) * photo * 1.25f);
            var holder = new Control { Position = pos, RotationDegrees = i % 2 == 0 ? -5 : 4 };
            holder.AddChild(Portrait.Create(_family.Portrait(people[i].Id), false, photo));
            c.AddChild(holder);
        }
    }

    /// <summary>Every icon, in four rows: the 1950s, 1970s, 1980s and 2010s ink and accent.</summary>
    private static void IconSheet(Control c, List<string> files)
    {
        var years = new[] { 1950, 1970, 1985, 2015 };
        c.AddChild(new ColorRect { Color = new Color("f8efe0"), Size = c.Size });
        for (int row = 0; row < years.Length; row++)
        {
            UiTheme.SetYear(years[row]);
            var band = new ColorRect { Color = UiTheme.Background, Position = new Vector2(0, 60 + row * 190), Size = new Vector2(c.Size.X, 190) };
            c.AddChild(band);
            var label = new Label { Text = $"{years[row] / 10 * 10}s", Position = new Vector2(20, 70 + row * 190) };
            label.AddThemeFontOverride("font", UiTheme.Hand);
            label.AddThemeFontSizeOverride("font_size", 28);
            label.AddThemeColorOverride("font_color", UiTheme.Muted);
            c.AddChild(label);
            for (int i = 0; i < files.Count; i++)
            {
                // Odd icons in the ink colour, even ones in the accent, so both can be judged.
                var color = i % 2 == 0 ? UiTheme.Text : UiTheme.Accent;
                string svg = System.IO.File.ReadAllText(files[i]).Replace("currentColor", "#" + color.ToHtml(false));
                var image = new Image();
                image.LoadSvgFromString(svg, 2.5f);
                int col = i % 12, line = i / 12;
                var tex = new TextureRect { Texture = ImageTexture.CreateFromImage(image), Position = new Vector2(150 + col * 118, 82 + row * 190 + line * 82) };
                c.AddChild(tex);
            }
        }
        var names = new Label { Text = string.Join("  ·  ", files.Select(System.IO.Path.GetFileNameWithoutExtension)), Position = new Vector2(20, 14), Size = new Vector2(c.Size.X - 40, 40), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        names.AddThemeFontSizeOverride("font_size", 14);
        names.AddThemeColorOverride("font_color", new Color("47280f"));
        c.AddChild(names);
        UiTheme.SetYear(1970);
    }

    private static void Wordmark(Control cv, Font font, float size, float spacing, string text, Color color) =>
        Centered(cv, font, text, cv.Size.Y * 0.62f, size, color, spacing);

    // --- Helpers -------------------------------------------------------------------------------

    private static Font Font(string name) => GD.Load<FontFile>($"res://fonts/{name}.ttf");

    private static Font Spaced(Font font, float size, float spacing) =>
        spacing == 0 ? font : new FontVariation { BaseFont = font, SpacingGlyph = (int)(size * spacing) };

    private static void Text(Control cv, Font font, string text, Vector2 baseline, float size, Color color, float spacing = 0) =>
        cv.DrawString(Spaced(font, size, spacing), baseline, text, HorizontalAlignment.Left, -1, (int)size, color);

    private static void Centered(Control cv, Font font, string text, float baseline, float size, Color color, float spacing = 0)
    {
        var f = Spaced(font, size, spacing);
        float width = f.GetStringSize(text, HorizontalAlignment.Left, -1, (int)size).X;
        cv.DrawString(f, new Vector2((cv.Size.X - width) / 2, baseline), text, HorizontalAlignment.Left, -1, (int)size, color);
    }

    /// <summary>A control that draws with the given function.</summary>
    private static void Canvas(Control parent, Action<Control> draw)
    {
        var cv = new DrawBox(draw);
        cv.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        parent.AddChild(cv);
    }

    /// <summary>The album paper of a given year, with something drawn on top.</summary>
    private static void Paper(Control parent, int year, Action<Control> draw)
    {
        UiTheme.SetYear(year);
        var paper = new AlbumPaper { Tint = UiTheme.Background };
        paper.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        parent.AddChild(paper);
        Canvas(parent, draw);
    }

    private void Add(string name, Vector2I size, bool transparent, Action<Control> build) => _jobs.Enqueue((name, size, transparent, build));

    private void Next()
    {
        _viewport?.QueueFree();
        if (_jobs.Count == 0) { GD.Print($"Brand sheet saved to {_dir}"); GetTree().Quit(); return; }
        var (name, size, transparent, build) = _jobs.Dequeue();
        _current = name;
        _viewport = new SubViewport { Size = size, TransparentBg = transparent, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        var root = new Control { Size = size };
        _viewport.AddChild(root);
        AddChild(_viewport);
        build(root);
        _frames = 0;
    }

    public override void _Process(double delta)
    {
        if (_viewport == null || ++_frames < 5) return;
        _viewport.GetTexture().GetImage().SavePng($"{_dir}/{_current}.png");
        Next();
    }

    private partial class DrawBox : Control
    {
        private readonly Action<Control> _draw;
        public DrawBox(Action<Control> draw) { _draw = draw; MouseFilter = MouseFilterEnum.Ignore; }
        public override void _Ready() => Resized += QueueRedraw;
        public override void _Draw() => _draw(this);
    }
}
