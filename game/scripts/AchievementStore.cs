using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// The player's achievements, kept across all families in user://achievements.json (not in the saves).
/// Automated runs (smoke test, screenshots) keep theirs in memory only.
/// </summary>
public static class AchievementStore
{
    private const string Path = "user://achievements.json";

    public sealed class Earned
    {
        public string Family { get; set; } = "";
        public int Year { get; set; }
        public string Date { get; set; } = "";
    }

    private static Dictionary<string, Earned>? _data;

    private static bool Automated => System.Linq.Enumerable.Any(OS.GetCmdlineUserArgs(), a => a == "--smoke" || a.StartsWith("--screenshots="));

    private static Dictionary<string, Earned> Data
    {
        get
        {
            if (_data != null) return _data;
            _data = new();
            if (Automated) return _data;
            try
            {
                if (FileAccess.FileExists(Path))
                    _data = JsonSerializer.Deserialize<Dictionary<string, Earned>>(FileAccess.GetFileAsString(Path)) ?? new();
            }
            catch (JsonException)
            {
                _data = new();
            }
            return _data;
        }
    }

    public static IReadOnlyDictionary<string, Earned> All => Data;

    public static HashSet<string> Ids => new(Data.Keys);

    public static void Add(string id, string family, int year)
    {
        if (Data.ContainsKey(id)) return;
        Data[id] = new Earned { Family = family, Year = year, Date = System.DateTime.Now.ToString("yyyy-MM-dd") };
        if (Automated) return;
        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(Data));
    }
}
