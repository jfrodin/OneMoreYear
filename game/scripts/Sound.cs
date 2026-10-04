using System;
using System.Collections.Generic;
using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// Quiet interface sounds, synthesised in code so the game needs no audio files: a soft click for
/// buttons, paper for pages and the newspaper, a small chime for a new year. They play on an
/// "Effects" bus whose volume follows the settings. The big moments (a birth, a death, a wedding, a
/// graduation) have their own. Music is in <see cref="Music"/>.
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
        // The big moments of a life, each short and soft: they should be felt more than heard.
        // A music box for a birth.
        Streams["birth"] = Make(2.4, t => Notes(t, 0.22, new[] { 1046.5, 1318.5, 1568.0, 2093.0 }, (f, u) => Bell(f, u, 2.6, 0.5)) * 0.16);
        // One low bell, far away, for a death.
        Streams["death"] = Make(4.5, t => Bell(174.6, t, 0.9, 0.9) * 0.26);
        // Two bells and a little peal for a wedding.
        Streams["wedding"] = Make(2.8, t => Notes(t, 0.16, new[] { 784.0, 987.8, 1174.7, 1568.0, 1174.7, 1568.0 }, (f, u) => Bell(f, u, 2.2, 0.6)) * 0.13);
        // A few rising notes for a graduation.
        Streams["graduation"] = Make(2.0, t => Notes(t, 0.18, new[] { 523.3, 659.3, 784.0, 1046.5 }, (f, u) => Soft(f, u, 1.6)) * 0.16);
        foreach (var (name, stream) in Streams)
        {
            var player = new AudioStreamPlayer { Stream = stream, Bus = Bus };
            root.AddChild(player);
            Players[name] = player;
        }
        SetEffectsVolume(Settings.EffectsVolume);
    }

    public static void Export(string dir)
    {
        foreach (var (name, stream) in Streams) stream.SaveToWav($"{dir}/sound_{name}.wav");
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

    /// <summary>A soft, round tone (a few harmonics, a gentle attack).</summary>
    public static double Soft(double f, double t, double decay)
    {
        if (t < 0) return 0;
        double env = Math.Min(1, t * 60) * Math.Exp(-t * decay);
        return env * (Math.Sin(2 * Math.PI * f * t) + 0.3 * Math.Sin(2 * Math.PI * f * 2 * t) + 0.1 * Math.Sin(2 * Math.PI * f * 3 * t));
    }

    /// <summary>Notes one after another, each ringing on under the next.</summary>
    private static double Notes(double t, double step, double[] freqs, Func<double, double, double> voice)
    {
        double sum = 0;
        for (int i = 0; i < freqs.Length; i++) sum += voice(freqs[i], t - i * step);
        return sum;
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
