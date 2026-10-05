/// <summary>
/// --midi=DIR: writes a MIDI sketch of every piece of music the game needs (docs/music/brief.md):
/// the family theme, its chords and a bass line, arranged for each decade. Meant to be dragged into
/// a DAW and played on real instruments; the General MIDI sounds are only a preview.
/// </summary>
static class MusicSketches
{
    private const int Tpq = 480;

    // The family theme in C major, as (semitones above the tonic, length in beats), one list per bar.
    private static readonly (int Note, double Beats)[][] Theme44 =
    {
        new[] { (4, 1.0), (7, 1.0), (12, 2.0) },
        new[] { (11, 1.5), (9, 0.5), (7, 2.0) },
        new[] { (9, 1.0), (12, 1.0), (16, 1.0), (14, 1.0) },
        new[] { (12, 3.0) },
        new[] { (4, 1.0), (7, 1.0), (12, 1.0), (14, 1.0) },
        new[] { (16, 2.0), (14, 1.0), (11, 1.0) },
        new[] { (9, 1.0), (12, 1.0), (16, 1.0), (14, 1.0) },
        new[] { (12, 4.0) },
    };

    // The same theme as a waltz, for the fifties.
    private static readonly (int Note, double Beats)[][] Theme34 =
    {
        new[] { (4, 1.0), (7, 1.0), (12, 1.0) },
        new[] { (11, 1.0), (9, 1.0), (7, 1.0) },
        new[] { (9, 1.0), (12, 1.0), (16, 1.0) },
        new[] { (14, 1.0), (12, 2.0) },
        new[] { (4, 1.0), (7, 1.0), (12, 1.0) },
        new[] { (16, 2.0), (14, 1.0) },
        new[] { (9, 1.0), (12, 1.0), (16, 1.0) },
        new[] { (12, 3.0) },
    };

    private record Chord(int Root, string Quality, int? Bass = null);

    // Chords as degrees of the key: I, V/7 (V over its third), vi, IV ...
    private static Chord I => new(0, "maj"); private static Chord V => new(7, "maj"); private static Chord Vb => new(7, "maj", 11);
    private static Chord vi => new(9, "min"); private static Chord IV => new(5, "maj"); private static Chord IVmaj7 => new(5, "maj7");
    private static Chord iii => new(4, "min"); private static Chord ii => new(2, "min"); private static Chord Vsus => new(7, "sus4");

    private static readonly Chord[] ThemeChords = { I, Vb, vi, IV, I, V, IVmaj7, I };
    private static readonly Chord[] Bridge = { vi, iii, IV, I, ii, V, iii, Vsus };
    private static readonly Chord[] Intro = { I, IV, I, Vsus };
    private static readonly Chord[] Outro = { IV, Vsus, I, I };

    // The minor version, for In memoriam and the eighties (natural minor, the dominant borrowed from harmonic minor).
    private static Chord i => new(0, "min"); private static Chord vMinB => new(7, "min", 10); private static Chord VI => new(8, "maj");
    private static Chord iv => new(5, "min"); private static Chord VMaj => new(7, "maj"); private static Chord III => new(3, "maj");
    private static readonly Chord[] MinorChords = { i, vMinB, VI, iv, i, VMaj, VI, i };
    private static readonly Chord[] MinorBridge = { VI, III, iv, i, iv, vMinB, VI, VMaj };
    private static readonly Chord[] MinorIntro = { i, VI, iv, VMaj };
    private static readonly Chord[] MinorOutro = { VI, iv, i, i };

    private record Section(string Name, Chord[] Chords, bool Melody, int MelodyOctave = 0);

    private record Piece(string File, int Tonic, bool Minor, int Bpm, int BeatsPerBar, string Style,
        int MelodyProgram, int ChordProgram, int BassProgram, Section[] Form);

    private static Section[] MajorForm(int lift = 12) => new[]
    {
        new Section("Intro", Intro, false), new Section("A", ThemeChords, true), new Section("B", Bridge, false),
        new Section("A2", ThemeChords, true, lift), new Section("Outro", Outro, false),
    };

    private static Section[] MinorForm => new[]
    {
        new Section("Intro", MinorIntro, false), new Section("A", MinorChords, true), new Section("B", MinorBridge, false),
        new Section("A2", MinorChords, true), new Section("Outro", MinorOutro, false),
    };

    // General MIDI programs, for the preview only.
    private const int Piano = 0, EPiano = 4, Celesta = 8, Vibes = 11, Nylon = 24, Steel = 25, AcBass = 32, ElBass = 33,
        SynthBass = 38, Cello = 42, Strings = 48, Clarinet = 71, Flute = 73, WarmPad = 89, ChoirPad = 91, SquareLead = 80;

    private static readonly Piece[] Pieces =
    {
        new("title", 60, false, 72, 4, "broken", Piano, Strings, Piano, MajorForm()),
        new("memoriam", 57, true, 60, 4, "sparse", Piano, Cello, Cello, MinorForm),
        new("1950s", 65, false, 76, 3, "waltz", Clarinet, Piano, AcBass, MajorForm(0)),
        new("1960s", 67, false, 84, 4, "travis", Flute, Nylon, AcBass, MajorForm(0)),
        new("1970s", 62, false, 80, 4, "rhodes", Flute, EPiano, ElBass, MajorForm(0)),
        new("1980s", 57, true, 90, 4, "arp", SquareLead, WarmPad, SynthBass, MinorForm),
        new("1990s", 64, false, 78, 4, "strum", Piano, Steel, ElBass, MajorForm(0)),
        new("2000s", 60, false, 70, 4, "sparse", Piano, Piano, Piano, MajorForm()),
        new("2010s", 58, false, 66, 4, "broken", Piano, Strings, Cello, MajorForm(0)),
        new("2020s", 62, false, 72, 4, "broken", Piano, WarmPad, ElBass, MajorForm()),
        new("future", 60, false, 60, 4, "pad", Celesta, ChoirPad, SynthBass, MajorForm()),
    };

    public static void Run(string dir)
    {
        Directory.CreateDirectory(dir);
        foreach (var piece in Pieces)
        {
            File.WriteAllBytes(Path.Combine(dir, piece.File + ".mid"), Write(piece));
            Console.WriteLine($"{piece.File}.mid");
        }
    }

    private static int[] Notes(Chord c, int tonic)
    {
        int r = tonic + c.Root;
        return c.Quality switch
        {
            "min" => new[] { r, r + 3, r + 7 },
            "maj7" => new[] { r, r + 4, r + 7, r + 11 },
            "sus4" => new[] { r, r + 5, r + 7 },
            _ => new[] { r, r + 4, r + 7 },
        };
    }

    /// <summary>The theme bar, moved to minor where the piece is minor (the dominant bar keeps its leading note).</summary>
    private static int MinorNote(int n, int bar) => n switch
    {
        4 => 3, 9 => 8, 16 => 15, 11 when bar != 5 => 10, _ => n,
    };

    private static byte[] Write(Piece p)
    {
        var melody = new List<(int Tick, int Note, int Len, int Vel)>();
        var chords = new List<(int Tick, int Note, int Len, int Vel)>();
        var bass = new List<(int Tick, int Note, int Len, int Vel)>();
        int bar = Tpq * p.BeatsPerBar;
        int t = 0;
        var rng = new Random(p.File.GetHashCode() & 0xffff);
        var theme = p.BeatsPerBar == 3 ? Theme34 : Theme44;

        foreach (var section in p.Form)
        {
            for (int b = 0; b < section.Chords.Length; b++)
            {
                var chord = section.Chords[b];
                var notes = Notes(chord, p.Tonic - 12);
                int bassNote = p.Tonic - 24 + (chord.Bass ?? chord.Root);
                if (bassNote < 36) bassNote += 12;

                if (section.Melody)
                {
                    double at = 0;
                    foreach (var (n, beats) in theme[b])
                    {
                        int note = p.Tonic + (p.Minor ? MinorNote(n, b) : n) + section.MelodyOctave;
                        melody.Add((t + (int)(at * Tpq), note, (int)(beats * Tpq) - 20, 84));
                        at += beats;
                    }
                }

                Accompany(p, chord, notes, bassNote, t, bar, chords, bass, rng);
                t += bar;
            }
        }
        // The last chord rings on.
        var last = p.Form[^1].Chords[^1];
        foreach (int n in Notes(last, p.Tonic - 12)) chords.Add((t, n, bar * 2, 60));
        bass.Add((t, p.Tonic - 24 + last.Root < 36 ? p.Tonic - 12 + last.Root : p.Tonic - 24 + last.Root, bar * 2, 70));

        var tracks = new List<byte[]>
        {
            Conductor(p),
            Track("Melody (the family theme)", 0, p.MelodyProgram, melody),
            Track("Chords", 1, p.ChordProgram, chords),
            Track("Bass", 2, p.BassProgram, bass),
        };
        using var ms = new MemoryStream();
        ms.Write("MThd"u8);
        Be32(ms, 6); Be16(ms, 1); Be16(ms, tracks.Count); Be16(ms, Tpq);
        foreach (var tr in tracks) ms.Write(tr);
        return ms.ToArray();
    }

    /// <summary>How the chords are played: the style of the decade.</summary>
    private static void Accompany(Piece p, Chord chord, int[] notes, int bassNote, int t, int bar,
        List<(int, int, int, int)> chords, List<(int, int, int, int)> bass, Random rng)
    {
        int q = Tpq, e = Tpq / 2, s = Tpq / 4;
        switch (p.Style)
        {
            case "broken":
                // A low root, then the chord broken upwards and back in eighths.
                bass.Add((t, bassNote, bar - 10, 72));
                int[] shape = { notes[0], notes[^1] , notes[1] + 12, notes[^1], notes[0] + 12, notes[^1], notes[1] + 12, notes[^1] };
                for (int i = 0; i < 8; i++) chords.Add((t + i * e, shape[i], e * 2, 50 + (i % 2 == 0 ? 8 : 0)));
                break;
            case "sparse":
                // Bass and a held chord; a single high note answers in the second half.
                bass.Add((t, bassNote, bar - 10, 64));
                foreach (int n in notes) chords.Add((t + e, n + 12, bar - e - 10, 44));
                if (rng.NextDouble() < 0.6) chords.Add((t + 2 * q + e, notes[rng.Next(notes.Length)] + 24, q * 2, 38));
                break;
            case "waltz":
                // Oom pah pah: bass on one, the chord on two and three.
                bass.Add((t, bassNote, q - 10, 74));
                foreach (int beat in new[] { 1, 2 })
                    foreach (int n in notes) chords.Add((t + beat * q, n + 12, q - 30, 50));
                break;
            case "travis":
                // A fingerpicked guitar: alternating bass on the beats, chord tones on the offbeats.
                for (int beat = 0; beat < 4; beat++)
                {
                    int thumb = beat % 2 == 0 ? bassNote + 12 : bassNote + 19;
                    chords.Add((t + beat * q, thumb, q - 10, 60));
                    chords.Add((t + beat * q + e, notes[1 + beat % (notes.Length - 1)] + 12, e, 48));
                }
                bass.Add((t, bassNote, q * 2 - 10, 66));
                bass.Add((t + 2 * q, bassNote + 7, q * 2 - 10, 60));
                break;
            case "rhodes":
                // Chords on one and on the "and" of two, held; a walking root.
                foreach (int n in notes) { chords.Add((t, n + 12, q + e, 54)); chords.Add((t + q + e, n + 12, 2 * q + e, 48)); }
                bass.Add((t, bassNote, q + e, 74));
                bass.Add((t + q + e, bassNote, e, 60));
                bass.Add((t + 2 * q, bassNote + 7, q, 66));
                bass.Add((t + 3 * q, bassNote + 12, q, 62));
                break;
            case "arp":
                // A sixteenth arpeggio over a held pad (the pad is the same track; split it in the DAW).
                foreach (int n in notes) chords.Add((t, n, bar - 10, 40));
                for (int i = 0; i < 16; i++) chords.Add((t + i * s, notes[i % notes.Length] + 24, s - 10, 46 + (i % 4 == 0 ? 10 : 0)));
                for (int i = 0; i < 8; i++) bass.Add((t + i * e, bassNote, e - 30, i % 2 == 0 ? 76 : 60));
                break;
            case "strum":
                // A strummed acoustic: down, down up, up down up.
                foreach (var (beat, vel) in new[] { (0.0, 64), (1.0, 56), (1.5, 50), (2.5, 50), (3.0, 56), (3.5, 48) })
                    for (int k = 0; k < notes.Length; k++)
                        chords.Add((t + (int)(beat * q) + k * 12, notes[k] + 12, e, vel));
                bass.Add((t, bassNote, 2 * q - 10, 70));
                bass.Add((t + 2 * q, bassNote, 2 * q - 10, 62));
                break;
            default: // "pad": long held chords, a slow bass.
                foreach (int n in notes) chords.Add((t, n + 12, bar - 10, 46));
                bass.Add((t, bassNote, bar - 10, 58));
                break;
        }
    }

    private static byte[] Conductor(Piece p)
    {
        var data = new MemoryStream();
        Meta(data, 0x03, System.Text.Encoding.ASCII.GetBytes($"One More Year: {p.File}"));
        int mpq = 60_000_000 / p.Bpm;
        Meta(data, 0x51, new[] { (byte)(mpq >> 16), (byte)(mpq >> 8), (byte)mpq });
        Meta(data, 0x58, new byte[] { (byte)p.BeatsPerBar, 2, 24, 8 });
        Meta(data, 0x2F, Array.Empty<byte>());
        return Chunk(data.ToArray());
    }

    private static byte[] Track(string name, int channel, int program, List<(int Tick, int Note, int Len, int Vel)> notes)
    {
        var events = new List<(int Tick, int Order, byte[] Bytes)>();
        foreach (var (tick, note, len, vel) in notes)
        {
            byte n = (byte)Math.Clamp(note, 0, 127);
            events.Add((tick, 1, new byte[] { (byte)(0x90 | channel), n, (byte)Math.Clamp(vel, 1, 127) }));
            events.Add((tick + Math.Max(10, len), 0, new byte[] { (byte)(0x80 | channel), n, 0 }));
        }
        var data = new MemoryStream();
        Meta(data, 0x03, System.Text.Encoding.ASCII.GetBytes(name));
        VarLen(data, 0); data.WriteByte((byte)(0xC0 | channel)); data.WriteByte((byte)program);
        int last = 0;
        foreach (var ev in events.OrderBy(x => x.Tick).ThenBy(x => x.Order))
        {
            VarLen(data, ev.Tick - last);
            data.Write(ev.Bytes);
            last = ev.Tick;
        }
        Meta(data, 0x2F, Array.Empty<byte>());
        return Chunk(data.ToArray());
    }

    private static void Meta(Stream s, byte type, byte[] payload)
    {
        VarLen(s, 0);
        s.WriteByte(0xFF); s.WriteByte(type);
        VarLen(s, payload.Length);
        s.Write(payload);
    }

    private static byte[] Chunk(byte[] data)
    {
        var ms = new MemoryStream();
        ms.Write("MTrk"u8);
        Be32(ms, data.Length);
        ms.Write(data);
        return ms.ToArray();
    }

    private static void VarLen(Stream s, int value)
    {
        var bytes = new Stack<byte>();
        bytes.Push((byte)(value & 0x7F));
        while ((value >>= 7) > 0) bytes.Push((byte)((value & 0x7F) | 0x80));
        foreach (var b in bytes) s.WriteByte(b);
    }

    private static void Be32(Stream s, int v) { s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16)); s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }
    private static void Be16(Stream s, int v) { s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }
}
