using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// Parts of the game that can be switched on and off as a whole. One place to decide, so a feature
/// can be a playtesting tool now and something else (an add-on, say) later.
/// </summary>
public static class Features
{
    /// <summary>
    /// The character creator: start any life, anywhere, at any age. On in development builds and
    /// in builds exported with the "creator" feature tag. Later this is where an add-on check goes.
    /// </summary>
    public static bool CharacterCreator => OS.IsDebugBuild() || OS.HasFeature("creator");

    /// <summary>F1 playtest notes with a screenshot and a save: development builds and the playtest build.</summary>
    public static bool Feedback => OS.IsDebugBuild() || OS.HasFeature("playtest");

    /// <summary>
    /// The tools on the command line (--load, --smoke, --screenshots, --country and the image makers):
    /// development builds and the playtest build. A release build ignores its command line.
    /// </summary>
    public static bool DevTools => OS.IsDebugBuild() || OS.HasFeature("playtest");

    /// <summary>The command line, empty in a release build.</summary>
    public static string[] Args => DevTools ? OS.GetCmdlineUserArgs() : System.Array.Empty<string>();

    /// <summary>An automated run (--smoke or --screenshots): silent, and saves nothing of the player's.</summary>
    public static bool Automated => System.Array.Exists(Args, a => a == "--smoke" || a.StartsWith("--screenshots="));
}
