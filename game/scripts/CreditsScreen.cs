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
        ("Caveat", "The Caveat Project Authors", "SIL Open Font License 1.1", "OFL-caveat.txt"),
        ("Playfair Display", "The Playfair Display Project Authors", "SIL Open Font License 1.1", "OFL-playfairdisplay.txt"),
        ("Fraunces", "The Fraunces Project Authors", "SIL Open Font License 1.1", "OFL-fraunces.txt"),
        ("Inter", "The Inter Project Authors", "SIL Open Font License 1.1", "OFL-inter.txt"),
        ("Rajdhani", "Indian Type Foundry", "SIL Open Font License 1.1", "OFL-rajdhani.txt"),
        ("Special Elite", "Astigmatic", "Apache License 2.0", "LICENSE-SpecialElite.txt"),
    };

    public override void _Ready()
    {
        var book = BookView.OpenOn(this, 1f, single: true);
        var col = Ui.VBox(10);
        col.CustomMinimumSize = new Vector2(760, 0);
        var center = new CenterContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        center.AddChild(col);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever };
        var margin = Ui.Margin(center, 30);
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(margin);
        book.Left.AddChild(scroll);

        void Heading(string text) { col.AddChild(Ui.Spacer(16)); col.AddChild(UiTheme.HandLabel(text, 30, UiTheme.Accent)); }
        void Line(string text, int size = 18, Color? colour = null) => col.AddChild(Ui.Label(text, size, colour ?? UiTheme.Text, wrap: true));

        var title = Ui.Label("One More Year", 56, UiTheme.Accent);
        title.AddThemeFontOverride("font", UiTheme.Masthead);
        col.AddChild(title);
        Line($"Version {Main.Version}", 15, UiTheme.Faint);
        if (Studio.Developer != "") Line(Studio.Developer, 24);

        if (Studio.MadeBy != "")
        {
            Heading("Made by");
            Line(Studio.MadeBy, 22);
        }

        Heading("Music");
        Line("Written for the game, one piece for every decade.", 17, UiTheme.Muted);

        Heading("Thank you");
        if (Studio.Thanks.Length > 0) Line(string.Join(", ", Studio.Thanks));
        Line("To everyone who played the early versions and wrote down what they found.", 17, UiTheme.Muted);

        Heading("Typefaces");
        foreach (var t in Typefaces)
            Line($"{t.Name}, by {t.Who}. {t.Licence}.", 16, UiTheme.Muted);

        Heading("Built with");
        Line("Godot Engine, by Juan Linietsky, Ariel Manzur and the Godot community. MIT License.", 16, UiTheme.Muted);
        Line(".NET, by the .NET Foundation and contributors. MIT License.", 16, UiTheme.Muted);

        col.AddChild(Ui.Spacer(20));
        var buttons = Ui.HBox(10);
        var back = Ui.Button("Back", () => _main.ShowTitle(), 50);
        back.CustomMinimumSize = new Vector2(160, 50);
        UiTheme.MakePrimary(back);
        buttons.AddChild(back);
        buttons.AddChild(Ui.Button("The licences in full…", ShowLicences, 50));
        col.AddChild(buttons);
        col.AddChild(Ui.Spacer(30));
        Ui.FocusLater(back);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_main.HasModal || !e.IsActionPressed("ui_cancel")) return;
        GetViewport().SetInputAsHandled();
        _main.ShowTitle();
    }

    /// <summary>The full texts: the engine's licence and its parts, and each typeface's.</summary>
    private void ShowLicences()
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine("GODOT ENGINE").AppendLine().AppendLine(Engine.GetLicenseText()).AppendLine();
        foreach (var part in Engine.GetCopyrightInfo())
        {
            var info = (Godot.Collections.Dictionary)part;
            text.AppendLine(info["name"].ToString());
        }
        foreach (var t in Typefaces)
        {
            string path = $"res://fonts/{t.File}";
            if (!FileAccess.FileExists(path)) continue;
            text.AppendLine().AppendLine(t.Name.ToUpperInvariant()).AppendLine().AppendLine(FileAccess.GetFileAsString(path));
        }

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
