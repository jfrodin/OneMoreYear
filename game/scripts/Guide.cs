using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OneMoreYear.Simulation;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Game;

/// <summary>
/// Tips for a new player, one at a time, each when it becomes useful and only once. Shown at the
/// top of the tab it is about. Can be turned off on the card itself or in the settings.
/// </summary>
public static class Guide
{
    private sealed record Tip(string Id, string Tab, Func<GameSession, bool> When, string Text);

    public const string Time = "time";
    public const string Year = "year", People = "people", Work = "work", Money = "money", Tree = "tree", Chronicle = "chronicle";

    private static int Age(GameSession s) => s.Player.Age(s.Year);

    // In the order they should be learned. Within a tab, the first one not yet seen is shown.
    private static readonly Tip[] Tips =
    {
        new("year", Year, _ => true,
            "This is one year of your life. Read what happened, answer anything waiting below, then press Next Year on the left."),
        new("answered", Year, s => s.CurrentEvents().Any(e => e.ChosenText != null),
            "What you chose stays on the card, with what it did. Green helped you, red cost you something."),
        new("values", Year, s => s.Year > s.World.StartYear,
            "Health, happiness, smarts, looks and fitness are on the left. Hover over one to see what it does."),
        new("time", Year, s => s.Actions(null).Count > 0 && Age(s) >= 4,
            "You also have time for a few things of your own each year, on the Your Time tab and under People. What you do not use is gone when the year ends."),
        new("chapter", Year, s => Age(s) >= 8,
            "Chronicle is the family's story, for everyone in it. When your life ends, you go on as someone you leave behind."),

        new("time_tab", Time, _ => true,
            "Everything you can do with this year's time. Each thing takes some of it, and what is left over is gone when the year ends."),

        new("people", People, _ => true,
            "Your family and friends. Click someone to see who they are and how they feel about you. People remember what you do to them."),
        new("people_time", People, s => Age(s) >= 6,
            "Under a person you can spend time on them: talk, help, argue, fall in love. It costs some of the year's time."),

        new("school", Work, s => s.Player.Activity == Activity.School,
            "Your grades are here. How hard you try moves them, and good grades open better schools and jobs later."),
        new("work", Work, s => s.Player.Activity == Activity.Working,
            "Performance decides promotions, and whether you keep the job. Look for other jobs below."),

        new("money", Money, _ => true,
            "Every line shows where money came from or went this year. Under How you live you choose how much to spend and how much to save."),
        new("tree", Tree, _ => true,
            "Everyone in the family, over every generation. Click a name to see their part of the tree."),
        new("chronicle", Chronicle, _ => true,
            "The family's story, newest first. Choose how much to show at the top."),
    };

    /// <summary>The tip to show at the top of a tab right now, or null.</summary>
    public static Control? CardFor(string tab, GameSession session)
    {
        if (!Settings.TipsOn) return null;
        var tip = Tips.FirstOrDefault(t => t.Tab == tab && !Settings.TipSeen(t.Id) && t.When(session));
        if (tip == null) return null;
        // On the year tab, one thing at a time: a tip waits until the ones before it have been seen.
        if (tab == Year && Tips.TakeWhile(t => t != tip).Any(t => t.Tab == Year && !Settings.TipSeen(t.Id))) return null;

        var box = Ui.VBox(8);
        var row = Ui.HBox(12);
        var label = UiTheme.HandLabel("Tip", 22, UiTheme.Accent);
        label.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        row.AddChild(label);
        var text = Ui.Label(tip.Text, 17, UiTheme.Text, wrap: true);
        text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(text);
        box.AddChild(row);
        var buttons = Ui.HBox(8);
        buttons.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        PanelContainer card = null!;
        var off = Ui.Button("No more tips", () => { Settings.SetTips(false); card.QueueFree(); }, 38);
        buttons.AddChild(off);
        var ok = Ui.Button("Got it", () => { Settings.MarkTip(tip.Id); card.QueueFree(); }, 38);
        ok.CustomMinimumSize = new Vector2(110, 38);
        buttons.AddChild(ok);
        box.AddChild(buttons);
        card = Ui.Card(box, UiTheme.Panel, UiTheme.Accent);
        return card;
    }
}
