using System;
using System.Collections.Generic;
using Godot;
using OneMoreYear.Simulation;

namespace OneMoreYear.Game;

/// <summary>
/// A generated portrait drawn from the person's genes (see Simulation.Systems.Faces): face shape,
/// skin, hair, eyes and features inherited from the parents, plus age (children's proportions,
/// grey hair, baldness, wrinkles), weight, mood, glasses and beard. The dead are drawn in grey.
/// </summary>
public partial class Portrait : Control
{
    private PortraitView _v = null!;
    private bool _highlight;

    public static Portrait Create(PortraitView view, bool highlight, float size)
    {
        var p = new Portrait { CustomMinimumSize = new Vector2(size, size), MouseFilter = MouseFilterEnum.Ignore };
        p._v = view;
        p._highlight = highlight;
        // Photos look like prints of their time (sepia in the fifties, faded in the seventies).
        if (view.Alive) p.Modulate = UiTheme.PhotoTint;
        return p;
    }

    // --- Colours --------------------------------------------------------------------------------

    public static Color SkinColor(double tone)
    {
        var light = new Color("f2d2bd");
        var mid = new Color("c68b62");
        var dark = new Color("5b3a27");
        float t = (float)tone;
        return t < 0.5f ? light.Lerp(mid, t * 2) : mid.Lerp(dark, (t - 0.5f) * 2);
    }

    public static Color HairColor(string name, double grey, int age)
    {
        var c = name switch
        {
            "black" => new Color("1e1a17"),
            "dark brown" => new Color("3a291e"),
            "brown" => new Color("5e4330"),
            "dark blond" => new Color("8a6b45"),
            "blond" => new Color("d3b273"),
            "red" => new Color("a3482a"),
            _ => new Color("5e4330"),
        };
        if (age < 4) c = c.Lightened(0.15f);
        float g = (float)grey;
        c = c.Lerp(new Color("9a9a9a"), Mathf.Clamp(g * 1.1f, 0, 1));
        if (g > 0.8f) c = c.Lerp(new Color("e6e6e6"), (g - 0.8f) / 0.2f);
        return c;
    }

    private static Color EyeColor(string name) => name switch
    {
        "brown" => new Color("5a3b22"),
        "blue" => new Color("4a7fb0"),
        "green" => new Color("5d8a4f"),
        "grey" => new Color("7d8a92"),
        "hazel" => new Color("8a6a3a"),
        _ => new Color("5a3b22"),
    };

    private static readonly Color[] Shirts =
    {
        new("3f5f8a"), new("8a4a3f"), new("4f7a52"), new("6b4f8a"), new("8a7a3f"),
        new("3f7a8a"), new("8a3f6b"), new("5b6b3f"), new("39456e"), new("7a5b3f"),
    };

    public static Color ShirtColor(int id) => Shirts[id % Shirts.Length];

    /// <summary>The dead are drawn in muted grey.</summary>
    private Color T(Color c)
    {
        if (_v.Alive) return c;
        float l = c.R * 0.3f + c.G * 0.59f + c.B * 0.11f;
        return new Color(l, l, l, c.A).Darkened(0.1f);
    }

    // --- Geometry helpers --------------------------------------------------------------------

    private float _s;
    private Vector2 _o;
    private Vector2[] _clip = Array.Empty<Vector2>();

    /// <summary>Unit coordinates (0–1 across the portrait) to pixels.</summary>
    private Vector2 P(float x, float y) => _o + new Vector2(x * _s, y * _s);

    private static Vector2[] Ellipse(Vector2 c, float rx, float ry, int n = 32)
    {
        var pts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.Tau * i / n;
            pts[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        }
        return pts;
    }

    private void Poly(Vector2[] pts, Color c)
    {
        if (pts.Length >= 3) DrawColoredPolygon(pts, T(c));
    }

    /// <summary>Draws a polygon clipped to the round frame (for shoulders and long hair).</summary>
    private void Clipped(Vector2[] pts, Color c)
    {
        foreach (var part in Geometry2D.IntersectPolygons(pts, _clip))
            Poly(part, c);
    }

    private void Line(Vector2 a, Vector2 b, Color c, float width) => DrawLine(a, b, T(c), Mathf.Max(1, width * _s), true);

    private void Curve(IList<Vector2> pts, Color c, float width) =>
        DrawPolyline(new List<Vector2>(pts).ToArray(), T(c), Mathf.Max(1, width * _s), true);

    // --- Drawing -----------------------------------------------------------------------------

    public override void _Draw()
    {
        if (_v == null) return;
        _s = Mathf.Min(Size.X, Size.Y);
        _o = (Size - new Vector2(_s, _s)) / 2;
        var f = _v.Face;
        int age = _v.Age;
        bool baby = age < 2, child = age < 13;
        float heavy = (float)_v.Heaviness;

        _clip = Ellipse(P(0.5f, 0.5f), _s / 2, _s / 2, 64);
        Poly(_clip, UiTheme.PhotoBackdrop);

        var skin = SkinColor(f.Skin);
        var skinShade = skin.Darkened(0.18f);
        var hair = HairColor(_v.HairColor, _v.Grey, age);

        // Proportions: children have bigger heads and eyes, heavier people wider faces.
        float cx = 0.5f;
        float cy = baby ? 0.5f : child ? 0.47f : 0.44f;
        float rx = (baby ? 0.25f : child ? 0.23f : 0.2f) * (0.9f + (float)f.Width * 0.18f) * (0.9f + heavy * 0.22f);
        float ry = baby ? 0.25f : child ? 0.26f : 0.26f;
        float jaw = baby ? 0.95f : child ? 0.82f : 0.6f + (float)f.Jaw * 0.3f + heavy * 0.1f;
        float chin = child ? 0 : (float)f.Chin;

        // Long hair behind the head.
        int style = f.HairStyle;
        bool longHair = !_v.Male && !baby && style is 0 or 1;
        if (longHair && _v.Bald < 0.5)
            Clipped(Ellipse(P(cx, cy + 0.14f), rx * _s * 1.2f, ry * _s * 1.35f), hair.Darkened(0.12f));

        // Shoulders and neck.
        if (!baby)
        {
            float sw = 0.34f + heavy * 0.1f + (float)(_v.Fitness - 50) / 1000f;
            Clipped(Ellipse(P(0.5f, 1.1f), sw * _s, 0.28f * _s, 48), ShirtColor(_v.Id));
            var neckTop = cy + ry * 0.55f;
            Poly(new[] { P(cx - rx * 0.42f, neckTop), P(cx + rx * 0.42f, neckTop), P(cx + rx * 0.46f, 0.86f), P(cx - rx * 0.46f, 0.86f) }, skinShade);
            Poly(new[] { P(cx - 0.075f, 0.84f), P(cx + 0.075f, 0.84f), P(cx, 0.92f) }, skinShade);
        }
        else
        {
            Clipped(Ellipse(P(0.5f, 1.08f), 0.3f * _s, 0.22f * _s, 48), new Color("e8e2d0"));
        }

        // Ears, head.
        float earR = 0.03f + (float)f.Ears * 0.018f;
        Poly(Ellipse(P(cx - rx * 0.97f, cy + ry * 0.05f), earR * _s, earR * 1.5f * _s, 16), skinShade);
        Poly(Ellipse(P(cx + rx * 0.97f, cy + ry * 0.05f), earR * _s, earR * 1.5f * _s, 16), skinShade);
        Poly(Head(cx, cy, rx, ry, jaw, chin, 1f), skin);

        // Freckles.
        if (f.Freckles > 0.35 && !baby)
        {
            var rnd = new Random(_v.Id);
            int n = (int)((f.Freckles - 0.3) * 30);
            for (int i = 0; i < n; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                var at = P(cx + side * rx * (0.25f + (float)rnd.NextDouble() * 0.35f), cy + ry * (0.12f + (float)rnd.NextDouble() * 0.2f));
                DrawCircle(at, Mathf.Max(0.8f, _s * 0.006f), T(skin.Darkened(0.35f) with { A = 0.7f }));
            }
        }

        // Wrinkles.
        if (age > 45)
        {
            float a = Mathf.Clamp((age - 45) / 35f, 0, 1) * 0.55f;
            var line = skin.Darkened(0.3f) with { A = a };
            for (int i = 0; i < 1 + (int)(a * 4); i++)
            {
                float y = cy - ry * (0.55f - i * 0.1f);
                Curve(new[] { P(cx - rx * 0.4f, y), P(cx, y - 0.008f), P(cx + rx * 0.4f, y) }, line, 0.006f);
            }
            if (age > 55)
            {
                Curve(new[] { P(cx - rx * 0.3f, cy + ry * 0.22f), P(cx - rx * 0.42f, cy + ry * 0.5f) }, line, 0.006f);
                Curve(new[] { P(cx + rx * 0.3f, cy + ry * 0.22f), P(cx + rx * 0.42f, cy + ry * 0.5f) }, line, 0.006f);
            }
        }

        // Beard (grown men).
        bool beardAge = _v.Male && age >= 18;
        if (beardAge && f.Beard is 1 or 2)
        {
            var beard = Head(cx, cy, rx, ry, jaw, chin, 1.02f, lowerFrom: f.Beard == 2 ? 0.1f : 0.2f);
            Poly(beard, hair with { A = f.Beard == 2 ? 0.95f : 0.28f });
        }

        // Eyes and brows.
        float ey = cy + ry * (child ? 0.05f : -0.02f);
        float spacing = rx * (0.36f + (float)f.EyeSpacing * 0.12f);
        float ew = (0.032f + (float)f.Eyes * 0.014f) * (child ? 1.25f : 1f);
        float eh = ew * 0.62f;
        float mood = (float)_v.Mood;
        foreach (float side in new[] { -1f, 1f })
        {
            var c = P(cx + side * spacing, ey);
            Poly(Ellipse(c, ew * _s, eh * _s, 20), new Color("f5f1ea"));
            DrawCircle(c, eh * 0.95f * _s, T(EyeColor(_v.EyeColor)));
            DrawCircle(c, eh * 0.45f * _s, T(new Color("141414")));
            DrawCircle(c + new Vector2(-eh * 0.3f, -eh * 0.35f) * _s, Mathf.Max(0.8f, eh * 0.22f * _s), T(new Color(1, 1, 1, 0.85f)));
            if (age > 60) Curve(new[] { c + new Vector2(-ew, -eh * 0.6f) * _s, c + new Vector2(0, -eh * 1.05f) * _s, c + new Vector2(ew, -eh * 0.6f) * _s }, skin.Darkened(0.25f), 0.006f);

            // Brows: sad people raise the inner end.
            float inner = -side * ew * 0.9f, outer = side * ew * 1.1f;
            float by = -eh * 2.2f;
            var browInner = c + new Vector2(inner, by - (mood < 0 ? -mood * eh * 1.2f : 0)) * _s;
            var browOuter = c + new Vector2(outer, by + eh * 0.3f) * _s;
            Line(browInner, browOuter, hair.Darkened(0.2f), 0.008f + (float)f.Brows * 0.012f);

            if (_v.Glasses)
            {
                DrawArc(c, ew * 1.45f * _s, 0, Mathf.Tau, 24, T(new Color("2a2a2a")), Mathf.Max(1, _s * 0.009f), true);
            }
        }
        if (_v.Glasses) Line(P(cx - spacing + ew * 1.45f, ey), P(cx + spacing - ew * 1.45f, ey), new Color("2a2a2a"), 0.009f);

        // Nose.
        float nw = (0.018f + (float)f.Nose * 0.022f) * (child ? 0.7f : 1f);
        float ny = cy + ry * (child ? 0.3f : 0.28f);
        Curve(new[] { P(cx - nw * 0.2f, ey + 0.03f), P(cx - nw, ny), P(cx - nw * 0.2f, ny + 0.012f), P(cx + nw * 0.6f, ny + 0.004f) }, skin.Darkened(0.28f), 0.007f);

        // Moustache.
        float my = cy + ry * (child ? 0.55f : 0.55f);
        float mw = rx * (0.28f + (float)f.Mouth * 0.22f) * (child ? 0.8f : 1f);
        if (beardAge && f.Beard is 2 or 3)
            Poly(new[] { P(cx - mw * 1.05f, my - 0.005f), P(cx, my - 0.04f), P(cx + mw * 1.05f, my - 0.005f), P(cx, my - 0.018f) }, hair);

        // Mouth: a curve that smiles or frowns with the mood.
        var lips = skin.Lerp(new Color("b24a4a"), 0.35f).Darkened(0.15f);
        var mouth = new List<Vector2>();
        for (int i = 0; i <= 10; i++)
        {
            float t = i / 10f;
            float x = cx - mw + t * mw * 2;
            float bend = mood * 0.028f * (1 - Mathf.Pow(2 * t - 1, 2));
            mouth.Add(P(x, my + bend - mood * 0.01f));
        }
        Curve(mouth, lips, 0.007f + (float)f.Lips * 0.009f);

        // Hair on top.
        DrawHairTop(cx, cy, rx, ry, hair, style, baby, child, f.Curl);

        // A print's white border; the player gets the era's accent colour instead.
        float ring = Mathf.Max(2, _s * 0.035f);
        DrawArc(P(0.5f, 0.5f), _s / 2 - ring / 2, 0, Mathf.Tau, 64, T(UiTheme.Panel.Lightened(0.35f)), ring, true);
        if (_highlight) DrawArc(P(0.5f, 0.5f), _s / 2 - 1.5f, 0, Mathf.Tau, 64, _v.Alive ? UiTheme.Accent : new Color("777777"), 3, true);
    }

    /// <summary>Head outline: an ellipse whose lower half narrows into the jaw and chin.</summary>
    private Vector2[] Head(float cx, float cy, float rx, float ry, float jaw, float chin, float scale, float lowerFrom = -1)
    {
        var pts = new List<Vector2>();
        const int n = 48;
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.Tau * i / n;
            float x = Mathf.Sin(a), y = -Mathf.Cos(a);
            if (y > 0)
            {
                x *= Mathf.Lerp(1, jaw, Mathf.Pow(y, 1.6f));
                y *= 1 + chin * 0.12f * y;
            }
            if (lowerFrom > -1 && y < lowerFrom) continue; // only the part of the face below lowerFrom
            pts.Add(P(cx + x * rx * scale, cy + y * ry * scale));
        }
        return pts.ToArray();
    }

    private void DrawHairTop(float cx, float cy, float rx, float ry, Color hair, int style, bool baby, bool child, double curl)
    {
        float bald = (float)_v.Bald;
        if (baby)
        {
            // A little tuft.
            Curve(new[] { P(cx - 0.02f, cy - ry * 0.95f), P(cx, cy - ry * 1.12f), P(cx + 0.025f, cy - ry * 0.98f) }, hair, 0.012f);
            return;
        }

        // How far down the forehead the hair comes (higher = receding).
        float fringe = _v.Male
            ? style switch { 2 => 0.62f, 3 => 0.4f, _ => 0.5f }
            : style switch { 1 => 0.25f, _ => 0.48f };
        fringe += bald * 0.45f;
        float thick = _v.Male && style == 2 ? 1.03f : 1.09f;

        // The top of the head, from temple to temple, then back along the hairline.
        var cap = new List<Vector2>();
        float spread = Mathf.Lerp(1.95f, 1.6f, bald);
        for (int i = 0; i <= 24; i++)
        {
            float a = -spread + 2 * spread * i / 24f;
            cap.Add(P(cx + Mathf.Sin(a) * rx * thick, cy - Mathf.Cos(a) * ry * thick));
        }
        float part = style == 1 || (_v.Male && style == 0) ? 0.25f : 0f;
        for (int i = 24; i >= 0; i--)
        {
            float t = i / 24f;
            float x = cx - rx * 0.95f + t * rx * 1.9f;
            float wave = Mathf.Sin(t * Mathf.Pi) * 0.06f + (t < part ? 0.04f : 0);
            // Never above the outline of the hair itself (a receding hairline on a bald head).
            float dx = (x - cx) / (rx * thick);
            float outline = Mathf.Sqrt(Mathf.Max(0, 1 - dx * dx)) * thick;
            cap.Add(P(x, cy - ry * Mathf.Min(fringe + wave, outline - 0.08f)));
        }

        if (bald < 0.75f)
        {
            Poly(cap.ToArray(), hair with { A = 1 - Mathf.Max(0, bald - 0.4f) * 1.5f });
        }
        // Sides stay longest.
        foreach (float side in new[] { -1f, 1f })
        {
            var sidePts = new[]
            {
                P(cx + side * rx * 1.08f, cy - ry * 0.35f), P(cx + side * rx * 0.9f, cy - ry * 0.45f),
                P(cx + side * rx * 0.88f, cy + ry * 0.05f), P(cx + side * rx * 1.04f, cy + ry * 0.1f),
            };
            Poly(sidePts, hair);
        }

        // Curls along the outline.
        if (curl > 0.6 && bald < 0.6f)
        {
            float r = 0.026f + (float)(curl - 0.6) * 0.05f;
            for (int i = 0; i <= 10; i++)
            {
                float a = -1.7f + 3.4f * i / 10f;
                DrawCircle(P(cx + Mathf.Sin(a) * rx * 1.08f, cy - Mathf.Cos(a) * ry * 1.07f), r * _s, T(hair));
            }
        }

        // Women's side hair down to the jaw (bob and long styles), and a bun for style 3.
        if (!_v.Male)
        {
            if (style is 0 or 1 or 2)
                foreach (float side in new[] { -1f, 1f })
                    Poly(new[]
                    {
                        P(cx + side * rx * 1.1f, cy - ry * 0.4f), P(cx + side * rx * 0.86f, cy - ry * 0.35f),
                        P(cx + side * rx * 0.9f, cy + ry * (style == 2 ? 0.6f : 0.75f)), P(cx + side * rx * 1.15f, cy + ry * (style == 2 ? 0.62f : 0.85f)),
                    }, hair);
        }
        if (!_v.Male && style == 3)
            DrawCircle(P(cx + rx * 0.2f, cy - ry * 1.12f), rx * 0.32f * _s, T(hair));
    }
}
