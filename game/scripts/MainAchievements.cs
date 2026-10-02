using System.Collections.Generic;
using System.Linq;
using Godot;
using OneMoreYear.Simulation;
using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Systems;

namespace OneMoreYear.Game;

/// <summary>Achievements: checking after each year, a quiet notice when one is earned, and the list.</summary>
public partial class Main
{
    private Control? _toastLayer;
    private readonly Queue<AchievementDef> _toasts = new();
    private bool _toastShowing;

    /// <summary>Checks the current family for new achievements; shows a notice for each.</summary>
    public void CheckAchievements()
    {
        if (Session == null) return;
        foreach (var a in Session.NewAchievements(AchievementStore.Ids))
        {
            AchievementStore.Add(a.Id, Session.World.FamilyName, Session.Year);
            _toasts.Enqueue(a);
        }
        if (!_toastShowing) ShowNextToast();
    }

    private static Color TierColor(string tier) => tier switch
    {
        AchievementSystem.Legendary => new Color("c8962e"),
        AchievementSystem.Rare => UiTheme.Info,
        AchievementSystem.Secret => UiTheme.Bad,
        _ => UiTheme.Good,
    };

    private static string TierName(string tier) => tier switch
    {
        AchievementSystem.Legendary => "Legendary",
        AchievementSystem.Rare => "Rare",
        AchievementSystem.Secret => "Secret",
        _ => "Achievement",
    };

    /// <summary>A card in the top right corner that fades away; it never takes focus or blocks the game.</summary>
    private void ShowNextToast()
    {
        if (_toasts.Count == 0) { _toastShowing = false; return; }
        _toastShowing = true;
        var a = _toasts.Dequeue();
        if (_toastLayer == null)
        {
            _toastLayer = new Control { MouseFilter = MouseFilterEnum.Ignore, ZIndex = 20 };
            _toastLayer.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_toastLayer);
        }

        var box = Ui.VBox(4);
        box.CustomMinimumSize = new Vector2(400, 0);
        box.AddChild(Ui.Label(TierName(a.Tier).ToUpperInvariant(), 13, TierColor(a.Tier)));
        var name = UiTheme.HeadingLabel(a.Name, 22, UiTheme.Text);
        box.AddChild(name);
        var text = Ui.Label(a.Text, 15, UiTheme.Muted, wrap: true);
        text.CustomMinimumSize = new Vector2(400, 0);
        box.AddChild(text);
        var card = Ui.Card(box, UiTheme.Panel, TierColor(a.Tier));
        card.MouseFilter = MouseFilterEnum.Ignore;
        _toastLayer.AddChild(card);
        card.SetAnchorsPreset(LayoutPreset.TopRight);
        card.ResetSize();
        card.Position = new Vector2(GetViewportRect().Size.X - card.Size.X - 24, 24);
        card.Modulate = new Color(1, 1, 1, 0);
        Sound.Play("year");

        bool instant = _shotDir != null;
        var tween = CreateTween();
        tween.TweenProperty(card, "modulate:a", 1f, instant ? 0 : 0.4);
        tween.TweenInterval(instant ? 0.5 : 6);
        tween.TweenProperty(card, "modulate:a", 0f, instant ? 0 : 0.8);
        tween.TweenCallback(Callable.From(() =>
        {
            card.QueueFree();
            ShowNextToast();
        }));
    }

    /// <summary>
    /// The list: common ones in full, rare and legendary ones as a hint until they are earned,
    /// secret ones only once found (and how many are still hidden).
    /// </summary>
    public void ShowAchievements()
    {
        var all = GameSession.AllAchievements();
        var earned = AchievementStore.All;
        var box = Ui.VBox(12);
        box.CustomMinimumSize = new Vector2(820, 0);
        box.AddChild(UiTheme.HeadingLabel("Achievements", 30, UiTheme.Accent));
        box.AddChild(Ui.Label($"{earned.Count} of {all.Count} earned, across all your families.", 16, UiTheme.Muted));

        var list = Ui.VBox(14);
        foreach (var tier in new[] { AchievementSystem.Common, AchievementSystem.Rare, AchievementSystem.Legendary, AchievementSystem.Secret })
        {
            var inTier = all.Where(a => a.Tier == tier).ToList();
            var shown = tier == AchievementSystem.Secret ? inTier.Where(a => earned.ContainsKey(a.Id)).ToList() : inTier;
            list.AddChild(Ui.Label(tier switch
            {
                AchievementSystem.Common => "Along the way",
                AchievementSystem.Rare => "Rare",
                AchievementSystem.Legendary => "Legendary",
                _ => "Secret",
            }, 20, TierColor(tier)));
            foreach (var a in shown)
            {
                bool have = earned.TryGetValue(a.Id, out var when);
                var row = Ui.VBox(2);
                if (have)
                {
                    row.AddChild(Ui.Label(a.Name, 18, UiTheme.Text));
                    row.AddChild(Ui.Label(a.Text, 14, UiTheme.Muted, wrap: true));
                    row.AddChild(Ui.Label($"The {when!.Family} family, {when.Year}", 13, UiTheme.Faint));
                }
                else if (tier == AchievementSystem.Common)
                {
                    row.AddChild(Ui.Label(a.Name, 18, UiTheme.Faint));
                    row.AddChild(Ui.Label(a.Text, 14, UiTheme.Faint, wrap: true));
                }
                else
                {
                    row.AddChild(Ui.Label("Not yet", 18, UiTheme.Faint));
                    var hint = UiTheme.HandLabel(a.Hint, 20, UiTheme.Faint, wrap: true);
                    row.AddChild(hint);
                }
                list.AddChild(row);
            }
            if (tier == AchievementSystem.Secret)
            {
                int hidden = inTier.Count - shown.Count;
                if (hidden > 0)
                    list.AddChild(Ui.Label(hidden == inTier.Count
                        ? $"{hidden} secret ones. You will know them when they happen."
                        : $"{hidden} more secret ones.", 15, UiTheme.Faint, wrap: true));
            }
        }
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(820, 560), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(list);
        box.AddChild(scroll);

        System.Action close = () => { };
        var ok = Ui.Button("Close", () => close(), 48);
        box.AddChild(ok);
        close = ShowDialog(box, ok);
    }
}
