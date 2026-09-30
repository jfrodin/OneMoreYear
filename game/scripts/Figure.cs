using Godot;
using OneMoreYear.Simulation;

namespace OneMoreYear.Game;

/// <summary>
/// A simple full-body figure, drawn to scale: the control's height is 200 cm, so a child is small
/// next to an adult. Build and weight change the width; children have bigger heads.
/// </summary>
public partial class Figure : Control
{
    private PortraitView _v = null!;

    public static Figure Create(PortraitView view, float height)
    {
        var f = new Figure { CustomMinimumSize = new Vector2(height * 0.42f, height), MouseFilter = MouseFilterEnum.Ignore };
        f._v = view;
        return f;
    }

    private Color T(Color c)
    {
        if (_v.Alive) return c;
        float l = c.R * 0.3f + c.G * 0.59f + c.B * 0.11f;
        return new Color(l, l, l, c.A).Darkened(0.1f);
    }

    public override void _Draw()
    {
        float scale = Size.Y / 200f;            // pixels per cm
        float h = _v.HeightCm * scale;
        float ground = Size.Y - 2;
        float cx = Size.X / 2;
        int age = _v.Age;
        float heavy = (float)_v.Heaviness;

        // Head share of body height: about a quarter for toddlers, an eighth for adults.
        float headShare = age < 2 ? 0.26f : age < 6 ? 0.2f : age < 13 ? 0.16f : 0.135f;
        float head = h * headShare;
        float body = h - head;
        float width = h * (age < 13 ? 0.3f : 0.28f) * (0.8f + heavy * 0.5f);

        var skin = Portrait.SkinColor(_v.Face.Skin);
        var hair = Portrait.HairColor(_v.HairColor, _v.Grey, age);
        var shirt = Portrait.ShirtColor(_v.Id);
        var trousers = new Color("2f3440");

        float top = ground - h;
        float shoulders = top + head * 1.05f;
        float hips = shoulders + body * 0.4f;
        float legW = width * 0.22f;

        // Legs and shoes.
        foreach (float side in new[] { -1f, 1f })
        {
            var leg = new Rect2(cx + side * width * 0.24f - legW / 2, hips, legW, ground - hips);
            DrawRect(leg, T(trousers));
            DrawRect(new Rect2(leg.Position.X - legW * 0.1f, ground - legW * 0.45f, legW * 1.3f, legW * 0.45f), T(new Color("1c1c1c")));
        }
        // Arms.
        foreach (float side in new[] { -1f, 1f })
        {
            var arm = new[]
            {
                new Vector2(cx + side * width * 0.46f, shoulders + 2),
                new Vector2(cx + side * width * 0.62f, shoulders + 4),
                new Vector2(cx + side * width * 0.58f, hips + body * 0.12f),
                new Vector2(cx + side * width * 0.45f, hips + body * 0.12f),
            };
            DrawColoredPolygon(arm, T(shirt.Darkened(0.12f)));
            DrawCircle(new Vector2(cx + side * width * 0.52f, hips + body * 0.14f), legW * 0.45f, T(skin));
        }
        // Torso.
        DrawColoredPolygon(new[]
        {
            new Vector2(cx - width * 0.5f, shoulders), new Vector2(cx + width * 0.5f, shoulders),
            new Vector2(cx + width * 0.42f, hips + 2), new Vector2(cx - width * 0.42f, hips + 2),
        }, T(shirt));
        // Neck, head, hair.
        DrawRect(new Rect2(cx - head * 0.14f, shoulders - head * 0.15f, head * 0.28f, head * 0.2f), T(skin.Darkened(0.15f)));
        var headCenter = new Vector2(cx, top + head * 0.45f);
        if (!_v.Male && age >= 2 && _v.Face.HairStyle is 0 or 1 && _v.Bald < 0.5)
            DrawRect(new Rect2(cx - head * 0.5f, headCenter.Y - head * 0.1f, head, head * 0.75f), T(hair.Darkened(0.1f)));
        DrawCircle(headCenter, head * 0.45f, T(skin));
        if (_v.Bald < 0.8)
            DrawArc(headCenter, head * 0.4f, Mathf.Pi * 1.08f, Mathf.Pi * 1.92f, 16, T(hair), head * 0.18f, true);
    }
}
