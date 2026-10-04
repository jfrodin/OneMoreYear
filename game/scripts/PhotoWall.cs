using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OneMoreYear.Simulation;
using OneMoreYear.Simulation.Model;
using OneMoreYear.Simulation.Systems;

namespace OneMoreYear.Game;

/// <summary>
/// The title screen's backdrop: a wall of family photographs, prints from different decades, each
/// a little crooked, tinted like a photo of its year. Every few seconds one is taken down and another
/// put up: the same family, at another age, in another time.
/// </summary>
public partial class PhotoWall : Control
{
    private const int Count = 9;
    private readonly Random _rng = new();
    private readonly List<(Person Person, PortraitView Face)> _family = new();
    private readonly Control?[] _frames = new Control?[Count];
    private double _timer = 2.5;
    private int _next;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        // A family of its own every time the game starts: no simulation, just who they are.
        var session = GameSession.NewGame(new NewGameOptions { Seed = (ulong)_rng.NextInt64(1, long.MaxValue), StartYear = 1950 + _rng.Next(4) * 10 });
        foreach (var p in session.World.People.Where(p => p.InFamily || p.Id == session.Player.Id).Take(12))
            _family.Add((p, session.Portrait(p.Id)));
        Resized += Place;
        for (int i = 0; i < Count; i++) _frames[i] = NewPhoto(i, fadeIn: false);
        Place();
    }

    public override void _Process(double delta)
    {
        _timer -= delta;
        if (_timer > 0 || _family.Count == 0) return;
        _timer = 3.2;
        int slot = _next++ % Count;
        if (_frames[slot] is { } old)
        {
            var fade = CreateTween();
            fade.TweenProperty(old, "modulate:a", 0f, 1.2);
            fade.TweenCallback(Callable.From(old.QueueFree));
        }
        _frames[slot] = NewPhoto(slot, fadeIn: true);
        Place();
    }

    /// <summary>Someone from the family at an age they have lived, or will live.</summary>
    private Control NewPhoto(int slot, bool fadeIn)
    {
        var (person, face) = _family[_rng.Next(_family.Count)];
        int age = _rng.Next(0, 85);
        int year = person.BirthYear + age;
        var view = face with
        {
            Age = age, Alive = true, Year = year, Mood = 0.5,
            Grey = Faces.GreyAt(face.Face, age), Bald = Faces.BaldAt(face.Face, person.Sex, age),
            Glasses = age >= face.Face.GlassesFromAge,
            Outfit = age < 2 ? "baby" : age >= 66 ? "cardigan" : new[] { "casual", "smart", "work", "suit" }[_rng.Next(4)],
        };

        // A print: white border, a soft shadow, a name and a year in pencil.
        var print = new StyleBoxFlat { BgColor = new Color("f7f2e6") };
        print.SetContentMarginAll(10);
        print.ContentMarginBottom = 6;
        print.ShadowColor = new Color(0.15f, 0.1f, 0.05f, 0.28f);
        print.ShadowSize = 10;
        print.ShadowOffset = new Vector2(3, 5);
        var frame = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        frame.AddThemeStyleboxOverride("panel", print);
        var box = Ui.VBox(2);
        var portrait = Portrait.Create(view, false, 150);
        portrait.Modulate = UiTheme.PhotoTintFor(year);
        box.AddChild(portrait);
        var caption = UiTheme.HandLabel($"{person.FirstName}, {year}", 18, new Color("5a4a3a"));
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(caption);
        frame.AddChild(box);
        Ui.PassMouse(frame);
        frame.RotationDegrees = (float)(_rng.NextDouble() * 12 - 6);
        frame.SetMeta("slot", slot);
        frame.SetMeta("jitter", new Vector2((float)_rng.NextDouble() * 50 - 25, (float)_rng.NextDouble() * 40 - 20));
        if (fadeIn)
        {
            frame.Modulate = new Color(1, 1, 1, 0);
            CreateTween().TweenProperty(frame, "modulate:a", 1f, 1.6).SetDelay(0.8);
        }
        AddChild(frame);
        return frame;
    }

    /// <summary>Hangs the prints in a loose grid that fits the wall.</summary>
    private void Place()
    {
        if (Size.X < 10) return;
        int cols = Size.X > 900 ? 4 : 3;
        int rows = (Count + cols - 1) / cols;
        float cellW = Size.X / cols, cellH = Size.Y / rows;
        foreach (var frame in GetChildren().OfType<Control>())
        {
            if (!frame.HasMeta("slot")) continue;
            int slot = (int)frame.GetMeta("slot");
            var jitter = (Vector2)frame.GetMeta("jitter");
            // Every other row shifted, like photos hung by hand.
            float x = (slot % cols + (slot / cols % 2) * 0.35f) * cellW + jitter.X;
            float y = slot / cols * cellH + jitter.Y + 20;
            frame.PivotOffset = frame.Size / 2;
            frame.Position = new Vector2(x, y);
        }
    }
}
