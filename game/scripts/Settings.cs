using System.Collections.Generic;
using System.Text.Json;
using Godot;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Game;

/// <summary>
/// Player preferences that are not part of a save: the content settings new games start with, and
/// whether the player has been asked about them yet. Stored in user://settings.json.
/// </summary>
public static class Settings
{
    private const string Path = "user://settings.json";

    private sealed class Data
    {
        public bool ContentAsked { get; set; }
        public Dictionary<string, ContentLevel> Content { get; set; } = new();
    }

    private static Data? _data;

    private static Data Current
    {
        get
        {
            if (_data != null) return _data;
            try
            {
                _data = FileAccess.FileExists(Path)
                    ? JsonSerializer.Deserialize<Data>(FileAccess.GetFileAsString(Path)) ?? new Data()
                    : new Data();
            }
            catch (JsonException)
            {
                _data = new Data();
            }
            return _data;
        }
    }

    /// <summary>True until the player has seen the content settings once.</summary>
    public static bool ShouldAskAboutContent => !Current.ContentAsked;

    public static IReadOnlyDictionary<string, ContentLevel> Content => Current.Content;

    public static ContentLevel ContentLevelOf(string category) => Current.Content.GetValueOrDefault(category, ContentLevel.On);

    public static void SetContentLevel(string category, ContentLevel level)
    {
        if (level == ContentLevel.On) Current.Content.Remove(category);
        else Current.Content[category] = level;
        Save();
    }

    public static void MarkContentAsked()
    {
        Current.ContentAsked = true;
        Save();
    }

    private static void Save()
    {
        // Automated runs must not change the player's preferences.
        if (System.Linq.Enumerable.Any(OS.GetCmdlineUserArgs(), a => a == "--smoke" || a.StartsWith("--screenshots="))) return;
        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(Current));
    }
}
