using System;
using System.Collections.Generic;
using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// The game's emblems, drawn in code so they scale to any size and can be animated:
/// <list type="bullet">
/// <item><b>Rings</b> – a cross-section of a tree. Every ring is a year; the newest is the accent colour.
/// One more year, one more ring, and the tree is the family.</item>
/// <item><b>Candle</b> – a single birthday candle.</item>
/// <item><b>Photo</b> – an album print of a tree, held by photo corners.</item>
/// </list>
/// </summary>
public static class Emblems
{
    private static readonly Color Bark = new("4a2c17");
    private static readonly Color Wood = new("e9c99a");
    private static readonly Color WoodDark = new("d9b07a");
    private static readonly Color Line = new("8a5a32");

    /// <summary>Tree rings. <paramref name="grown"/> (0–1) shows only the inner part, for the startup animation.</summary>
    public static void Rings(Control c, Vector2 centre, float radius, int year, float grown = 1)
    {
        UiTheme.SetYear(year);
        var accent = UiTheme.Accent;
        var rnd = new Random(11);
        // Ring widths vary like real years: some good, some poor.
        var widths = new List<float>();
        for (int i = 0; i < 11; i++) widths.Add(0.7f + (float)rnd.NextDouble() * 0.6f);
        float total = 0;
        foreach (var w in widths) total += w;
        float p0 = (float)rnd.NextDouble() * 6, p1 = (float)rnd.NextDouble() * 6, p2 = (float)rnd.NextDouble() * 6;
        float scale = radius / 120f;

        Vector2[] Ring(float r)
        {
            var pts = new Vector2[120];
            for (int k = 0; k < pts.Length; k++)
            {
                float a = k * Mathf.Tau / pts.Length;
                float wobble = 1 + 0.035f * Mathf.Sin(3 * a + p0) + 0.02f * Mathf.Sin(5 * a + p1) + 0.012f * Mathf.Sin(11 * a + p2);
                pts[k] = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * wobble;
            }
            return pts;
        }

        // While growing, only the first years exist: the newest of them is the accent ring.
        var radii = new List<float>();
        float acc = 0;
        int count = Math.Max(1, (int)Math.Round(widths.Count * Mathf.Clamp(grown, 0, 1)));
        for (int i = 0; i < count; i++)
        {
            acc += widths[i];
            radii.Add(radius * 0.9f * acc / total);
        }
        float wood = radii[^1];
        // Bark, then the wood from the outside in.
        if (grown >= 1) c.DrawColoredPolygon(Ring(radius), Bark);
        c.DrawColoredPolygon(Ring(wood), Wood);
        for (int i = radii.Count - 1; i >= 0; i--)
        {
            var pts = Ring(radii[i]);
            if (i % 2 == 0) c.DrawColoredPolygon(pts, WoodDark with { A = 0.35f });
        }
        for (int i = 0; i < radii.Count; i++)
        {
            var pts = Ring(radii[i]);
            var closed = new Vector2[pts.Length + 1];
            Array.Copy(pts, closed, pts.Length);
            closed[^1] = pts[0];
            bool newest = i == radii.Count - 1;
            c.DrawPolyline(closed, newest ? accent : Line with { A = 0.55f }, (newest ? 4.5f : 1.6f) * scale, true);
        }
        // The heart of the tree, and a small crack from it.
        c.DrawCircle(centre, 3.5f * scale, Line);
        if (grown >= 1)
        {
            var crack = new[] { centre + new Vector2(4, -2) * scale, centre + new Vector2(22, -10) * scale, centre + new Vector2(34, -9) * scale, centre + new Vector2(52, -20) * scale };
            c.DrawPolyline(crack, Line with { A = 0.7f }, 2f * scale, true);
        }
    }

    /// <summary>Rings with a small sprout growing from the top: the family goes on.</summary>
    public static void RingsWithSprout(Control c, Vector2 centre, float radius)
    {
        Rings(c, centre + new Vector2(0, radius * 0.18f), radius * 0.82f, 1970);
        float s = radius / 120f;
        var top = centre + new Vector2(0, radius * 0.18f - radius * 0.82f);
        var green = new Color("5a7a22");
        c.DrawPolyline(new[] { top + new Vector2(0, 4) * s, top + new Vector2(2, -18) * s, top + new Vector2(0, -34) * s }, green, 4 * s, true);
        Leaf(c, top + new Vector2(0, -30) * s, -0.9f, 22 * s, green);
        Leaf(c, top + new Vector2(1, -22) * s, 0.7f, 18 * s, green.Lightened(0.15f));
    }

    private static void Leaf(Control c, Vector2 at, float angle, float length, Color color)
    {
        var dir = new Vector2(Mathf.Cos(angle - Mathf.Pi / 2), Mathf.Sin(angle - Mathf.Pi / 2));
        var side = new Vector2(-dir.Y, dir.X);
        var pts = new List<Vector2>();
        for (int i = 0; i <= 12; i++)
        {
            float t = i / 12f;
            pts.Add(at + dir * length * t + side * length * 0.35f * Mathf.Sin(t * Mathf.Pi));
        }
        for (int i = 11; i >= 1; i--)
        {
            float t = i / 12f;
            pts.Add(at + dir * length * t - side * length * 0.35f * Mathf.Sin(t * Mathf.Pi));
        }
        c.DrawColoredPolygon(pts.ToArray(), color);
    }

    /// <summary>A single birthday candle with a flame.</summary>
    public static void Candle(Control c, Vector2 centre, float radius)
    {
        float s = radius / 120f;
        var cream = new Color("f8e7c7");
        var stripe = new Color("c05a18");
        float w = 34 * s, h = 150 * s;
        var body = new Rect2(centre.X - w / 2, centre.Y - h * 0.25f, w, h);
        c.DrawRect(body, cream);
        // Spiral stripes.
        for (int i = 0; i < 6; i++)
        {
            float y = body.Position.Y + 8 * s + i * 25 * s;
            c.DrawColoredPolygon(new[]
            {
                new Vector2(body.Position.X, y + 10 * s), new Vector2(body.End.X, y), new Vector2(body.End.X, y + 9 * s), new Vector2(body.Position.X, y + 19 * s),
            }, stripe);
        }
        c.DrawRect(body, Line, false, 2 * s);
        // Wick and flame.
        var wickTop = new Vector2(centre.X, body.Position.Y - 16 * s);
        c.DrawLine(new Vector2(centre.X, body.Position.Y), wickTop, new Color("3a2a1a"), 3 * s);
        for (int g = 5; g >= 1; g--) c.DrawCircle(wickTop + new Vector2(0, -26 * s), 14 * s * g, new Color(1f, 0.75f, 0.3f, 0.04f));
        Flame(c, wickTop + new Vector2(0, 4 * s), 24 * s, 62 * s, new Color("f0a030"));
        Flame(c, wickTop + new Vector2(0, 2 * s), 12 * s, 34 * s, new Color("fff0c0"));
    }

    private static void Flame(Control c, Vector2 bottom, float width, float height, Color color)
    {
        var pts = new Vector2[48];
        for (int i = 0; i < pts.Length; i++)
        {
            float t = i * Mathf.Tau / pts.Length;
            // A teardrop: round at the bottom, pointed at the top.
            float x = width * Mathf.Sin(t) * Mathf.Abs(Mathf.Sin(t / 2));
            float y = -height * (1 + Mathf.Cos(t)) / 2;
            pts[i] = bottom + new Vector2(x, y);
        }
        c.DrawColoredPolygon(pts, color);
    }

    /// <summary>An album print of a tree, slightly askew, held by photo corners.</summary>
    public static void Photo(Control c, Vector2 centre, float radius)
    {
        float s = radius / 120f;
        c.DrawSetTransform(centre, Mathf.DegToRad(-5), Vector2.One);
        var frame = new Rect2(-100 * s, -110 * s, 200 * s, 220 * s);
        c.DrawRect(new Rect2(frame.Position + new Vector2(6, 8) * s, frame.Size), new Color(0, 0, 0, 0.18f));
        c.DrawRect(frame, new Color("fbf6ea"));
        var image = new Rect2(-86 * s, -96 * s, 172 * s, 160 * s);
        c.DrawRect(image, new Color("d8c08f"));
        c.DrawRect(new Rect2(image.Position.X, image.Position.Y + image.Size.Y * 0.72f, image.Size.X, image.Size.Y * 0.28f), new Color("b89a62"));
        // The tree: a trunk that splits, like a family.
        var ink = new Color("4a3320");
        var trunk = new Vector2(0, image.End.Y - 8 * s);
        void Branch(Vector2 from, float angle, float length, int depth)
        {
            var to = from + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * length;
            c.DrawLine(from, to, ink, Mathf.Max(1.5f, depth * 2.6f) * s, true);
            if (depth == 0)
            {
                c.DrawCircle(to, 13 * s, new Color("6f7a3a") with { A = 0.8f });
                c.DrawCircle(to + new Vector2(-6, -5) * s, 8 * s, new Color("85904a") with { A = 0.8f });
                return;
            }
            Branch(to, angle - 0.5f, length * 0.7f, depth - 1);
            Branch(to, angle + 0.45f, length * 0.72f, depth - 1);
        }
        Branch(trunk, 0, 46 * s, 4);
        // Photo corners.
        var corner = new Color("2e2018");
        foreach (var (p, dx, dy) in new[] { (frame.Position, 1, 1), (new Vector2(frame.End.X, frame.Position.Y), -1, 1), (new Vector2(frame.Position.X, frame.End.Y), 1, -1), (frame.End, -1, -1) })
            c.DrawColoredPolygon(new[] { p, p + new Vector2(dx * 30, 0) * s, p + new Vector2(0, dy * 30) * s }, corner);
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    /// <summary>The rounded paper tile behind an app icon.</summary>
    public static void IconTile(Control c, Rect2 area)
    {
        var tile = new StyleBoxFlat { BgColor = new Color("f3e2c0"), BorderColor = new Color("c9a878"), ShadowSize = 0 };
        tile.SetCornerRadiusAll((int)(area.Size.X * 0.2f));
        tile.SetBorderWidthAll(Math.Max(1, (int)(area.Size.X * 0.02f)));
        c.DrawStyleBox(tile, area.Grow(-area.Size.X * 0.03f));
    }
}
