using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using OneMoreYear.Simulation;

namespace OneMoreYear.Game;

/// <summary>
/// The player's finished families, kept in user://families.json (outside the saves), so the title
/// screen can show them: the last page of each, best first. Automated runs keep nothing.
/// </summary>
public static class FamilyArchive
{
    private const string Path = "user://families.json";

    public sealed class Entry
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public int From { get; set; }
        public int To { get; set; }
        public string Words { get; set; } = "";
        public int Score { get; set; }
        public int Lives { get; set; }
        public string Date { get; set; } = "";
    }

    private static List<Entry>? _data;

    private static bool Automated => Features.Automated;

    private static List<Entry> Data
    {
        get
        {
            if (_data != null) return _data;
            _data = new();
            if (Automated) return _data;
            try
            {
                if (FileAccess.FileExists(Path))
                    _data = JsonSerializer.Deserialize<List<Entry>>(FileAccess.GetFileAsString(Path)) ?? new();
            }
            catch (JsonException)
            {
                _data = new();
            }
            return _data;
        }
    }

    /// <summary>Best first.</summary>
    public static IReadOnlyList<Entry> All => Data.OrderByDescending(e => e.Score).ToList();

    /// <summary>Keeps the family's last page (once per family).</summary>
    public static void Add(GameSession s)
    {
        var e = s.Epilogue();
        string key = $"{s.World.Seed}:{s.World.StartYear}:{s.World.PlayedIds.FirstOrDefault()}";
        Data.RemoveAll(x => x.Key == key);
        Data.Add(new Entry
        {
            Key = key, Name = e.FamilyName, From = e.FromYear, To = e.ToYear, Words = e.Words, Score = e.Score,
            Lives = e.Lives.Count, Date = System.DateTime.Now.ToString("yyyy-MM-dd"),
        });
        if (Automated) return;
        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(Data));
    }
}
