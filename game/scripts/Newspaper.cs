using System.Linq;
using Godot;
using OneMoreYear.Simulation;

namespace OneMoreYear.Game;

/// <summary>
/// The family's own newspaper, shown when a new year begins: the biggest family news as the
/// headline, the rest in brief, and what happened in the world in the side column.
/// Newsprint and a masthead typeface, whatever the decade.
/// </summary>
public static class Newspaper
{
    private static readonly Color Newsprint = new("f1ede3");
    private static readonly Color Ink = new("1c1b19");
    private static readonly Color Grey = new("5d5a52");

    public static Control Build(GameSession s, YearReport report, System.Action onClose)
    {
        // One story per person, told in order; the biggest is the headline.
        var stories = s.Stories(report.News);
        var world = report.News.Where(l => l.Category == "world").ToList();
        var lead = stories.FirstOrDefault();

        var page = Ui.VBox(8);
        page.CustomMinimumSize = new Vector2(1120, 0);

        var mast = Label($"THE {s.World.FamilyName.ToUpperInvariant()} CHRONICLE", UiTheme.Masthead, 56, Ink);
        mast.HorizontalAlignment = HorizontalAlignment.Center;
        page.AddChild(mast);
        var date = Label($"{report.Year}   ·   No. {report.Year - s.World.StartYear + 1}   ·   {s.Player.FirstName} is {report.PlayerAge}   ·   Seed {s.SeedCode}",
            UiTheme.Body, 15, Grey);
        date.HorizontalAlignment = HorizontalAlignment.Center;
        page.AddChild(date);
        page.AddChild(Rule(3));

        var columns = Ui.HBox(28);
        var main = Ui.VBox(10);
        main.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        main.SizeFlagsStretchRatio = 2;

        var headline = Ui.HBox(18);
        var photo = Ui.VBox(4);
        var picture = Portrait.Create(s.Portrait(lead is { PersonId: > 0 } && s.World.TryGet(lead.PersonId) is { } who ? who.Id : s.Player.Id) with { Alive = false }, false, 130);
        picture.Modulate = Colors.White;
        photo.AddChild(picture);
        headline.AddChild(photo);
        var story = Ui.VBox(6);
        story.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        story.AddChild(Label(lead?.Headline ?? "A quiet year", UiTheme.Masthead, 34, Ink, wrap: true));
        story.AddChild(Label(lead == null ? "Nothing much happened in the family. Some years are like that, and some people would give anything for one."
            : lead.Body.Length > 0 ? lead.Body : "The family's biggest news this year.", UiTheme.Body, 16, lead?.Body.Length > 0 ? Ink : Grey, wrap: true));
        headline.AddChild(story);
        main.AddChild(headline);
        main.AddChild(Rule(1));
        if (stories.Count > 1)
        {
            main.AddChild(Label("ALSO IN THE FAMILY", UiTheme.Masthead, 18, Ink));
            foreach (var item in stories.Skip(1).Take(7))
                main.AddChild(Label("·  " + item.Text, UiTheme.Body, 16, Ink, wrap: true));
        }
        columns.AddChild(main);

        var side = Ui.VBox(8);
        side.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        side.AddChild(Label("THE WORLD", UiTheme.Masthead, 20, Ink));
        side.AddChild(Rule(1));
        if (world.Count == 0) side.AddChild(Label("A quiet year out there.", UiTheme.Body, 15, Grey, wrap: true));
        foreach (var line in world.Take(5)) side.AddChild(Label(line.Text, UiTheme.Body, 15, Ink, wrap: true));
        columns.AddChild(side);
        page.AddChild(columns);
        page.AddChild(Rule(1));

        var button = Ui.Button("Turn the page", onClose, 48);

        button.CustomMinimumSize = new Vector2(220, 48);
        var style = UiTheme.Box(Ink, 2, null, 0, 12);
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", UiTheme.Box(Ink.Lightened(0.2f), 2, null, 0, 12));
        button.AddThemeStyleboxOverride("pressed", style);
        button.AddThemeColorOverride("font_color", Newsprint);
        button.AddThemeColorOverride("font_hover_color", Newsprint);
        button.AddThemeColorOverride("font_focus_color", Newsprint);
        button.AddThemeFontOverride("font", UiTheme.Masthead);
        // Esc / B closes the paper as well.
        button.GuiInput += e => { if (e.IsActionPressed("ui_cancel")) { button.AcceptEvent(); onClose(); } };

        // How often the paper comes – so it never becomes a chore.
        var footer = Ui.HBox(12);
        footer.AddChild(Label("The paper comes", UiTheme.Body, 15, Grey));
        var mode = new OptionButton { CustomMinimumSize = new Vector2(200, 40) };
        mode.AddItem("every year");
        mode.AddItem("only in big years");
        mode.AddItem("never");
        mode.Selected = (int)Settings.Newspaper;
        mode.ItemSelected += i => Settings.SetNewspaper((NewspaperMode)(int)i);
        mode.AddThemeColorOverride("font_color", Ink);
        mode.AddThemeStyleboxOverride("normal", UiTheme.Box(Newsprint.Darkened(0.05f), 2, Grey, 1, 8));
        footer.AddChild(mode);
        footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        footer.AddChild(button);
        page.AddChild(footer);

        var sheet = new PanelContainer();
        var box = UiTheme.Box(Newsprint, 1, new Color("cfc8b8"), 1, 34);
        box.ShadowColor = new Color(0, 0, 0, 0.35f);
        box.ShadowSize = 18;
        box.ShadowOffset = new Vector2(4, 8);
        sheet.AddThemeStyleboxOverride("panel", box);
        sheet.AddChild(page);
        sheet.Rotation = Mathf.DegToRad(-0.6f);
        Ui.FocusLater(button);
        return sheet;
    }

    private static Label Label(string text, Font font, int size, Color color, bool wrap = false)
    {
        var l = Ui.Label(text, size, color, wrap);
        l.AddThemeFontOverride("font", font);
        return l;
    }

    private static Control Rule(int thickness) => new ColorRect { Color = Ink, CustomMinimumSize = new Vector2(0, thickness) };
}
