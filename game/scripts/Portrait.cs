using System;
using System.Collections.Generic;
using Godot;
using OneMoreYear.Simulation;

namespace OneMoreYear.Game;

/// <summary>
/// A generated portrait drawn from the person's genes (see Simulation.Systems.Faces): face shape,
/// skin, hair, eyes and features inherited from the parents, plus age (children's proportions,
/// grey hair, baldness, wrinkles), weight, mood, glasses and beard. The haircut follows the fashion
/// of the picture's decade, and the clothes follow the job. The dead are drawn in grey.
/// </summary>
public partial class Portrait : Control
{
    private PortraitView _v = null!;
    private bool _highlight;
    /// <summary>A square photograph (for prints with a paper border) instead of a round cameo.</summary>
    private bool _square;

    public static Portrait Create(PortraitView view, bool highlight, float size, bool square = false)
    {
        var p = new Portrait { CustomMinimumSize = new Vector2(size, size), MouseFilter = MouseFilterEnum.Ignore };
        p._v = view;
        p._highlight = highlight;
        p._square = square;
        // Photos look like prints of their time (sepia in the fifties, faded in the seventies).
        if (view.Alive) p.Modulate = UiTheme.PhotoTint;
        return p;
    }

    // --- Colours --------------------------------------------------------------------------------

    public static Color SkinColor(double tone)
    {
        var light = new Color("f3d4bf");
        var mid = new Color("c98d63");
        var dark = new Color("5a3926");
        float t = (float)tone;
        return t < 0.5f ? light.Lerp(mid, t * 2) : mid.Lerp(dark, (t - 0.5f) * 2);
    }

    public static Color HairColor(string name, double grey, int age)
    {
        var c = name switch
        {
            "black" => new Color("1f1b18"),
            "dark brown" => new Color("3d2b1f"),
            "brown" => new Color("624531"),
            "dark blond" => new Color("8e6d45"),
            "blond" => new Color("d6b676"),
            "red" => new Color("a64a2b"),
            _ => new Color("624531"),
        };
        if (age < 4) c = c.Lightened(0.15f);
        float g = (float)grey;
        c = c.Lerp(new Color("9c9c9c"), Mathf.Clamp(g * 1.1f, 0, 1));
        if (g > 0.8f) c = c.Lerp(new Color("e8e8e8"), (g - 0.8f) / 0.2f);
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

    // A soft darkening towards the edge of the photograph: smooth, with no bands.
    private static readonly Texture2D RoundVignette = Radial.Make(128, new Vector2(0.5f, 0.5f), 0.5f, d =>
        d > 1 ? new Color(0, 0, 0, 0) : new Color(0.12f, 0.08f, 0.04f, 0.32f * Radial.Step(0.74f, 0.985f, d)));
    private static readonly Texture2D SquareVignette = Radial.Make(128, new Vector2(0.5f, 0.5f), 0.5f, d =>
        new Color(0.12f, 0.08f, 0.04f, 0.36f * Radial.Step(0.8f, 1.42f, d)));

    // --- Geometry helpers --------------------------------------------------------------------

    private float _s;
    private Vector2 _o;
    private Vector2[] _clip = Array.Empty<Vector2>();
    private Vector2[] _head = Array.Empty<Vector2>();

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
        // A shape that folds over itself cannot be filled; skip it rather than spam the log.
        if (pts.Length >= 3 && Geometry2D.TriangulatePolygon(pts).Length > 0) DrawColoredPolygon(pts, T(c));
    }

    /// <summary>Draws a polygon clipped to another (the round frame, or the face).</summary>
    private void ClippedTo(Vector2[] pts, Vector2[] clip, Color c)
    {
        foreach (var part in Geometry2D.IntersectPolygons(pts, clip))
            if (!Geometry2D.IsPolygonClockwise(part) || part.Length >= 3) Poly(part, c);
    }

    private void Clipped(Vector2[] pts, Color c) => ClippedTo(pts, _clip, c);

    private void Line(Vector2 a, Vector2 b, Color c, float width) => DrawLine(a, b, T(c), Mathf.Max(1, width * _s), true);

    private void Curve(IList<Vector2> pts, Color c, float width) =>
        DrawPolyline(new List<Vector2>(pts).ToArray(), T(c), Mathf.Max(1, width * _s), true);

    /// <summary>A smooth quadratic curve through a control point, as points.</summary>
    private static List<Vector2> Bezier(Vector2 a, Vector2 ctrl, Vector2 b, int n = 12)
    {
        var pts = new List<Vector2>(n + 1);
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            pts.Add(a.Lerp(ctrl, t).Lerp(ctrl.Lerp(b, t), t));
        }
        return pts;
    }

    /// <summary>A stable pseudo random number for this person (0–1), so choices never flicker.</summary>
    private float Hash(int salt)
    {
        uint h = (uint)(_v.Id * 2654435761u) ^ (uint)(salt * 40503);
        h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
        return (h % 10000) / 10000f;
    }

    // --- Drawing -----------------------------------------------------------------------------

    private float _cx, _cy, _rx, _ry;

    public override void _Draw()
    {
        if (_v == null) return;
        _s = Mathf.Min(Size.X, Size.Y);
        _o = (Size - new Vector2(_s, _s)) / 2;
        var f = _v.Face;
        int age = _v.Age;
        bool baby = age < 2, child = age < 13;
        float heavy = (float)_v.Heaviness;

        _clip = _square ? new[] { P(0, 0), P(1, 0), P(1, 1), P(0, 1) } : Ellipse(P(0.5f, 0.5f), _s / 2, _s / 2, 64);
        // A soft studio backdrop, lighter behind the head and falling into shadow at the edges.
        Poly(_clip, UiTheme.PhotoBackdrop);
        for (int i = 0; i < 6; i++)
            Poly(Ellipse(P(0.5f, 0.38f), _s * (0.42f - i * 0.06f), _s * (0.36f - i * 0.05f), 40), UiTheme.PhotoBackdrop.Lightened(0.035f) with { A = 0.35f });
        DrawTextureRect(_square ? SquareVignette : RoundVignette, new Rect2(_o, new Vector2(_s, _s)), false, T(Colors.White));

        // Everyone has their own undertone: a little pinker, more golden or more olive.
        var skin = SkinColor(f.Skin);
        float under = Hash(31);
        skin = under < 0.33f ? skin.Lerp(new Color("e8a59a"), 0.12f) : under < 0.66f ? skin.Lerp(new Color("e2b277"), 0.1f) : skin.Lerp(new Color("b9a27a"), 0.1f);
        var skinShade = skin.Darkened(0.16f);
        // And their own shade of their hair colour.
        var hair = HairColor(_v.HairColor, _v.Grey, age);
        hair = hair.Lerp(Hash(32) < 0.5f ? new Color("8a5a2a") : new Color("2a2622"), 0.08f + Hash(33) * 0.1f).Lightened((Hash(34) - 0.5f) * 0.12f);

        // Proportions: children have bigger heads and eyes, heavier people wider faces.
        _cx = 0.5f;
        _cy = baby ? 0.5f : child ? 0.47f : 0.44f;
        _rx = (baby ? 0.25f : child ? 0.225f : 0.195f) * (0.9f + (float)f.Width * 0.18f) * (0.9f + heavy * 0.22f);
        _ry = (baby ? 0.25f : child ? 0.255f : 0.26f) * (0.93f + (float)f.Length * 0.14f);
        _rx *= 1.04f - (float)f.Length * 0.08f;
        float jaw = baby ? 0.95f : child ? 0.82f : 0.58f + (float)f.Jaw * 0.3f + heavy * 0.1f;
        float chin = child ? 0 : (float)f.Chin;
        float cx = _cx, cy = _cy, rx = _rx, ry = _ry;

        string cut = Haircut(age, child, baby);

        // Hair that falls behind the head and shoulders.
        if (_v.Bald < 0.5) DrawHairBack(cut, hair);

        // Shoulders, clothes and neck.
        DrawBody(skin, skinShade, heavy, baby);

        // Ears, head.
        float earR = 0.03f + (float)f.Ears * 0.018f;
        foreach (float side in new[] { -1f, 1f })
        {
            var ear = P(cx + side * rx * 0.97f, cy + ry * 0.06f);
            Poly(Ellipse(ear, earR * _s, earR * 1.5f * _s, 16), skin.Darkened(0.06f));
            Poly(Ellipse(ear + new Vector2(side * -0.004f, 0) * _s, earR * 0.5f * _s, earR * 0.9f * _s, 12), skinShade with { A = 0.6f });
            if (!_v.Male && age >= 15 && f.Style2 is > 0.15 and < 0.55)
                DrawCircle(ear + new Vector2(0, earR * 1.45f) * _s, Mathf.Max(1.5f, _s * 0.009f), T(f.Style2 < 0.35 ? new Color("d4af37") : new Color("e8e4dc")));
        }
        _head = Head(cx, cy, rx, ry, jaw, chin, 1f);
        // Light from the upper left: the whole head in shade, then the lit part on top of it,
        // which leaves a soft dark rim on the right and under the jaw.
        Poly(_head, skin.Darkened(0.12f));
        ClippedTo(Head(cx - rx * 0.07f, cy - ry * 0.04f, rx, ry, jaw, chin, 0.97f), _head, skin);
        // A fine ink line round the face, like an illustration.
        var faceLine = new List<Vector2>(_head) { _head[0] };
        Curve(faceLine, skin.Darkened(0.42f) with { A = 0.55f }, 0.0045f);
        float blush = baby || child ? 0.3f : !_v.Male ? 0.2f : 0.08f;
        blush += (float)Math.Max(0, _v.Mood) * 0.08f;
        foreach (float side in new[] { -1f, 1f })
            ClippedTo(Ellipse(P(cx + side * rx * 0.55f, cy + ry * 0.3f), rx * 0.24f * _s, ry * 0.12f * _s, 20), _head, new Color("e0605a") with { A = blush * 0.4f });

        // Cheekbones that catch the light, with a soft hollow under them.
        if (!child && f.Cheekbones > 0.55)
            foreach (float side in new[] { -1f, 1f })
                ClippedTo(Ellipse(P(cx + side * rx * 0.62f, cy + ry * 0.36f), rx * 0.2f * _s, ry * 0.07f * _s, 16), _head, skin.Darkened(0.14f) with { A = ((float)f.Cheekbones - 0.55f) * 1.2f });
        // A mole, always in the same place for the same person.
        if (!baby && f.Mole > 0.7)
            DrawCircle(P(cx + (Hash(21) * 2 - 1) * rx * 0.55f, cy + ry * (0.12f + Hash(22) * 0.5f)), Mathf.Max(1.2f, _s * 0.0065f), T(skin.Darkened(0.5f)));
        // A cleft chin.
        if (!child && f.Cleft > 0.75)
            Curve(Bezier(P(cx, cy + ry * 0.84f), P(cx + 0.002f, cy + ry * 0.9f), P(cx, cy + ry * 0.95f), 4), skin.Darkened(0.28f) with { A = 0.6f }, 0.005f);

        // Freckles.
        if (f.Freckles > 0.35 && !baby)
        {
            var rnd = new Random(_v.Id);
            int n = (int)((f.Freckles - 0.3) * 34);
            for (int i = 0; i < n; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                var at = P(cx + side * rx * (0.18f + (float)rnd.NextDouble() * 0.4f), cy + ry * (0.1f + (float)rnd.NextDouble() * 0.22f));
                DrawCircle(at, Mathf.Max(0.7f, _s * 0.005f), T(skin.Darkened(0.32f) with { A = 0.65f }));
            }
        }

        // Wrinkles: forehead, laughter lines, crow's feet.
        if (age > 40)
        {
            float a = Mathf.Clamp((age - 40) / 40f, 0, 1) * 0.5f;
            var line = skin.Darkened(0.32f) with { A = a };
            for (int i = 0; i < 1 + (int)(a * 5); i++)
            {
                float y = cy - ry * (0.52f - i * 0.08f);
                Curve(Bezier(P(cx - rx * 0.42f, y + 0.004f), P(cx, y - 0.012f), P(cx + rx * 0.42f, y + 0.004f), 8), line, 0.005f);
            }
            if (age > 50)
                foreach (float side in new[] { -1f, 1f })
                    Curve(Bezier(P(cx + side * rx * 0.22f, cy + ry * 0.24f), P(cx + side * rx * 0.42f, cy + ry * 0.38f), P(cx + side * rx * 0.36f, cy + ry * 0.58f), 8), line, 0.006f);
        }

        // Beard (grown men).
        bool beardAge = _v.Male && age >= 18;
        if (beardAge && f.Beard is 1 or 2)
        {
            // The jaw from one sideburn round the chin to the other, then back up over the cheeks and
            // along the upper lip, so the beard has the shape of a beard and not of a mask.
            var outline = Head(cx, cy, rx, ry, jaw, chin, 1.03f, lowerFrom: 0.02f);
            var beard = new List<Vector2>();
            // Head() runs clockwise from the top; the lower part comes as right side, chin, left side.
            beard.AddRange(outline);
            float my = cy + ry * 0.56f;
            float mw = rx * (0.3f + (float)f.Mouth * 0.2f);
            beard.AddRange(Bezier(P(cx - rx * 0.92f, cy + ry * 0.04f), P(cx - rx * 0.62f, cy + ry * 0.42f), P(cx - mw * 1.25f, my - 0.02f), 8));
            beard.AddRange(Bezier(P(cx - mw * 1.1f, my - 0.03f), P(cx, my - 0.05f), P(cx + mw * 1.1f, my - 0.03f), 8));
            beard.AddRange(Bezier(P(cx + mw * 1.25f, my - 0.02f), P(cx + rx * 0.62f, cy + ry * 0.42f), P(cx + rx * 0.92f, cy + ry * 0.04f), 8));
            var shape = beard.ToArray();
            if (Geometry2D.IsPolygonClockwise(shape) == Geometry2D.IsPolygonClockwise(outline)) { }
            Poly(shape, hair with { A = f.Beard == 2 ? 0.95f : 0.3f });
        }

        DrawEyes(skin, hair, child, baby);
        DrawNose(skin, child);
        DrawMouth(skin, hair, child, beardAge);
        if (_v.Bald < 0.85 || baby) DrawHairFront(cut, hair, baby);

        // A thin ink line round the cameo; the player gets the era's accent colour. Square prints get their border from the paper.
        if (!_square) DrawArc(P(0.5f, 0.5f), _s / 2 - 1f, 0, Mathf.Tau, 64, T(UiTheme.Text with { A = 0.45f }), Mathf.Max(1.5f, _s * 0.012f), true);
        if (_highlight && !_square) DrawArc(P(0.5f, 0.5f), _s / 2 - 1.5f, 0, Mathf.Tau, 64, _v.Alive ? UiTheme.Accent : new Color("777777"), 3, true);
    }

    /// <summary>Head outline: an ellipse whose lower half narrows into the jaw and chin.</summary>
    private Vector2[] Head(float cx, float cy, float rx, float ry, float jaw, float chin, float scale, float lowerFrom = -1)
    {
        var pts = new List<Vector2>();
        const int n = 56;
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.Tau * i / n;
            float x = Mathf.Sin(a), y = -Mathf.Cos(a);
            if (y > 0)
            {
                x *= Mathf.Lerp(1, jaw, Mathf.Pow(y, 1.6f));
                y *= 1 + chin * 0.12f * y;
            }
            if (lowerFrom > -1 && y < lowerFrom) continue;
            pts.Add(P(cx + x * rx * scale, cy + y * ry * scale));
        }
        return pts.ToArray();
    }

    // --- Body and clothes ----------------------------------------------------------------------

    private void DrawBody(Color skin, Color skinShade, float heavy, bool baby)
    {
        float cx = _cx, cy = _cy, rx = _rx, ry = _ry;
        if (baby)
        {
            Clipped(Ellipse(P(0.5f, 1.06f), 0.32f * _s, 0.22f * _s, 48), new Color("ece6d6"));
            return;
        }
        bool child = _v.Age < 13;
        float sw = (child ? 0.3f : 0.37f) + heavy * 0.08f + (float)(_v.Fitness - 50) / 1200f;
        float shoulderTop = child ? 0.84f : 0.8f;
        var shirt = ShirtColor(_v.Id);
        string outfit = _v.Outfit;
        var cloth = outfit switch
        {
            "suit" => new[] { new Color("2b2f3a"), new Color("3a3226"), new Color("33363d") }[_v.Id % 3],
            "scrubs" => new Color("5f9ea0"),
            "uniform" => new Color("2f3b52"),
            "work" => new[] { new Color("3a5a7a"), new Color("4f5a3a"), new Color("7a4a2a") }[_v.Id % 3],
            "cardigan" => shirt.Lerp(new Color("b5a98f"), 0.45f),
            "smart" => shirt.Darkened(0.1f),
            _ => shirt,
        };

        // The neck first, with a shadow under the jaw; the clothes go over its foot.
        float nw = rx * (0.4f + heavy * 0.1f) * (_v.Male ? 1.08f : 0.95f);
        float neckTop = cy + ry * 0.5f;
        Poly(new[] { P(cx - nw, neckTop), P(cx + nw, neckTop), P(cx + nw * 1.12f, shoulderTop + 0.05f), P(cx - nw * 1.12f, shoulderTop + 0.05f) }, skin.Darkened(0.04f));
        Poly(Ellipse(P(cx, cy + ry * 0.86f), nw * 1.1f * _s, ry * 0.18f * _s, 20), skinShade with { A = 0.6f });

        // Shoulders: a wide rounded bust, clipped to the frame.
        var body = new List<Vector2>();
        body.AddRange(Bezier(P(cx - sw * 1.3f, 1.05f), P(cx - sw * 1.05f, shoulderTop), P(cx - nw * 1.4f, shoulderTop - 0.01f), 14));
        body.AddRange(Bezier(P(cx + nw * 1.4f, shoulderTop - 0.01f), P(cx + sw * 1.05f, shoulderTop), P(cx + sw * 1.3f, 1.05f), 14));
        body.Add(P(cx + sw * 1.3f, 1.1f));
        body.Add(P(cx - sw * 1.3f, 1.1f));
        var bodyPts = body.ToArray();
        Clipped(bodyPts, cloth);
        // The shaded side.
        Clipped(Ellipse(P(cx + sw * 0.9f, 1.02f), sw * 0.55f * _s, 0.2f * _s, 24), cloth.Darkened(0.2f) with { A = 0.45f });

        var light = new Color("efeae0");
        float collarY = shoulderTop - 0.005f;
        switch (outfit)
        {
            case "suit":
                // Shirt and tie in the opening, lapels either side.
                Clipped(new[] { P(cx - nw * 1.2f, collarY), P(cx + nw * 1.2f, collarY), P(cx + 0.012f, 1.0f), P(cx - 0.012f, 1.0f) }, light);
                Clipped(new[] { P(cx - 0.016f, collarY + 0.025f), P(cx + 0.016f, collarY + 0.025f), P(cx + 0.026f, 1f), P(cx, 1.02f), P(cx - 0.026f, 1f) },
                    new[] { new Color("8a2c2c"), new Color("2c4a8a"), new Color("5a5a2c") }[_v.Id % 3]);
                foreach (float side in new[] { -1f, 1f })
                    Clipped(new[] { P(cx + side * nw * 1.25f, collarY), P(cx + side * 0.06f, 1.0f), P(cx + side * 0.15f, 1.0f), P(cx + side * nw * 1.9f, collarY + 0.02f) }, cloth.Darkened(0.28f));
                break;
            case "scrubs":
                Clipped(new[] { P(cx - nw * 1.15f, collarY), P(cx + nw * 1.15f, collarY), P(cx, collarY + 0.1f) }, skin.Darkened(0.04f));
                break;
            case "uniform":
                foreach (float side in new[] { -1f, 1f })
                {
                    Clipped(new[] { P(cx + side * nw * 1.2f, collarY - 0.005f), P(cx, collarY + 0.06f), P(cx + side * 0.02f, collarY + 0.09f), P(cx + side * nw * 1.6f, collarY + 0.03f) }, cloth.Lightened(0.15f));
                    Clipped(new[] { P(cx + side * sw * 0.55f, collarY + 0.02f), P(cx + side * sw * 0.9f, collarY + 0.035f), P(cx + side * sw * 0.88f, collarY + 0.06f), P(cx + side * sw * 0.53f, collarY + 0.045f) }, new Color("c9a64a"));
                }
                DrawCircle(P(cx, collarY + 0.13f), _s * 0.008f, T(new Color("c9a64a")));
                break;
            case "work":
                Clipped(Bezier(P(cx - nw * 1.15f, collarY), P(cx, collarY + 0.08f), P(cx + nw * 1.15f, collarY), 10).ToArray(), skin.Darkened(0.04f));
                foreach (float side in new[] { -1f, 1f })
                    Clipped(new[] { P(cx + side * nw * 1.6f, collarY + 0.01f), P(cx + side * nw * 2.3f, collarY + 0.015f), P(cx + side * nw * 2.1f, 1.05f), P(cx + side * nw * 1.4f, 1.05f) }, cloth.Darkened(0.3f));
                break;
            case "smart":
                Clipped(new[] { P(cx - nw * 1.1f, collarY), P(cx + nw * 1.1f, collarY), P(cx, collarY + 0.07f) }, skin.Darkened(0.04f));
                foreach (float side in new[] { -1f, 1f })
                    Clipped(new[] { P(cx + side * nw * 1.15f, collarY - 0.012f), P(cx + side * 0.004f, collarY + 0.075f), P(cx + side * nw * 1.9f, collarY + 0.05f) }, light);
                break;
            case "cardigan":
                Clipped(Bezier(P(cx - nw * 1.15f, collarY), P(cx, collarY + 0.07f), P(cx + nw * 1.15f, collarY), 10).ToArray(), shirt.Lightened(0.3f));
                Clipped(new[] { P(cx - nw * 0.9f, collarY + 0.02f), P(cx + nw * 0.9f, collarY + 0.02f), P(cx + 0.015f, 1.02f), P(cx - 0.015f, 1.02f) }, shirt.Lightened(0.3f));
                for (int i = 0; i < 2; i++) DrawCircle(P(cx + 0.03f, collarY + 0.08f + i * 0.045f), _s * 0.007f, T(cloth.Darkened(0.35f)));
                break;
            default:
                // A round neckline.
                Clipped(Bezier(P(cx - nw * 1.15f, collarY), P(cx, collarY + 0.07f), P(cx + nw * 1.15f, collarY), 10).ToArray(), skin.Darkened(0.04f));
                Curve(Bezier(P(cx - nw * 1.2f, collarY), P(cx, collarY + 0.075f), P(cx + nw * 1.2f, collarY), 10), cloth.Darkened(0.22f), 0.008f);
                break;
        }
    }

    // --- Eyes, nose, mouth ---------------------------------------------------------------------

    private void DrawEyes(Color skin, Color hair, bool child, bool baby)
    {
        var f = _v.Face;
        float cx = _cx, cy = _cy, rx = _rx, ry = _ry;
        float ey = cy + ry * (child ? 0.05f : -0.02f);
        float spacing = rx * (0.36f + (float)f.EyeSpacing * 0.12f);
        float ew = (0.031f + (float)f.Eyes * 0.02f) * (child ? 1.2f : 1f);
        float eh = ew * (child ? 0.7f : 0.56f) * (1.15f - (float)f.EyeShape * 0.35f);
        // Outer corners lifted or lowered, the same for both lids so the shape holds.
        float tilt = 0.06f + (0.5f - (float)f.EyeTilt) * 0.36f;
        float mood = (float)_v.Mood;
        int age = _v.Age;
        // Happy eyes narrow a little from below; old eyes from above.
        float squint = Mathf.Max(0, mood) * 0.18f + (age > 60 ? 0.12f : 0);

        foreach (float side in new[] { -1f, 1f })
        {
            var c = P(cx + side * spacing, ey);
            // An almond: an upper and a lower arc.
            var almond = new List<Vector2>();
            for (int i = 0; i <= 14; i++)
            {
                float t = i / 14f, x = -1 + 2 * t;
                float up = Mathf.Sqrt(Mathf.Max(0, 1 - x * x)) * (1 - squint * 0.4f);
                almond.Add(c + new Vector2(x * ew, -up * eh + side * x * eh * tilt) * _s);
            }
            for (int i = 14; i >= 0; i--)
            {
                float t = i / 14f, x = -1 + 2 * t;
                float down = Mathf.Sqrt(Mathf.Max(0, 1 - x * x)) * (0.75f - squint * 0.4f);
                almond.Add(c + new Vector2(x * ew, down * eh + side * x * eh * tilt) * _s);
            }
            var shape = almond.ToArray();
            Poly(shape, new Color("f6f2ea"));
            var irisAt = c + new Vector2(0, eh * 0.05f) * _s;
            ClippedTo(Ellipse(irisAt, eh * 1.0f * _s, eh * 1.0f * _s, 20), shape, EyeColor(_v.EyeColor));
            ClippedTo(Ellipse(irisAt, eh * 0.5f * _s, eh * 0.5f * _s, 16), shape, new Color("141414"));
            DrawCircle(irisAt + new Vector2(-eh * 0.35f, -eh * 0.35f) * _s, Mathf.Max(0.8f, eh * 0.24f * _s), T(new Color(1, 1, 1, 0.9f)));
            // The upper lid line, heavier at the outer corner; lashes for women and children.
            var lid = almond.GetRange(0, 15);
            Curve(lid, new Color("2a1f1a") with { A = 0.85f }, (baby ? 0.006f : 0.008f) + (!_v.Male ? 0.003f : 0));
            if (!_v.Male || baby)
            {
                var outer = lid[side < 0 ? 0 : 14];
                Line(outer, outer + new Vector2(side * ew * 0.35f, -eh * 0.35f) * _s, new Color("2a1f1a"), 0.005f);
            }
            if (age > 45)
                Curve(Bezier(c + new Vector2(-ew * 0.8f, eh * 0.9f) * _s, c + new Vector2(0, eh * 1.5f) * _s, c + new Vector2(ew * 0.8f, eh * 0.9f) * _s, 8),
                    skin.Darkened(0.25f) with { A = Mathf.Clamp((age - 45) / 40f, 0, 0.6f) }, 0.005f);

            // Brows: tapered; sad people raise the inner end, angry ones lower it.
            float by = -eh * 2.3f;
            var inner = c + new Vector2(-side * ew * 0.95f, by - (mood < 0 ? -mood * eh * 1.2f : 0)) * _s;
            var mid = c + new Vector2(side * ew * 0.1f, by - eh * (0.15f + (float)f.BrowArch * 0.7f)) * _s;
            var outerB = c + new Vector2(side * ew * 1.15f, by + eh * 0.35f) * _s;
            float thick = (0.008f + (float)f.Brows * 0.012f) * _s * (_v.Male ? 1.1f : 0.8f);
            var brow = new List<Vector2>();
            foreach (var p in Bezier(inner, mid, outerB, 10)) brow.Add(p);
            var lower = Bezier(outerB, mid + new Vector2(0, thick * 1.2f), inner + new Vector2(0, thick), 10);
            brow.AddRange(lower);
            Poly(brow.ToArray(), hair.Darkened(0.22f));

            if (_v.Glasses) DrawLens(c, ew, side);
        }
        if (_v.Glasses) Line(P(cx - spacing + ew * LensHalfWidth(), ey - 0.004f), P(cx + spacing - ew * LensHalfWidth(), ey - 0.004f), FrameColour(), 0.009f);
    }


    // --- Glasses -------------------------------------------------------------------------------

    /// <summary>The frames of the time and the person: round, square, big in the eighties, cat eyes in the fifties.</summary>
    private string LensStyle()
    {
        int year = _v.Year > 0 ? _v.Year : 2000;
        float s = (float)_v.Face.Style2;
        if (!_v.Male && year < 1972 && s < 0.5f) return "cat";
        if (year is >= 1976 and < 1995 && s > 0.3f) return "big";
        return s < 0.4f ? "round" : "square";
    }

    private float LensHalfWidth() => LensStyle() switch { "big" => 1.8f, "cat" => 1.65f, "square" => 1.6f, _ => 1.5f };

    private Color FrameColour()
    {
        float s = (float)_v.Face.Style2;
        int year = _v.Year > 0 ? _v.Year : 2000;
        if (s > 0.72f) return new Color("6a4428");
        if (year is >= 1970 and < 1990 && s is > 0.4f and < 0.55f) return new Color("b08d3a");
        return new Color("2a2a2a");
    }

    private void DrawLens(Vector2 c, float ew, float side)
    {
        float width = Mathf.Max(1, _s * 0.009f);
        var colour = T(FrameColour());
        string style = LensStyle();
        if (style == "round") { DrawArc(c, ew * 1.5f * _s, 0, Mathf.Tau, 28, colour, width, true); return; }
        float hw = ew * LensHalfWidth() * _s, hh = ew * (style == "big" ? 1.55f : style == "cat" ? 1.1f : 1.15f) * _s;
        float r = hh * 0.45f;
        var pts = new List<Vector2>();
        // A rounded rectangle; cat eyes lift at the outer top corner.
        void Corner(Vector2 centre, float from)
        {
            for (int i = 0; i <= 5; i++)
            {
                float a = from + Mathf.Pi / 2 * i / 5;
                pts.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
        }
        Corner(c + new Vector2(hw - r, hh - r), 0);
        Corner(c + new Vector2(-hw + r, hh - r), Mathf.Pi / 2);
        Corner(c + new Vector2(-hw + r, -hh + r), Mathf.Pi);
        Corner(c + new Vector2(hw - r, -hh + r), Mathf.Pi * 1.5f);
        if (style == "cat")
            for (int i = 0; i < pts.Count; i++)
            {
                float outer = (pts[i].X - c.X) * side / hw, upper = -(pts[i].Y - c.Y) / hh;
                if (outer > 0.3f && upper > 0.2f) pts[i] += new Vector2(side * 0.25f, -0.45f) * hh * outer * upper;
            }
        pts.Add(pts[0]);
        DrawPolyline(pts.ToArray(), colour, width, true);
    }
    private void DrawNose(Color skin, bool child)
    {
        var f = _v.Face;
        float cx = _cx, cy = _cy, ry = _ry;
        float nw = (0.016f + (float)f.Nose * 0.02f) * (child ? 0.7f : 1f);
        float top = cy + ry * (child ? 0.18f : 0.14f);
        // Three kinds of nose: a short button, a straight one, and one with a bump on the bridge.
        float shape = child ? 0.2f : (float)f.NoseShape;
        float length = shape < 0.33f ? 0.82f : shape > 0.66f ? 1.14f : 1f;
        float ny = top + (cy + ry * 0.3f - top) * length;
        float bump = shape > 0.66f ? nw * (1.5f + (shape - 0.66f) * 3f) : nw * 0.9f;
        float tip = shape < 0.33f ? 0.95f : shape > 0.66f ? 0.7f : 0.75f;
        // A soft shadow down the shaded side, the tip and the nostrils.
        Curve(Bezier(P(cx + nw * 0.4f, top), P(cx + bump, (top + ny) / 2), P(cx + nw * 1.1f, ny - 0.004f), 8), skin.Darkened(0.22f) with { A = 0.7f }, 0.007f);
        if (shape > 0.66f && !child)
            Curve(Bezier(P(cx + nw * 0.2f, top + (ny - top) * 0.25f), P(cx + bump * 0.8f, top + (ny - top) * 0.45f), P(cx + nw * 0.5f, top + (ny - top) * 0.7f), 6), skin.Darkened(0.3f) with { A = 0.45f }, 0.005f);
        DrawCircle(P(cx, ny - 0.006f), nw * tip * _s, T(skin.Lightened(0.06f)));
        foreach (float side in new[] { -1f, 1f })
            DrawCircle(P(cx + side * nw * 0.55f, ny + 0.004f), Mathf.Max(0.8f, nw * 0.28f * _s), T(skin.Darkened(0.4f) with { A = 0.8f }));
    }

    private void DrawMouth(Color skin, Color hair, bool child, bool beardAge)
    {
        var f = _v.Face;
        float cx = _cx, cy = _cy, rx = _rx, ry = _ry;
        float my = cy + ry * 0.56f;
        float mw = rx * (0.27f + (float)f.Mouth * 0.2f) * (child ? 0.8f : 1f);
        float mood = (float)_v.Mood;
        float lip = (0.006f + (float)f.Lips * 0.012f) * (child ? 0.8f : 1f);

        if (beardAge && f.Beard is 2 or 3)
            Poly(new[] { P(cx - mw * 1.1f, my - 0.003f), P(cx - mw * 0.3f, my - 0.036f), P(cx, my - 0.03f), P(cx + mw * 0.3f, my - 0.036f), P(cx + mw * 1.1f, my - 0.003f), P(cx, my - 0.014f) }, hair);

        // The line between the lips smiles or frowns with the mood; at rest, people look kindly into a camera.
        float bend = (mood + 0.35f) * 0.026f;
        var line = Bezier(P(cx - mw, my - bend * 0.3f), P(cx, my + bend), P(cx + mw, my - bend * 0.3f), 12);
        var lipColor = skin.Lerp(new Color("b5524f"), _v.Male ? 0.3f : 0.48f).Darkened(0.08f);
        // Lipstick: most women in the fifties to the eighties, some always.
        int year = _v.Year > 0 ? _v.Year : 2000;
        if (!_v.Male && !child && _v.Age >= 18 && (f.Style2 > 0.85 || (year < 1990 && f.Style2 > 0.35)))
            lipColor = lipColor.Lerp(year < 1965 ? new Color("a8262e") : year < 1980 ? new Color("b5506a") : new Color("9a2f4a"), 0.6f);
        // Lower lip: a fuller curve under the line.
        var lowerLip = new List<Vector2>(line);
        lowerLip.AddRange(Bezier(P(cx + mw * 0.8f, my - bend * 0.2f), P(cx, my + bend + lip * 2.6f), P(cx - mw * 0.8f, my - bend * 0.2f), 12));
        Poly(lowerLip.ToArray(), lipColor.Lightened(0.06f));
        // Upper lip with a cupid's bow.
        var upper = new List<Vector2> { P(cx - mw, my - bend * 0.3f) };
        upper.AddRange(Bezier(P(cx - mw * 0.9f, my - bend * 0.3f - lip * 0.6f), P(cx - mw * 0.35f, my - lip * 1.8f), P(cx, my - lip * 1.1f + bend * 0.2f), 6));
        upper.AddRange(Bezier(P(cx, my - lip * 1.1f + bend * 0.2f), P(cx + mw * 0.35f, my - lip * 1.8f), P(cx + mw * 0.9f, my - bend * 0.3f - lip * 0.6f), 6));
        upper.Add(P(cx + mw, my - bend * 0.3f));
        for (int i = line.Count - 1; i >= 0; i--) upper.Add(line[i]);
        Poly(upper.ToArray(), lipColor.Darkened(0.08f));
        // Teeth for a real smile.
        if (mood > 0.55f && !child)
        {
            var teeth = new List<Vector2>(line);
            teeth.AddRange(Bezier(P(cx + mw * 0.7f, my), P(cx, my + bend * 0.9f + 0.006f), P(cx - mw * 0.7f, my), 10));
            Poly(teeth.ToArray(), new Color("f1ece2"));
        }
        Curve(line, lipColor.Darkened(0.4f), 0.005f);
    }

    // --- Hair ----------------------------------------------------------------------------------

    /// <summary>
    /// The haircut: what the person's genes allow (curly, balding), what their decade wore, and a
    /// stable personal choice among those. Grown men in the fifties had side partings; women in
    /// the eighties had perms; everyone in the seventies had more hair.
    /// </summary>
    private string Haircut(int age, bool child, bool baby)
    {
        if (baby) return "tuft";
        var f = _v.Face;
        int year = _v.Year > 0 ? _v.Year : 2000;
        bool afro = f.Curl > 0.78 && f.Skin > 0.55;
        bool curly = f.Curl > 0.62;
        float pick = Hash(7);
        string Choose(params string[] options) => options[Math.Min(options.Length - 1, (int)(pick * options.Length))];

        if (_v.Male)
        {
            if (afro) return age < 30 && year >= 1968 && year < 1985 ? "afro" : "short_curls";
            if (child) return year is >= 1965 and < 1985 ? Choose("bowl", "mop") : Choose("crop", "bowl", "side");
            if (curly) return Choose("short_curls", "curly_top");
            if (age > 60) return Choose("side", "crop", "slick");
            return year switch
            {
                < 1960 => Choose("slick", "side", "side"),
                < 1965 => age < 30 ? Choose("quiff", "side") : Choose("side", "slick"),
                < 1980 => age < 40 ? Choose("long", "mop", "side_long") : Choose("side", "side_long"),
                < 1995 => age < 35 ? Choose("mullet", "feathered", "crop") : Choose("side", "crop"),
                < 2010 => Choose("spiky", "crop", "buzz"),
                _ => age < 40 ? Choose("undercut", "crop", "buzz", "bun") : Choose("crop", "side"),
            };
        }
        if (afro) return age < 50 ? "afro" : "short_curls";
        if (child) return Choose("pigtails", "bob", "long", "ponytail");
        if (curly) return age > 55 ? "short_curls" : Choose("curly_long", "curly_bob");
        if (age > 60) return Choose("short_curls", "bun", "bob");
        return year switch
        {
            < 1965 => Choose("waves", "bun", "waves"),
            < 1980 => Choose("long", "long", "bob", "flick"),
            < 1995 => age < 40 ? Choose("perm", "perm", "long", "bob") : Choose("bob", "perm"),
            < 2010 => Choose("long", "ponytail", "bob", "pixie"),
            _ => Choose("long", "bun", "bob", "ponytail"),
        };
    }

    private void DrawHairBack(string cut, Color hair)
    {
        float cx = _cx, cy = _cy, rx = _rx, ry = _ry;
        var back = hair.Darkened(0.14f);
        switch (cut)
        {
            case "long" or "curly_long" or "perm" or "flick":
                float len = cut == "perm" ? 1.25f : cut == "flick" ? 1.05f : 1.4f;
                float wide = cut is "perm" or "curly_long" ? 1.45f : 1.22f;
                Clipped(Ellipse(P(cx, cy + ry * 0.35f), rx * wide * _s, ry * len * _s, 40), back);
                if (cut is "perm" or "curly_long")
                    for (int i = 0; i < 16; i++)
                    {
                        float a = Mathf.Tau * i / 16f;
                        Clipped(Ellipse(P(cx + Mathf.Cos(a) * rx * wide * 0.95f, cy + ry * 0.35f + Mathf.Sin(a) * ry * len * 0.95f), rx * 0.22f * _s, rx * 0.22f * _s, 12), back);
                    }
                break;
            case "afro":
                Clipped(Ellipse(P(cx, cy - ry * 0.25f), rx * 1.6f * _s, ry * 1.25f * _s, 40), back);
                break;
            case "bob" or "curly_bob" or "pigtails" or "side_long" or "mullet":
                float down = cut == "mullet" ? 0.95f : 0.7f;
                Clipped(Ellipse(P(cx, cy + ry * 0.12f), rx * 1.2f * _s, ry * (0.95f + down * 0.3f) * _s, 36), back);
                break;
            case "ponytail":
                Clipped(Ellipse(P(cx + rx * 0.9f, cy + ry * 0.4f), rx * 0.35f * _s, ry * 0.7f * _s, 20), back);
                break;
        }
    }

    private void DrawHairFront(string cut, Color hair, bool baby)
    {
        float cx = _cx, cy = _cy, rx = _rx, ry = _ry;
        float bald = (float)_v.Bald;
        if (cut == "tuft")
        {
            Curve(Bezier(P(cx - 0.02f, cy - ry * 0.95f), P(cx - 0.01f, cy - ry * 1.18f), P(cx + 0.03f, cy - ry * 1.02f)), hair, 0.012f);
            return;
        }
        var shine = hair.Lightened(0.22f) with { A = 0.35f };

        // fringe: how far down the forehead the hair comes (as a share of the head above its centre);
        // thick: how far the hair stands off the skull; part: where the parting is (0.5 = none).
        (float fringe, float thick, float part) = cut switch
        {
            "buzz" => (0.66f, 1.02f, 0.5f),
            "crop" or "spiky" or "undercut" => (0.6f, 1.05f, 0.5f),
            "side" or "slick" or "side_long" => (0.58f, 1.06f, 0.32f),
            "quiff" => (0.6f, 1.1f, 0.5f),
            "mop" or "bowl" or "long" or "mullet" or "feathered" => (0.32f, 1.08f, 0.5f),
            "pixie" => (0.4f, 1.06f, 0.35f),
            "afro" or "short_curls" or "curly_top" => (0.55f, 1.12f, 0.5f),
            "bun" or "ponytail" or "waves" => (0.55f, 1.07f, 0.38f),
            _ => (0.45f, 1.08f, 0.42f),
        };
        // A receding hairline climbs at the temples first, then everywhere.
        fringe += bald * 0.35f;
        float temples = (_v.Male ? 0.12f : 0.02f) + bald * 0.4f;

        // The shape: the outline over the top from the left side to the right, then down beside the
        // right ear, up over the right temple, across the forehead, over the left temple and down.
        const float sideAngle = 1.9f;
        var shape = new List<Vector2>();
        for (int i = 0; i <= 32; i++)
        {
            float a = -sideAngle + 2 * sideAngle * i / 32f;
            float lift = cut == "quiff" && Mathf.Abs(a) < 0.7f ? 1 + 0.12f * Mathf.Cos(a / 0.7f * Mathf.Pi / 2) : 1f;
            if (cut == "spiky") lift += (i % 2) * 0.05f * (1 - Mathf.Abs(a) / sideAngle);
            shape.Add(P(cx + Mathf.Sin(a) * rx * thick, cy - Mathf.Cos(a) * ry * thick * lift));
        }
        float templeY = cy - ry * (0.2f + temples);
        float earX = rx * 0.9f;
        shape.Add(P(cx + earX, cy + ry * 0.02f));
        shape.AddRange(Bezier(P(cx + earX * 0.98f, templeY), P(cx + rx * 0.75f, cy - ry * (fringe + 0.05f)), P(cx + rx * 0.35f, cy - ry * fringe), 8));
        if (part < 0.5f)
        {
            // A parting: the fringe sweeps across from the far side.
            float px = cx - rx * 0.9f + part * rx * 1.8f;
            shape.AddRange(Bezier(P(cx + rx * 0.3f, cy - ry * fringe), P(cx, cy - ry * (fringe - 0.12f)), P(px, cy - ry * (fringe + 0.18f)), 8));
            shape.Add(P(cx - rx * 0.35f, cy - ry * (fringe + 0.02f)));
        }
        else
        {
            float dip = cut is "bowl" or "mop" or "mullet" or "feathered" or "long" or "pixie" ? -0.08f : 0.04f;
            shape.AddRange(Bezier(P(cx + rx * 0.3f, cy - ry * fringe), P(cx, cy - ry * (fringe + dip)), P(cx - rx * 0.3f, cy - ry * fringe), 8));
        }
        shape.AddRange(Bezier(P(cx - rx * 0.35f, cy - ry * fringe), P(cx - rx * 0.75f, cy - ry * (fringe + 0.05f)), P(cx - earX * 0.98f, templeY), 8));
        shape.Add(P(cx - earX, cy + ry * 0.02f));

        float alpha = cut == "buzz" ? 0.75f : 1f;
        if (bald < 0.8f)
        {
            Poly(shape.ToArray(), hair with { A = alpha * (1 - Mathf.Max(0, bald - 0.45f) * 1.6f) });
            if (bald < 0.45f && cut is not "buzz")
            {
                // An ink outline and a few strands, so it reads as hair and not as a helmet.
                Curve(new List<Vector2>(shape) { shape[0] }, hair.Darkened(0.4f) with { A = 0.6f }, 0.0045f);
                if (cut is not "afro" and not "short_curls" and not "curly_top")
                    for (int k = 0; k < 6; k++)
                    {
                        float a = -1.25f + k * 0.5f;
                        Curve(Bezier(P(cx + Mathf.Sin(a) * rx * 0.25f, cy - ry * thick * 0.97f), P(cx + Mathf.Sin(a) * rx * 0.75f, cy - ry * thick * 0.86f),
                            P(cx + Mathf.Sin(a) * rx * 0.86f, cy - ry * (fringe + 0.1f)), 10), hair.Darkened(0.22f) with { A = 0.45f }, 0.0035f);
                    }
            }
        }
        if (bald >= 0.8f)
            // What is left: a band round the sides.
            foreach (float s in new[] { -1f, 1f })
                Poly(new[] { P(cx + s * rx * 1.04f, cy - ry * 0.3f), P(cx + s * rx * 0.86f, cy - ry * 0.3f), P(cx + s * rx * 0.88f, cy + ry * 0.05f), P(cx + s * rx * 1.02f, cy + ry * 0.08f) }, hair);

        // Locks that come down by the face: bobs, long hair, pigtails.
        if (cut is "long" or "bob" or "curly_bob" or "curly_long" or "perm" or "flick" or "side_long" or "pigtails" or "waves" or "mullet" or "feathered")
        {
            float low = cut switch { "long" or "curly_long" => 0.95f, "perm" => 0.8f, "flick" => 0.6f, "waves" => 0.35f, "mullet" or "feathered" => 0.3f, "pigtails" => 0.4f, _ => 0.6f };
            foreach (float s in new[] { -1f, 1f })
            {
                // A lock that hangs from the temple, fuller in the middle and tapering to a soft point.
                var lockPts = new List<Vector2> { P(cx + s * rx * 1.08f, cy - ry * 0.6f) };
                lockPts.AddRange(Bezier(P(cx + s * rx * 0.86f, cy - ry * 0.35f), P(cx + s * rx * 0.79f, cy + ry * low * 0.55f), P(cx + s * rx * 0.9f, cy + ry * low), 10));
                lockPts.Add(P(cx + s * rx * (cut == "flick" ? 1.35f : 1.03f), cy + ry * (low + (cut == "flick" ? -0.04f : 0.09f))));
                lockPts.AddRange(Bezier(P(cx + s * rx * 1.15f, cy + ry * low * 0.8f), P(cx + s * rx * 1.26f, cy + ry * low * 0.2f), P(cx + s * rx * 1.13f, cy - ry * 0.45f), 10));
                if (s > 0) lockPts.Reverse();
                Poly(lockPts.ToArray(), hair);
                Curve(new List<Vector2>(lockPts) { lockPts[0] }, hair.Darkened(0.4f) with { A = 0.55f }, 0.004f);
            }
        }
        if (cut is "curly_bob" or "curly_long" or "perm" or "short_curls" or "curly_top" or "afro")
        {
            float r = cut == "afro" ? 0.05f : 0.03f;
            int n = cut == "afro" ? 14 : 12;
            for (int i = 0; i <= n; i++)
            {
                float a = -1.75f + 3.5f * i / n;
                var at = P(cx + Mathf.Sin(a) * rx * (thick + 0.02f), cy - Mathf.Cos(a) * ry * (thick + 0.02f));
                DrawCircle(at, r * _s, T(hair));
                DrawCircle(at + new Vector2(-0.006f, -0.006f) * _s, r * 0.4f * _s, T(shine));
            }
        }
        if (cut == "pigtails")
            foreach (float s in new[] { -1f, 1f })
                Poly(Ellipse(P(cx + s * rx * 1.3f, cy + ry * 0.15f), rx * 0.22f * _s, ry * 0.42f * _s, 16), hair);
        if (cut == "bun")
        {
            DrawCircle(P(cx, cy - ry * 1.12f), rx * (_v.Male ? 0.24f : 0.34f) * _s, T(hair));
            DrawCircle(P(cx - rx * 0.1f, cy - ry * 1.2f), rx * 0.1f * _s, T(shine));
        }
        if (cut == "ponytail")
            DrawCircle(P(cx + rx * 0.85f, cy - ry * 0.55f), rx * 0.12f * _s, T(hair.Darkened(0.2f)));

        // A highlight where the light falls on the hair.
        if (bald < 0.6f && cut is not "buzz" and not "afro")
            Curve(Bezier(P(cx - rx * 0.55f, cy - ry * 0.72f), P(cx - rx * 0.32f, cy - ry * 0.9f), P(cx + rx * 0.02f, cy - ry * 0.95f), 10), shine, 0.012f);
    }
}
