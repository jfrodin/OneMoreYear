namespace OneMoreYear.Game;

/// <summary>
/// Who made the game, in one place. Empty lines are left out of the credits and the start screen,
/// so nothing shows until the names are decided.
/// </summary>
public static class Studio
{
    /// <summary>The developer's name in the store and on the start screen (a studio name or a person).</summary>
    public const string Developer = "";

    /// <summary>The person behind it, for the credits ("Made by ...").</summary>
    public const string MadeBy = "";

    /// <summary>People to thank by name in the credits; empty thanks everyone who played early versions.</summary>
    public static readonly string[] Thanks = { };
}
