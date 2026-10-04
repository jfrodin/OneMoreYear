using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Game;

/// <summary>When the family newspaper is shown at the start of a year.</summary>
public enum NewspaperMode { EveryYear, BigYears, Never }

/// <summary>
/// Player preferences that are not part of a save: the content settings new games start with,
/// whether the player has been asked about them yet, and when the newspaper appears.
/// Stored in user://settings.json.
/// </summary>
public static class Settings
{
    private const string Path = "user://settings.json";

    private sealed class Data
    {
        public bool ContentAsked { get; set; }
        public Dictionary<string, ContentLevel> Content { get; set; } = new();
        public NewspaperMode Newspaper { get; set; } = NewspaperMode.BigYears;
        public bool IntroSeen { get; set; }
        /// <summary>Tips for new players, and the ones already read.</summary>
        public bool TipsOn { get; set; } = true;
        public List<string> TipsSeen { get; set; } = new();
        public bool Fullscreen { get; set; }
        /// <summary>0 small, 1 normal, 2 large, 3 extra large.</summary>
        public int TextSize { get; set; } = 1;
        /// <summary>A decade whose look is always used; null = the look follows the years.</summary>
        public int? FixedLook { get; set; }
        public double MasterVolume { get; set; } = 0.8;
        public double MusicVolume { get; set; } = 0.5;
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

    public static NewspaperMode Newspaper => Current.Newspaper;

    public static void SetNewspaper(NewspaperMode mode)
    {
        Current.Newspaper = mode;
        Save();
    }

    public static bool IntroSeen => Current.IntroSeen;
    public static void MarkIntroSeen() { Current.IntroSeen = true; Save(); }
    public static bool TipsOn => Current.TipsOn;
    public static bool TipSeen(string id) => Current.TipsSeen.Contains(id);
    public static void MarkTip(string id) { if (!Current.TipsSeen.Contains(id)) Current.TipsSeen.Add(id); Save(); }
    /// <summary>Turning tips back on shows them all again from the start.</summary>
    public static void SetTips(bool on) { Current.TipsOn = on; if (on) Current.TipsSeen.Clear(); Save(); }

    public static bool Fullscreen => Current.Fullscreen;
    public static int TextSize => Current.TextSize;
    public static int? FixedLook => Current.FixedLook;
    public static void SetFixedLook(int? year) { Current.FixedLook = year; Save(); }
    public static double MasterVolume => Current.MasterVolume;
    public static double MusicVolume => Current.MusicVolume;

    public static void SetFullscreen(bool on) { Current.Fullscreen = on; Save(); Apply(); }
    public static void SetTextSize(int size) { Current.TextSize = Math.Clamp(size, 0, 3); Save(); Apply(); }
    public static void SetMasterVolume(double v) { Current.MasterVolume = Math.Clamp(v, 0, 1); Save(); Apply(); }
    public static void SetMusicVolume(double v) { Current.MusicVolume = Math.Clamp(v, 0, 1); Save(); Apply(); }

    /// <summary>Puts the display and sound settings into effect.</summary>
    public static void Apply()
    {
        if (DisplayServer.GetName() != "headless")
            DisplayServer.WindowSetMode(Current.Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        if (Engine.GetMainLoop() is SceneTree tree)
            tree.Root.ContentScaleFactor = Current.TextSize switch { 0 => 0.9f, 2 => 1.12f, 3 => 1.25f, _ => 1f };
        AudioServer.SetBusVolumeDb(0, (float)Mathf.LinearToDb(Current.MasterVolume));
        Music.SetVolume(Current.MusicVolume);
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
