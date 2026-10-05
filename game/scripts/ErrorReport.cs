using System;
using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// When something goes wrong that should not: the details go to a log file in the user folder, and
/// the player gets a calm message instead of a frozen screen. The last save is untouched, since the
/// game only saves after a year or a choice has gone through.
/// </summary>
public static class ErrorReport
{
    public const string LogPath = "user://errors.log";

    /// <summary>Runs <paramref name="work"/>; if it throws, writes it down and tells the player. Returns false then.</summary>
    public static bool Try(Main main, string what, Action work)
    {
        try
        {
            work();
            return true;
        }
        catch (Exception e)
        {
            Write(what, e);
            // What was half done is thrown away: back to the last save, which was made before it began.
            main.LoadSlot(SaveSystem.CurrentSlot);
            main.ShowMessage("Something went wrong",
                "The album could not do that just now. Your last save is safe. If it happens again, the details are in " +
                $"{ProjectSettings.GlobalizePath(LogPath)}, which helps when you report it.");
            return false;
        }
    }

    public static void Write(string what, Exception e)
    {
        GD.PushError($"{what}: {e}");
        try
        {
            string path = ProjectSettings.GlobalizePath(LogPath);
            System.IO.File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  version {Main.Version}  {what}{System.Environment.NewLine}{e}{System.Environment.NewLine}{System.Environment.NewLine}");
        }
        catch
        {
            // Nowhere to write it: the message on screen is all there is.
        }
    }
}
