using System;

namespace OneMoreYear.Game;

/// <summary>
/// Tones the music is written with. The game has no sound effects on purpose: music is enough
/// (producer's decision, 2026-10-04). See <see cref="Music"/>.
/// </summary>
public static class Sound
{
    /// <summary>A bell: a few inharmonic partials that die away, the higher ones faster.</summary>
    public static double Bell(double f, double t, double decay, double warmth)
    {
        if (t < 0) return 0;
        double attack = Math.Min(1, t * 400);
        return attack * (Math.Sin(2 * Math.PI * f * t) * Math.Exp(-t * decay)
                         + warmth * 0.5 * Math.Sin(2 * Math.PI * f * 2.0 * t) * Math.Exp(-t * decay * 1.6)
                         + 0.35 * Math.Sin(2 * Math.PI * f * 2.76 * t) * Math.Exp(-t * decay * 2.4)
                         + 0.18 * Math.Sin(2 * Math.PI * f * 5.4 * t) * Math.Exp(-t * decay * 4));
    }
}
