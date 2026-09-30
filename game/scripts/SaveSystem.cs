using Godot;
using OneMoreYear.Simulation;

namespace OneMoreYear.Game;

/// <summary>A single autosave slot in the user data folder.</summary>
public static class SaveSystem
{
    private const string Path = "user://save.json";

    public static bool HasSave => FileAccess.FileExists(Path);

    public static void Save(GameSession session)
    {
        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        file?.StoreString(session.Save());
    }

    public static GameSession? Load()
    {
        if (!HasSave) return null;
        try
        {
            return GameSession.Load(FileAccess.GetFileAsString(Path));
        }
        catch (System.Exception e)
        {
            GD.PushError($"Could not load save: {e.Message}");
            return null;
        }
    }

    public static void Delete()
    {
        if (HasSave) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(Path));
    }
}
