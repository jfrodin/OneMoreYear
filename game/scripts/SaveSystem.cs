using System;
using System.Linq;
using System.Text.Json;
using Godot;
using OneMoreYear.Simulation;

namespace OneMoreYear.Game;

/// <summary>A save slot's summary, kept next to the save so the slot list loads instantly.</summary>
public sealed record SlotInfo(int Slot, string Family, string Player, int Age, int Year, string SeedCode, DateTime SavedAt, bool GameOver);

/// <summary>
/// Three save slots in the user data folder. Each game lives in one slot and autosaves there; a
/// small summary file per slot lets the menus show what is in it without loading the whole world.
/// </summary>
public static class SaveSystem
{
    public const int Slots = 30;

    // Automated runs (smoke test, screenshot tour) get their own slot so they never touch the player's saves.
    private static readonly bool Automated =
        OS.GetCmdlineUserArgs().Any(a => a == "--smoke" || a.StartsWith("--screenshots="));

    /// <summary>The slot the current game saves to.</summary>
    public static int CurrentSlot { get; set; } = 1;

    private static string SavePath(int slot) => Automated ? "user://test_save.json" : $"user://save_{slot}.json";
    private static string InfoPath(int slot) => Automated ? "user://test_save.info.json" : $"user://save_{slot}.info.json";

    static SaveSystem()
    {
        // Before 0.22 there was a single save: it becomes slot 1.
        if (!Automated && FileAccess.FileExists("user://save.json") && !FileAccess.FileExists(SavePath(1)))
            DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath("user://save.json"), ProjectSettings.GlobalizePath(SavePath(1)));
    }

    public static bool HasSave => Enumerable.Range(1, Slots).Any(s => FileAccess.FileExists(SavePath(s)));

    public static bool IsEmpty(int slot) => !FileAccess.FileExists(SavePath(slot));

    public static int? FirstEmptySlot() => Enumerable.Range(1, Slots).Cast<int?>().FirstOrDefault(s => IsEmpty(s!.Value));

    public static void Save(GameSession session)
    {
        using (var file = FileAccess.Open(SavePath(CurrentSlot), FileAccess.ModeFlags.Write))
            file?.StoreString(session.Save());
        var p = session.Player;
        var info = new SlotInfo(CurrentSlot, session.World.FamilyName, p.FullName, p.Age(session.Year), session.Year, session.SeedCode,
            DateTime.Now, session.GameOver);
        using var infoFile = FileAccess.Open(InfoPath(CurrentSlot), FileAccess.ModeFlags.Write);
        infoFile?.StoreString(JsonSerializer.Serialize(info));
    }

    public static SlotInfo? Info(int slot)
    {
        if (IsEmpty(slot)) return null;
        try
        {
            if (FileAccess.FileExists(InfoPath(slot)))
                return JsonSerializer.Deserialize<SlotInfo>(FileAccess.GetFileAsString(InfoPath(slot)));
        }
        catch (JsonException) { }
        // A save from before slots had summaries: describe it from the save itself.
        var s = Load(slot);
        return s == null ? null
            : new SlotInfo(slot, s.World.FamilyName, s.Player.FullName, s.Player.Age(s.Year), s.Year, s.SeedCode, DateTime.MinValue, s.GameOver);
    }

    /// <summary>The slot saved to most recently – what "Continue" opens.</summary>
    public static int? LatestSlot() =>
        Enumerable.Range(1, Slots).Where(s => !IsEmpty(s))
            .OrderByDescending(s => FileAccess.GetModifiedTime(SavePath(s))).Cast<int?>().FirstOrDefault();

    public static GameSession? Load(int slot)
    {
        if (IsEmpty(slot)) return null;
        try
        {
            return GameSession.Load(FileAccess.GetFileAsString(SavePath(slot)));
        }
        catch (Exception e)
        {
            GD.PushError($"Could not load slot {slot}: {e.Message}");
            return null;
        }
    }

    public static void Delete(int slot)
    {
        foreach (var path in new[] { SavePath(slot), InfoPath(slot) })
            if (FileAccess.FileExists(path)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
    }
}
