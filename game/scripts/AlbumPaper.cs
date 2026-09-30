using System;
using Godot;

namespace OneMoreYear.Game;

/// <summary>The album page behind everything: the era's paper colour with faint fibres.</summary>
public partial class AlbumPaper : Control
{
    public Color? Tint { get; set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        var paper = Tint ?? UiTheme.Background;
        DrawRect(new Rect2(Vector2.Zero, Size), paper);
        var fibre = paper.Darkened(0.35f) with { A = 0.07f };
        var rnd = new Random(7);
        int count = (int)(Size.X * Size.Y / 1600);
        for (int i = 0; i < count; i++)
        {
            var p = new Vector2((float)rnd.NextDouble() * Size.X, (float)rnd.NextDouble() * Size.Y);
            DrawLine(p, p + new Vector2((float)rnd.NextDouble() * 14 - 7, (float)rnd.NextDouble() * 3), fibre, 1);
        }
        // A soft darkening towards the edges, like an old page.
        var edge = paper.Darkened(0.5f);
        for (int i = 0; i < 24; i++)
        {
            var c = edge with { A = 0.006f * (24 - i) };
            DrawRect(new Rect2(i, i, Size.X - 2 * i, Size.Y - 2 * i), c, false, 1);
        }
    }
}
