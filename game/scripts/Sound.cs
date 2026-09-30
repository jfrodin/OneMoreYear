using System;
using System.Collections.Generic;
using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// Quiet interface sounds, synthesised in code so the game needs no audio files: a soft click for
/// buttons, paper for pages and the newspaper, a small chime for a new year. They play on an
/// "Effects" bus whose volume follows the settings. (Music is for later, with real recordings.)
/// </summary>
public static class Sound
{
    private const int Rate = 22050;
    private const string Bus = "Effects";
    private static readonly Dictionary<string, AudioStreamWav> Streams = new();
    private static readonly Dictionary<string, AudioStreamPlayer> Players = new();
    private static Node? _root;

    public static void Init(Node root)
    {
        _root = root;
        if (AudioServer.GetBusIndex(Bus) < 0)
        {
            AudioServer.AddBus();
            AudioServer.SetBusName(AudioServer.BusCount - 1, Bus);
        }
        Streams["click"] = Make(0.05, t => Math.Sin(t * 2 * Math.PI * 820) * Math.Exp(-t * 90) * 0.35);
        var rnd = new Random(3);
        Streams["page"] = Make(0.22, t => (rnd.NextDouble() * 2 - 1) * Math.Sin(Math.PI * t / 0.22) * 0.18);
        Streams["paper"] = Make(0.45, t => (rnd.NextDouble() * 2 - 1) * Math.Pow(Math.Sin(Math.PI * t / 0.45), 2) * (0.6 + 0.4 * Math.Sin(t * 60)) * 0.2);
        Streams["year"] = Make(1.2, t => (Math.Sin(t * 2 * Math.PI * 659.3) + 0.6 * Math.Sin(t * 2 * Math.PI * 987.8) + 0.3 * Math.Sin(t * 2 * Math.PI * 1318.5))
                                         * Math.Exp(-t * 3.2) * Math.Min(1, t * 200) * 0.22);
        foreach (var (name, stream) in Streams)
        {
            var player = new AudioStreamPlayer { Stream = stream, Bus = Bus };
            root.AddChild(player);
            Players[name] = player;
        }
        SetEffectsVolume(Settings.EffectsVolume);
    }

    public static void Play(string name)
    {
        if (Players.TryGetValue(name, out var player) && player.IsInsideTree()) player.Play();
    }

    public static void SetEffectsVolume(double volume)
    {
        int bus = AudioServer.GetBusIndex(Bus);
        if (bus < 0) return;
        AudioServer.SetBusMute(bus, volume <= 0.001);
        AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb((float)Math.Max(0.001, volume)));
    }

    /// <summary>16-bit mono samples from a function of time (seconds) returning -1..1.</summary>
    private static AudioStreamWav Make(double seconds, Func<double, double> wave)
    {
        int n = (int)(seconds * Rate);
        var data = new byte[n * 2];
        for (int i = 0; i < n; i++)
        {
            short s = (short)(Math.Clamp(wave((double)i / Rate), -1, 1) * short.MaxValue);
            data[i * 2] = (byte)(s & 0xff);
            data[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = data };
    }
}
