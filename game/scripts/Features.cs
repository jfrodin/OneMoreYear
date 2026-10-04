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
}
