using System.Linq;
using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// The credits: who made the game, who helped, and the typefaces and engine it is built with, with
/// their licences one click away (the licences ask to be shown).
/// </summary>
public partial class CreditsScreen : Control
{
    private Main _main = null!;

    public void Init(Main main) => _main = main;

    private static readonly (string Name, string Who, string Licence, string File)[] Typefaces =
    {
        ("Lora", "The Lora Project Authors", "SIL Open Font License 1.1", "OFL-lora.txt"),
        ("Libre Caslon Text", "The Libre Caslon Text Project Authors", "SIL Open Font License 1.1", "OFL-librecaslontext.txt"),
        ("Playfair Display", "The Playfair Display Project Authors", "SIL Open Font License 1.1", "OFL-playfairdisplay.txt"),
        ("Cedarville Cursive", "Kimberly Geswein", "SIL Open Font License 1.1", "OFL-cedarvillecursive.txt"),
    };

    public override void _Ready()
    {
        // A single sheet of the album: the credits centred like the end of a film, the buttons always
        // at the foot of the page.
        var book = BookView.OpenOn(this, 1f, single: true);
        var page = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        book.Left.AddChild(page);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
        page.AddChild(scroll);
        var col = Ui.VBox(4);
        col.CustomMinimumSize = new Vector2(640, 0);
        var center = new CenterContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        center.AddChild(col);
        var margin = Ui.Margin(center, 40);
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(margin);

        Label Centred(string text, Font font, int size, Color colour, int spacing = 0)
        {
            var l = Ui.Label(text, size, colour, wrap: true);
            l.HorizontalAlignment = HorizontalAlignment.Center;
            l.AddThemeFontOverride("font", font);
            if (spacing != 0)
            {
                var spaced = new FontVariation { BaseFont = font, SpacingGlyph = spacing };
                l.AddThemeFontOverride("font", spaced);
            }
            col.AddChild(l);
            return l;
        }
        // A section: a small spaced label in capitals, a thin rule, and the names under it.
        void Section(string title)
        {
            col.AddChild(Ui.Spacer(30));
            Centred(title.ToUpperInvariant(), UiTheme.Body, 13, UiTheme.Muted, spacing: 3);
            var rule = new ColorRect { Color = UiTheme.Muted with { A = 0.35f }, CustomMinimumSize = new Vector2(40, 1), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
            col.AddChild(Ui.Spacer(4));
            col.AddChild(rule);
            col.AddChild(Ui.Spacer(8));
        }
        void Name(string text) => Centred(text, UiTheme.Heading, 24, UiTheme.Text);
        void Small(string text) => Centred(text, UiTheme.Body, 15, UiTheme.Muted);

        Centred("One More Year", UiTheme.Masthead, 60, UiTheme.Accent);
        Small($"Version {Main.Version}");
        if (Studio.Developer != "") { col.AddChild(Ui.Spacer(6)); Name(Studio.Developer); }

        if (Studio.MadeBy != "")
        {
            Section("Made by");
            Name(Studio.MadeBy);
        }

        Section("Music");
        if (Studio.Composer != "") Name(Studio.Composer);
        Small("Written for the game, one piece for every decade.");

        Section("Thank you");
        if (Studio.Thanks.Length > 0) Name(string.Join(", ", Studio.Thanks));
        Small("To everyone who played the early versions and wrote down what they found.");

        Section("Typefaces");
        foreach (var t in Typefaces) Small($"{t.Name} by {t.Who}");

        Section("Built with");
        Small("Godot Engine, by Juan Linietsky, Ariel Manzur and the Godot community");
        Small(".NET, by the .NET Foundation and contributors");
        col.AddChild(Ui.Spacer(30));

        // The buttons, outside the scrolling part so they are always there.
        var buttons = Ui.HBox(12);
        buttons.Alignment = BoxContainer.AlignmentMode.Center;
        var back = Ui.Button("Back", () => _main.ShowTitle(), 50);
        back.CustomMinimumSize = new Vector2(160, 50);
        UiTheme.MakePrimary(back);
        buttons.AddChild(back);
        buttons.AddChild(AlbumBits.Link("The licences in full", ShowLicences));
        var foot = Ui.Margin(buttons, 16);
        page.AddChild(foot);
        Ui.FocusLater(back);
    }

    private void ShowLicences() => ShowLicencesBox(LicenceText());

    public override void _UnhandledInput(InputEvent e)
    {
        if (_main.HasModal || !e.IsActionPressed("ui_cancel")) return;
        GetViewport().SetInputAsHandled();
        _main.ShowTitle();
    }

    /// <summary>The full texts: the engine's licence and its parts, and each typeface's.</summary>
    public static System.Text.StringBuilder LicenceText()
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine("GODOT ENGINE").AppendLine().AppendLine(Engine.GetLicenseText()).AppendLine();
        // Each part of the engine: who holds the copyright and under which licence, then the licences in full.
        text.AppendLine("THE PARTS OF THE ENGINE").AppendLine();
        foreach (var component in Engine.GetCopyrightInfo())
        {
            text.AppendLine(component["name"].ToString());
            foreach (var p in component["parts"].AsGodotArray())
            {
                var part = (Godot.Collections.Dictionary)p;
                foreach (var line in part["copyright"].AsStringArray()) text.AppendLine($"  © {line}");
                text.AppendLine($"  License: {part["license"]}");
            }
            text.AppendLine();
        }
        var licences = Engine.GetLicenseInfo();
        foreach (var name in licences.Keys)
            text.AppendLine().AppendLine($"LICENSE: {name}").AppendLine().AppendLine(licences[name].ToString());
        string dotnet = "res://fonts/LICENSE-dotnet.txt";
        if (FileAccess.FileExists(dotnet)) text.AppendLine().AppendLine(".NET").AppendLine().AppendLine(FileAccess.GetFileAsString(dotnet));
        foreach (var t in Typefaces)
        {
            string path = $"res://fonts/{t.File}";
            if (!FileAccess.FileExists(path)) continue;
            text.AppendLine().AppendLine(t.Name.ToUpperInvariant()).AppendLine().AppendLine(FileAccess.GetFileAsString(path));
        }
        return text;
    }

    private void ShowLicencesBox(System.Text.StringBuilder text)
    {

        var box = Ui.VBox(10);
        box.CustomMinimumSize = new Vector2(860, 0);
        box.AddChild(UiTheme.HeadingLabel("Licences", 28, UiTheme.Accent));
        var body = new TextEdit { Text = text.ToString(), Editable = false, WrapMode = TextEdit.LineWrappingMode.Boundary, CustomMinimumSize = new Vector2(0, Mathf.Min(560, GetViewportRect().Size.Y - 260)) };
        body.AddThemeFontSizeOverride("font_size", 14);
        box.AddChild(body);
        System.Action close = () => { };
        var done = Ui.Button("Close", () => close(), 46);
        done.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        box.AddChild(done);
        close = _main.ShowDialog(box, done);
    }
}
