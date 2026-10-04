using System.Collections.Generic;
using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// Simple line icons, drawn as SVG in code and coloured to the current look: the tabs and the
/// values (health, happiness and the rest). Kept plain on purpose, like a pencil sketch in a margin.
/// </summary>
public static class Icons
{
    // 24 x 24, drawn with a 2 px line. {0} is the colour.
    private static readonly Dictionary<string, string> Shapes = new()
    {
        ["year"] = "<rect x='3' y='5' width='18' height='16' rx='2'/><path d='M3 10h18M8 3v4M16 3v4'/>",
        ["people"] = "<circle cx='9' cy='8' r='3'/><path d='M3 20c0-3.3 2.7-6 6-6s6 2.7 6 6'/><circle cx='17' cy='9' r='2.5'/><path d='M15.5 14.2c3 .2 5.5 2.6 5.5 5.8'/>",
        ["work"] = "<rect x='3' y='7' width='18' height='13' rx='2'/><path d='M9 7V5a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v2M3 13h18'/>",
        ["money"] = "<circle cx='12' cy='12' r='9'/><path d='M14.5 9.5a2.5 2 0 0 0-2.5-1.5c-1.5 0-2.5.8-2.5 2s1 1.7 2.5 2 2.5.8 2.5 2-1 2-2.5 2a2.5 2 0 0 1-2.5-1.5M12 6v2M12 16v2'/>",
        ["tree"] = "<circle cx='12' cy='5' r='2.2'/><circle cx='6' cy='19' r='2.2'/><circle cx='18' cy='19' r='2.2'/><path d='M12 7.2V12M6 16.8V12h12v4.8'/>",
        ["time"] = "<circle cx='12' cy='12' r='9'/><path d='M12 7v5l3 2'/>",
        ["chronicle"] = "<path d='M12 6c-2-1.5-5-2-9-2v14c4 0 7 .5 9 2 2-1.5 5-2 9-2V4c-4 0-7 .5-9 2zM12 6v14'/>",
        ["health"] = "<path d='M12 20s-7-4.5-9-9a4.6 4.6 0 0 1 9-3 4.6 4.6 0 0 1 9 3c-2 4.5-9 9-9 9z'/>",
        ["happiness"] = "<circle cx='12' cy='12' r='9'/><path d='M8 14a4.5 4.5 0 0 0 8 0'/><circle cx='9' cy='10' r='.6'/><circle cx='15' cy='10' r='.6'/>",
        ["smarts"] = "<path d='M9 18h6M10 21h4M12 3a6 6 0 0 0-3.5 10.9c.7.5 1 1.2 1 2.1h5c0-.9.3-1.6 1-2.1A6 6 0 0 0 12 3z'/>",
        ["looks"] = "<path d='M12 3l1.8 6.2L20 11l-6.2 1.8L12 19l-1.8-6.2L4 11l6.2-1.8z'/>",
        ["fitness"] = "<path d='M6 7v10M18 7v10M3 9.5v5M21 9.5v5M6 12h12'/>",
    };

    private static readonly Dictionary<string, Texture2D> Cache = new();

    /// <summary>The icon in a colour, at a size in pixels (sharp at any text size).</summary>
    public static Texture2D? Get(string name, Color color, int size = 20)
    {
        if (!Shapes.TryGetValue(name, out var shape)) return null;
        string hex = color.ToHtml(false);
        string key = $"{name}:{hex}:{size}";
        if (Cache.TryGetValue(key, out var cached)) return cached;
        string svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24' viewBox='0 0 24 24' fill='none' stroke='#{hex}' " +
                     $"stroke-width='2' stroke-linecap='round' stroke-linejoin='round'>{shape}</svg>";
        var image = new Image();
        if (image.LoadSvgFromString(svg, size / 24f) != Error.Ok) return null;
        var texture = ImageTexture.CreateFromImage(image);
        Cache[key] = texture;
        return texture;
    }

    /// <summary>The icon as a control, centred, for rows and labels.</summary>
    public static Control? Rect(string name, Color color, int size = 20)
    {
        if (Get(name, color, size) is not { } texture) return null;
        return new TextureRect
        {
            Texture = texture, StretchMode = TextureRect.StretchModeEnum.KeepCentered,
            CustomMinimumSize = new Vector2(size, size), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
    }
}
