using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace OneMoreYear.Game;

/// <summary>
/// Quiet music that follows the decades: a short piece written in code for each era (a soft piano
/// in the fifties, a guitar in the sixties, an electric piano in the seventies, synths in the
/// eighties, and so on), with a short pause between pieces. Made here so the game needs no audio
/// files; real recordings can replace it later without changing anything else.
/// </summary>
public static class Music
{
    private const int Rate = 22050;
    private const string Bus = "Music";
    private static AudioStreamPlayer? _player;
    private static Node? _root;
    private static readonly Dictionary<int, AudioStreamWav> Tracks = new();
    private static readonly HashSet<int> Making = new();
    private static int _decade = 1970;
    private static int _played;
    private static readonly Random Pause = new();

    public static void Init(Node root)
    {
        _root = root;
        if (AudioServer.GetBusIndex(Bus) < 0)
        {
            AudioServer.AddBus();
            AudioServer.SetBusName(AudioServer.BusCount - 1, Bus);
        }
        _player = new AudioStreamPlayer { Bus = Bus };
        root.AddChild(_player);
        // A short breath between pieces, so the music returns before the silence is noticed.
        _player.Finished += () => Later(8 + Pause.Next(12));
        SetVolume(Settings.MusicVolume);
        // Automated runs stay silent.
        _silent = Features.Automated;
        if (_silent) return;
        Later(3);
    }

    /// <summary>The year being played: the next piece is from its decade.</summary>
    public static void SetYear(int year) => _decade = Math.Clamp(year / 10 * 10, 1950, 2100);

    public static void SetVolume(double volume)
    {
        int bus = AudioServer.GetBusIndex(Bus);
        if (bus < 0) return;
        AudioServer.SetBusMute(bus, volume <= 0.001);
        AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb((float)Math.Max(0.001, volume)));
    }

    /// <summary>Writes each decade's piece to DIR/music_1950.wav and so on.</summary>
    public static void Export(string dir)
    {
        for (int decade = 1950; decade <= 2060; decade += 10)
            new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = Compose(decade) }
                .SaveToWav($"{dir}/music_{decade}.wav");
    }

    private static string? _place;
    private static bool _silent;
    /// <summary>Counts the changes of place, so a timer set before a change does not start a second piece.</summary>
    private static int _generation;
    private static Tween? _fade;

    /// <summary>
    /// A screen with music of its own ("title", "memoriam"); null plays the decade's. The piece that is
    /// playing fades out, and the new one begins after a breath.
    /// </summary>
    public static void SetPlace(string? place)
    {
        if (_place == place) return;
        _place = place;
        if (_player == null || _root == null || _silent) return;
        int generation = ++_generation;
        _fade?.Kill();
        _player.VolumeDb = 0;
        if (!_player.Playing) { Later(1.5, generation); return; }
        _fade = _root.CreateTween();
        _fade.TweenProperty(_player, "volume_db", -40f, 1.8);
        _fade.TweenCallback(Callable.From(() =>
        {
            if (generation != _generation) return;
            _player.Stop();
            _player.VolumeDb = 0;
            Later(1.5, generation);
        }));
    }

    private static string DecadeName(int decade) => decade is >= 1950 and < 2030 ? $"{decade}s" : decade < 1950 ? "1950s" : "future";

    private static readonly Dictionary<string, AudioStream?> RecordedCache = new();

    private static AudioStream? Recorded(string name)
    {
        if (RecordedCache.TryGetValue(name, out var cached)) return cached;
        string path = $"res://music/{name}.ogg";
        var stream = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
        RecordedCache[name] = stream;
        return stream;
    }

    /// <summary>The next piece after a pause, unless the place has changed (or music started) since.</summary>
    private static void Later(double seconds, int? generation = null)
    {
        int gen = generation ?? _generation;
        if (_root?.GetTree() is { } tree)
            tree.CreateTimer(seconds).Timeout += () => { if (gen == _generation && _player is { Playing: false }) PlayNext(); };
    }

    private static void PlayNext()
    {
        if (_player == null || _player.Playing) return;
        if (Settings.MusicVolume <= 0.001) { Later(30); return; }
        // A recorded piece, when there is one (docs/music/brief.md): res://music/title.ogg, 1970s.ogg and so on.
        if (Recorded(_place ?? DecadeName(_decade)) is { } recorded)
        {
            _player.Stream = recorded;
            _player.Play();
            return;
        }
        // Three pieces per decade, in turn, so the same one is not heard twice in a row.
        int decade = _decade, key = _decade * 10 + _played % 3;
        if (Tracks.TryGetValue(key, out var track))
        {
            _player.Stream = track;
            _player.Play();
            _played++;
            return;
        }
        // Written in the background, played as soon as it is ready.
        if (Making.Add(key))
            Task.Run(() =>
            {
                var data = Compose(decade, key % 10);
                Callable.From(() =>
                {
                    Tracks[key] = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = data };
                    Making.Remove(key);
                    PlayNext();
                }).CallDeferred();
            });
    }

    // --- Writing a piece --------------------------------------------------------------------------

    private enum Voice { Piano, Guitar, EPiano, Synth, Pad, Bell }

    private sealed record Style(int Root, bool Minor, double Bpm, Voice Lead, Voice Chords, bool Pad);

    /// <summary>Each decade's sound: key, tempo and instruments.</summary>
    private static Style StyleFor(int decade) => decade switch
    {
        1950 => new(53, false, 64, Voice.Piano, Voice.Piano, false),
        1960 => new(55, false, 70, Voice.Guitar, Voice.Guitar, false),
        1970 => new(50, false, 66, Voice.EPiano, Voice.EPiano, false),
        1980 => new(57, true, 72, Voice.Synth, Voice.Synth, true),
        1990 => new(52, true, 70, Voice.Piano, Voice.Synth, true),
        2000 => new(48, false, 62, Voice.Piano, Voice.Piano, false),
        2010 => new(58, false, 60, Voice.Piano, Voice.Piano, true),
        2020 => new(53, false, 62, Voice.Guitar, Voice.Piano, true),
        _ => new(48 + decade / 10 % 7, decade / 10 % 3 == 0, 58, Voice.Bell, Voice.Pad, true),
    };

    private static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };
    private static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };
    private static readonly int[][] Progressions =
    {
        new[] { 0, 4, 5, 3 }, new[] { 0, 5, 3, 4 }, new[] { 5, 3, 0, 4 }, new[] { 0, 3, 5, 4 }, new[] { 1, 4, 0, 5 }, new[] { 0, 2, 3, 4 },
    };

    private sealed record Note(double Start, double Length, double Freq, double Velocity, Voice Voice);

    private static double Hz(int midi) => 440 * Math.Pow(2, (midi - 69) / 12.0);

    /// <summary>About a minute and a half of music for a decade, as 16-bit mono samples.</summary>
    private static byte[] Compose(int decade, int variant = 0)
    {
        var style = StyleFor(decade);
        var rng = new Random(decade * 31 + 7 + variant * 1009);
        var scale = style.Minor ? Minor : Major;
        int Degree(int d, int octave) => style.Root + 12 * octave + scale[((d % 7) + 7) % 7] + 12 * (int)Math.Floor(d / 7.0);
        double beat = 60 / style.Bpm, bar = beat * 4;
        var a = Progressions[rng.Next(Progressions.Length)];
        var b = Progressions[rng.Next(Progressions.Length)];
        // A A B A, then the first chord once more to end on.
        var form = new List<(int[] Chords, bool Melody)> { (a, false), (a, true), (b, true), (a, true) };
        var notes = new List<Note>();
        double time = 0.6;
        int melody = 4; // scale degree, an octave up

        foreach (var (chords, withMelody) in form)
            foreach (int chord in chords)
            {
                // Bass: the root, held for the bar.
                notes.Add(new Note(time, bar * 0.95, Hz(Degree(chord, -1)), 0.55, style.Chords == Voice.Synth ? Voice.Pad : style.Chords));
                if (style.Pad)
                    foreach (int d in new[] { 0, 2, 4 })
                        notes.Add(new Note(time, bar, Hz(Degree(chord + d, 0)), 0.18, Voice.Pad));
                // Broken chords in eighths, some left out so it breathes.
                int[] shape = { 0, 2, 4, 7, 4, 2, 4, 2 };
                for (int i = 0; i < 8; i++)
                    if (rng.NextDouble() < (withMelody ? 0.55 : 0.8))
                        notes.Add(new Note(time + i * beat / 2, beat * 1.5, Hz(Degree(chord + shape[i], 0)), 0.32 + rng.NextDouble() * 0.12, style.Chords));
                // A simple tune on top, mostly steps, resting now and then.
                if (withMelody)
                    for (int i = 0; i < 4; i++)
                    {
                        if (rng.NextDouble() < 0.35) continue;
                        melody += rng.Next(-2, 3);
                        // Lean towards the chord's own notes on the first beat.
                        if (i == 0) melody = chord + new[] { 0, 2, 4 }[rng.Next(3)] + 7;
                        melody = Math.Clamp(melody, 4, 13);
                        double length = rng.NextDouble() < 0.3 ? beat * 2 : beat;
                        notes.Add(new Note(time + i * beat, length * 1.2, Hz(Degree(melody, 1)), 0.42, style.Lead));
                    }
                time += bar;
            }
        notes.Add(new Note(time, bar * 2, Hz(Degree(a[0], -1)), 0.5, style.Chords == Voice.Synth ? Voice.Pad : style.Chords));
        foreach (int d in new[] { 0, 2, 4, 7 })
            notes.Add(new Note(time + d * 0.05, bar * 2, Hz(Degree(a[0] + d, 0)), 0.3, style.Chords));
        time += bar * 2.5;

        int n = (int)(time * Rate);
        var buffer = new float[n];
        foreach (var note in notes) Render(buffer, note);
        return Finish(buffer);
    }

    private static void Render(float[] buffer, Note note)
    {
        int start = (int)(note.Start * Rate);
        double release = note.Voice is Voice.Pad or Voice.Synth ? 1.2 : 2.5;
        int length = (int)((note.Length + release) * Rate);
        double f = note.Freq;
        for (int i = 0; i < length && start + i < buffer.Length; i++)
        {
            double t = (double)i / Rate;
            // Held voices fade out after their length; struck ones simply die away.
            double held = t < note.Length ? 1 : Math.Exp(-(t - note.Length) * 4);
            double w = 2 * Math.PI * f * t;
            double v = note.Voice switch
            {
                Voice.Piano => Math.Min(1, t * 300) * Math.Exp(-t * (1.1 + f / 900)) * held
                               * (Math.Sin(w) + 0.45 * Math.Sin(2 * w) * Math.Exp(-t * 2) + 0.2 * Math.Sin(3 * w) * Math.Exp(-t * 3) + 0.08 * Math.Sin(4.01 * w) * Math.Exp(-t * 4)),
                Voice.Guitar => Math.Min(1, t * 500) * held
                                * (Math.Sin(w) * Math.Exp(-t * 1.6) + 0.5 * Math.Sin(2 * w) * Math.Exp(-t * 3) + 0.3 * Math.Sin(3 * w) * Math.Exp(-t * 4.5) + 0.15 * Math.Sin(4 * w) * Math.Exp(-t * 6)),
                Voice.EPiano => Math.Min(1, t * 200) * Math.Exp(-t * 1.0) * held * Math.Sin(w + 1.4 * Math.Exp(-t * 3) * Math.Sin(w)),
                Voice.Synth => Math.Min(1, t * 20) * held * Math.Exp(-t * 0.6)
                               * (Math.Sin(w) + 0.5 * Math.Sin(2 * w * 1.003) + 0.33 * Math.Sin(3 * w) + 0.25 * Math.Sin(4 * w * 0.998)) * 0.6,
                Voice.Pad => Math.Min(1, t * 1.5) * held * (Math.Sin(w * (1 + 0.002 * Math.Sin(t * 5))) + 0.3 * Math.Sin(2 * w * 1.004) + 0.2 * Math.Sin(w * 0.997)),
                _ => Sound.Bell(f, t, 1.4, 0.4) * held,
            };
            buffer[start + i] += (float)(v * note.Velocity);
        }
    }

    /// <summary>Warmth (a gentle low pass), a little room, a fade at each end, and a quiet level.</summary>
    private static byte[] Finish(float[] x)
    {
        int n = x.Length;
        float lp = 0;
        for (int i = 0; i < n; i++) { lp += 0.35f * (x[i] - lp); x[i] = lp; }
        var wet = new float[n];
        foreach (var (delay, feedback) in new[] { (0.0297, 0.55), (0.0371, 0.52), (0.0411, 0.5), (0.0437, 0.48) })
        {
            int d = (int)(delay * Rate);
            var comb = new float[n];
            for (int i = 0; i < n; i++) comb[i] = x[i] + (i >= d ? (float)feedback * comb[i - d] : 0);
            for (int i = 0; i < n; i++) wet[i] += comb[i] * 0.25f;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) { x[i] = x[i] * 0.75f + wet[i] * 0.3f; peak = Math.Max(peak, Math.Abs(x[i])); }
        float gain = 0.42f / peak;
        int fadeIn = Rate, fadeOut = Rate * 3;
        var data = new byte[n * 2];
        for (int i = 0; i < n; i++)
        {
            float env = Math.Min(1, Math.Min((float)i / fadeIn, (float)(n - i) / fadeOut));
            short s = (short)(Math.Clamp(x[i] * gain * env, -1, 1) * short.MaxValue);
            data[i * 2] = (byte)(s & 0xff);
            data[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }
        return data;
    }
}
