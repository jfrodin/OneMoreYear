using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// A placeholder portrait: a coloured disc with initials. Colour is stable per person;
/// the dead are shown in grey, the player gets a gold ring.
/// </summary>
public partial class Portrait : Control
{
    private static readonly Color[] Palette =
    {
        new("5b7aa6"), new("a6665b"), new("6b9a6e"), new("8f6ba6"), new("a68f5b"),
        new("5b9aa6"), new("a65b85"), new("7a8a5b"), new("5b64a6"), new("a67a5b"),
    };

    private string _initials = "";
    private Color _color;
    private bool _highlight;

    public static Portrait Create(int id, string name, bool alive, bool highlight, float size)
    {
        var p = new Portrait { CustomMinimumSize = new Vector2(size, size), MouseFilter = MouseFilterEnum.Ignore };
        p.Setup(id, name, alive, highlight);
        return p;
    }

    public void Setup(int id, string name, bool alive, bool highlight)
    {
        var parts = name.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        _initials = parts.Length switch
        {
            0 => "?",
            1 => parts[0].Substring(0, 1),
            _ => parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)
        };
        _color = alive ? Palette[id % Palette.Length] : new Color("4a4a4a");
        _highlight = highlight;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float r = Mathf.Min(Size.X, Size.Y) / 2f;
        var c = Size / 2f;
        DrawCircle(c, r, _color.Darkened(0.35f));
        DrawCircle(c, r - 3, _color);
        if (_highlight) DrawArc(c, r - 1.5f, 0, Mathf.Tau, 64, UiTheme.Accent, 3, true);
        var font = ThemeDB.FallbackFont;
        int fontSize = (int)(r * 0.8f);
        var textSize = font.GetStringSize(_initials, HorizontalAlignment.Center, -1, fontSize);
        var pos = new Vector2(c.X - textSize.X / 2f, c.Y + fontSize * 0.35f);
        DrawString(font, pos, _initials, HorizontalAlignment.Left, -1, fontSize, new Color(1, 1, 1, 0.92f));
    }
}
