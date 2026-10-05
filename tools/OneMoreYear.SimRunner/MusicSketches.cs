/// <summary>
/// --midi=DIR: writes a complete arrangement of every piece of music the game needs
/// (docs/music/brief.md). Each file has one track per instrument, named after the Studio One
/// instrument to put on it, so the only work left is choosing the sounds. Every piece is built from
/// the family theme: an intro, the theme, the theme again with a second voice, a bridge with its own
/// melody, the theme an octave up and fuller, a quiet bridge, the theme bare, and an ending that
/// slows down and rings out.
/// </summary>
static partial class MusicSketches
{
    private const int Tpq = 480;

    // --- The melodies, in C (semitones above the tonic, length in beats), one list per bar ---------

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

    // The bridge has a tune of its own: it climbs, rests, climbs higher, and leads back home.
    private static readonly (int Note, double Beats)[][] Bridge44 =
    {
        new[] { (9, 1.0), (11, 1.0), (12, 2.0) },
        new[] { (11, 1.0), (7, 1.0), (4, 2.0) },
        new[] { (5, 1.0), (9, 1.0), (12, 1.0), (9, 1.0) },
        new[] { (7, 3.0) },
        new[] { (14, 1.0), (12, 1.0), (9, 2.0) },
        new[] { (11, 1.0), (14, 1.0), (19, 2.0) },
        new[] { (16, 1.0), (14, 1.0), (11, 2.0) },
        new[] { (12, 2.0), (14, 2.0) },
    };

    private static readonly (int Note, double Beats)[][] Bridge34 =
    {
        new[] { (9, 1.0), (11, 1.0), (12, 1.0) },
        new[] { (11, 1.0), (7, 1.0), (4, 1.0) },
        new[] { (5, 1.0), (9, 1.0), (12, 1.0) },
        new[] { (7, 3.0) },
        new[] { (14, 1.0), (12, 1.0), (9, 1.0) },
        new[] { (11, 1.0), (14, 1.0), (19, 1.0) },
        new[] { (16, 1.0), (14, 1.0), (11, 1.0) },
        new[] { (12, 2.0), (14, 1.0) },
    };

    // --- Chords ----------------------------------------------------------------------------------

    private record Chord(int Root, string Quality, int? Bass = null);

    private static Chord I => new(0, "maj"); private static Chord V => new(7, "maj"); private static Chord Vb => new(7, "maj", 11);
    private static Chord vi => new(9, "min"); private static Chord IV => new(5, "maj"); private static Chord IVmaj7 => new(5, "maj7");
    private static Chord iii => new(4, "min"); private static Chord ii => new(2, "min"); private static Chord Vsus => new(7, "sus4");
    private static readonly Chord[] MajTheme = { I, Vb, vi, IV, I, V, IVmaj7, I };
    private static readonly Chord[] MajBridge = { vi, iii, IV, I, ii, V, iii, Vsus };
    private static readonly Chord[] MajIntro = { I, IV, I, Vsus };
    private static readonly Chord[] MajOutro = { IV, Vsus, I, I };

    private static Chord i => new(0, "min"); private static Chord vB => new(7, "min", 10); private static Chord VI => new(8, "maj");
    private static Chord iv => new(5, "min"); private static Chord VMaj => new(7, "maj"); private static Chord III => new(3, "maj");
    private static readonly Chord[] MinTheme = { i, vB, VI, iv, i, VMaj, VI, i };
    private static readonly Chord[] MinBridge = { VI, III, iv, i, iv, vB, VI, VMaj };
    private static readonly Chord[] MinIntro = { i, VI, iv, VMaj };
    private static readonly Chord[] MinOutro = { VI, iv, i, i };

    private static readonly int[] MajorScale = { 0, 2, 4, 5, 7, 9, 11 };
    private static readonly int[] MinorScale = { 0, 2, 3, 5, 7, 8, 10 };

    // --- The pieces --------------------------------------------------------------------------------

    /// <summary>An instrument on a track: what to put on it in Studio One, and a General MIDI sound for the preview.</summary>
    private record Part(string Name, int Program);

    private record Piece(string File, int Tonic, bool Minor, int Bpm, int BeatsPerBar, string Style, string? Drums,
        Part Melody, Part Second, Part Chords, Part? Pad, Part Bass, int Lift);

    private static Part P(string name, int program) => new(name, program);

    private static readonly Piece[] Pieces =
    {
        new("title", 60, false, 72, 4, "broken", null,
            P("Melody: Clarinet (Presence XT)", 71), P("Second voice: French horn, quiet (Presence XT)", 60), P("Chords: Piano (Presence XT)", 0),
            P("Pad: Strings, legato, quiet in the mix", 48), P("Bass: Piano, left hand (Presence XT)", 0), 12),
        new("memoriam", 57, true, 60, 4, "sparse", null,
            P("Melody: Piano (Presence XT)", 0), P("Second voice: Cello (Presence XT)", 42), P("Chords: Piano (Presence XT)", 0),
            P("Pad: Strings, legato, very quiet", 48), P("Bass: Cello (Presence XT)", 42), 0),
        new("1950s", 65, false, 76, 3, "waltz", "brush",
            P("Melody: Clarinet (Presence XT)", 71), P("Second voice: Flute (Presence XT)", 73), P("Chords: Piano (Presence XT)", 0),
            null, P("Bass: Upright bass (Presence XT)", 32), 0),
        new("1960s", 67, false, 84, 4, "travis", null,
            P("Melody: Flute (Presence XT)", 73), P("Second voice: Violin (Presence XT)", 40), P("Chords: Nylon or acoustic guitar (Presence XT)", 24),
            null, P("Bass: Upright bass (Presence XT)", 32), 0),
        new("1970s", 62, false, 80, 4, "rhodes", "soft",
            P("Melody: Clean electric guitar (Presence XT)", 27), P("Second voice: Flute (Presence XT)", 73), P("Chords: Electric piano (Presence XT)", 4),
            null, P("Bass: Electric bass (Presence XT)", 33), 0),
        new("1980s", 57, true, 90, 4, "arp", "machine",
            P("Melody: Bell or soft lead (Presence XT Mallets, or Mojito)", 11), P("Second voice: Soft lead (Mai Tai)", 81),
            P("Chords: Arpeggio (Mai Tai)", 81), P("Pad: Wide pad (Mai Tai)", 89), P("Bass: Synth bass (Mojito)", 38), 0),
        new("1990s", 64, false, 78, 4, "strum", "rim",
            P("Melody: Piano (Presence XT)", 0), P("Second voice: Violin (Presence XT)", 40), P("Chords: Acoustic guitar (Presence XT)", 25),
            P("Pad: Thin pad (Mai Tai)", 89), P("Bass: Electric bass (Presence XT)", 33), 0),
        new("2000s", 60, false, 70, 4, "sparse", null,
            P("Melody: Piano (Presence XT)", 0), P("Second voice: Piano, high (Presence XT)", 0), P("Chords: Piano (Presence XT)", 0),
            null, P("Bass: Piano, left hand (Presence XT)", 0), 12),
        new("2010s", 58, false, 66, 4, "broken", null,
            P("Melody: Piano (Presence XT)", 0), P("Second voice: Cello, in its high register (Presence XT)", 42), P("Chords: Piano (Presence XT)", 0),
            P("Pad: Strings, legato, slow swell", 48), P("Bass: Piano, left hand (Presence XT)", 0), 0),
        new("2020s", 62, false, 72, 4, "broken", null,
            P("Melody: Piano (Presence XT)", 0), P("Second voice: Soft lead (Mai Tai)", 81), P("Chords: Piano (Presence XT)", 0),
            P("Pad: Warm pad (Mai Tai)", 89), P("Bass: Electric bass (Presence XT)", 33), 12),
        new("future", 60, false, 60, 4, "pad", null,
            P("Melody: Celesta or bells (Presence XT Mallets)", 8), P("Second voice: Soft glassy lead (Mai Tai)", 81), P("Chords: Pad, slow attack (Mai Tai)", 89),
            P("Pad: Choir, far back (Presence XT)", 91), P("Bass: Soft synth bass (Mojito)", 38), 12),
    };

    // --- The form ----------------------------------------------------------------------------------

    private enum Tune { None, Theme, Embellished, Bridge }

    /// <summary>One part of a piece: its chords, what the melody does, how full the rest is, how loud.</summary>
    private record Section(string Name, bool Bridge, Tune Melody, int Lift, bool Second, bool Pad, string Density, bool Drums, double Loud, Tune SecondTune = Tune.None);

    /// <summary>
    /// The form: an intro, the theme, the theme with a second voice, the bridge, the theme higher and
    /// fuller, and an ending that rings out. Whatever has come in stays until the ending, so nothing
    /// drops out in the middle. A short piece plays the bridge and the big theme twice.
    /// The title keeps the form it was recorded from.
    /// </summary>
    private static Section[] Form(Piece p)
    {
        if (p.File == "title") return RecordedTitleForm(p).Where(s => !(p.Bpm <= 66 && s.Name == "A, bare")).ToArray();
        var intro = new Section("Intro", false, Tune.None, 0, false, false, "held", false, 0.75);
        var a = new Section("A", false, Tune.Theme, 0, false, false, "light", false, 0.85);
        var a2 = new Section("A with a second voice", false, Tune.Embellished, 0, true, false, "full", true, 0.92);
        var bridge = new Section("Bridge", true, Tune.Bridge, 0, true, p.Pad != null, "full", true, 0.96);
        var big = new Section("A, higher and fuller", false, Tune.Theme, p.Lift, true, p.Pad != null, "full", true, 1.06);
        var end = new Section("Ending", false, Tune.None, 0, false, p.Pad != null, "held", false, 0.72);
        var form = new List<Section> { intro, a, a2, bridge, big };
        double barSeconds = p.BeatsPerBar * 60.0 / p.Bpm;
        if ((form.Count * 8 - 4) * barSeconds < 130) form.AddRange(new[] { bridge with { Loud = 0.92 }, big with { Loud = 1.1 } });
        form.Add(end);
        return form.ToArray();
    }

    private static Section[] RecordedTitleForm(Piece p) => new[]
    {
        new Section("Intro", false, Tune.None, 0, false, false, "held", false, 0.75),
        new Section("A", false, Tune.Theme, 0, false, false, "light", false, 0.85),
        new Section("A with a second voice", false, Tune.Embellished, 0, true, false, "full", true, 0.95),
        new Section("Bridge", true, Tune.Bridge, 0, false, p.Pad != null, "full", true, 0.95),
        new Section("A, higher and fuller", false, Tune.Theme, p.Lift, true, p.Pad != null, "full", true, 1.08),
        new Section("Quiet bridge", true, Tune.None, 0, false, p.Pad != null, "light", false, 0.8, SecondTune: Tune.Bridge),
        new Section("A, bare", false, Tune.Theme, 0, false, false, "light", false, 0.82),
        new Section("Ending", false, Tune.None, 0, false, p.Pad != null, "held", false, 0.72),
    };

    public static void Run(string dir)
    {
        Directory.CreateDirectory(dir);
        foreach (var piece in Pieces)
        {
            double seconds;
            File.WriteAllBytes(Path.Combine(dir, piece.File + ".mid"), piece.File switch { "memoriam" => WriteMemoriam(piece, out seconds), "1980s" => WriteEighties(piece, out seconds), _ => Write(piece, out seconds) });
            Console.WriteLine($"{piece.File}.mid  {(int)seconds / 60}:{(int)seconds % 60:00}");
        }
    }

    // --- Building a piece --------------------------------------------------------------------------

    private sealed class Notes : List<(int Tick, int Note, int Len, int Vel)>
    {
        public void Add(int tick, int note, int len, double vel) => Add((tick, note, len, (int)Math.Clamp(vel, 1, 127)));
    }

    private static byte[] Write(Piece p, out double seconds)
    {
        var melody = new Notes(); var second = new Notes(); var chords = new Notes(); var pad = new Notes(); var bass = new Notes(); var drums = new Notes();
        int bar = Tpq * p.BeatsPerBar;
        int t = 0;
        var rng = new Random(p.File.Length * 7919 + p.Tonic);
        var scale = p.Minor ? MinorScale : MajorScale;
        var form = Form(p);

        foreach (var s in form)
        {
            var progression = s.Name == "Intro" ? (p.Minor ? MinIntro : MajIntro)
                : s.Name == "Ending" ? (p.Minor ? MinOutro : MajOutro)
                : s.Bridge ? (p.Minor ? MinBridge : MajBridge) : (p.Minor ? MinTheme : MajTheme);
            for (int b = 0; b < progression.Length; b++)
            {
                var chord = progression[b];
                var tones = ChordTones(chord, p.Tonic - 12);
                int bassNote = p.Tonic - 24 + (chord.Bass ?? chord.Root);
                if (bassNote < 36) bassNote += 12;

                // The tune, and the second voice a third under it or answering it.
                var line = Line(p, s.Melody, s.Bridge, b).ToList();
                foreach (var (at, note, len) in line)
                    melody.Add(t + at, note + s.Lift, len - 20, 84 * s.Loud);
                if (s.Second && line.Count > 0)
                {
                    // A third under the long notes, held over the short ones until the next long note,
                    // and over the rest of the bar, so the second voice never drops out in the middle of a phrase.
                    var held = line.Where((n, i) => i == 0 || n.Len >= Tpq).ToList();
                    int lineEnd = Math.Max(bar, line[^1].At + line[^1].Len);
                    for (int i = 0; i < held.Count; i++)
                    {
                        int until = i + 1 < held.Count ? held[i + 1].At : lineEnd;
                        second.Add(t + held[i].At, ThirdBelow(held[i].Note, scale, p.Tonic) + s.Lift - (s.Lift > 0 ? 12 : 0), until - held[i].At - 20, 58 * s.Loud);
                    }
                }
                foreach (var (at, note, len) in Line(p, s.SecondTune, s.Bridge, b))
                    second.Add(t + at, note - 12, len - 20, 56 * s.Loud);
                // Through the ending the melody holds a note of each chord, so it never leaves before the last one.
                if (s.Name == "Ending" && p.File != "title")
                {
                    var near = ChordTones(chord, p.Tonic).Concat(ChordTones(chord, p.Tonic + 12)).OrderBy(n => Math.Abs(n - (p.Tonic + 4))).First();
                    melody.Add(t, near, bar - 20, 62 * s.Loud);
                }

                Accompany(p, s.Density, tones, bassNote, t, bar, s.Loud, chords, bass, rng);
                if (s.Pad && p.Pad != null)
                    foreach (int n in tones) pad.Add(t, n + 12, bar - 10, 42 * s.Loud);
                if (s.Drums && p.Drums != null) Drum(p, t, bar, s.Loud, drums);
                t += bar;
            }
        }

        // The last chord rings on, with the melody home on the tonic.
        var last = (p.Minor ? MinOutro : MajOutro)[^1];
        foreach (int n in ChordTones(last, p.Tonic - 12)) chords.Add(t, n + 12, bar * 2, 44);
        if (p.Pad != null) foreach (int n in ChordTones(last, p.Tonic - 12)) pad.Add(t, n + 12, bar * 2, 34);
        int lastBass = p.Tonic - 24 + last.Root;
        bass.Add(t, lastBass < 36 ? lastBass + 12 : lastBass, bar * 2, 54);
        melody.Add(t, p.Tonic, bar * 2, 60);
        int end = t + bar * 2;

        // Slowing down over the last four bars and the final chord.
        var tempo = new List<(int Tick, double Bpm)> { (0, p.Bpm) };
        int slowFrom = end - bar * 6;
        for (int k = 0; k <= 12; k++) tempo.Add((slowFrom + (end - slowFrom) * k / 12, p.Bpm * (1 - 0.2 * k / 12.0)));
        seconds = 0;
        for (int k = 0; k < tempo.Count; k++)
        {
            int to = k + 1 < tempo.Count ? tempo[k + 1].Tick : end;
            seconds += (to - tempo[k].Tick) / (double)Tpq * 60 / tempo[k].Bpm;
        }

        var tracks = new List<byte[]> { Conductor(p, tempo) };
        int channel = 0;
        // A string pad is a string section: the top note to the violin, the middle to the viola, the lowest to the cello.
        var parts = new List<(Part?, Notes)> { (p.Melody, melody), (p.Second, second), (p.Chords, chords) };
        if (p.Pad != null && p.Pad.Name.StartsWith("Pad: Strings"))
        {
            string how = p.Pad.Name["Pad: Strings".Length..].TrimStart(',', ' ');
            var violin = new Notes(); var viola = new Notes(); var cello = new Notes();
            foreach (var chord in pad.GroupBy(n => n.Tick))
            {
                var sorted = chord.OrderBy(n => n.Note).ToList();
                cello.Add(sorted[0]);
                violin.Add(sorted[^1]);
                foreach (var middle in sorted.Skip(1).Take(sorted.Count - 2)) viola.Add(middle);
            }
            string suffix = how.Length > 0 ? $", {how} (Presence XT)" : " (Presence XT)";
            parts.Add((new Part("Pad: Violin" + suffix, 40), violin));
            parts.Add((new Part("Pad: Viola" + suffix, 41), viola));
            parts.Add((new Part("Pad: Cello" + suffix, 42), cello));
        }
        else parts.Add((p.Pad, pad));
        parts.Add((p.Bass, bass));
        foreach (var (part, notes) in parts)
            if (part != null && notes.Count > 0) tracks.Add(Track(part.Name, channel == 9 ? ++channel : channel++, part.Program, notes));
        if (drums.Count > 0) tracks.Add(Track(DrumName(p.Drums!), 9, 0, drums));

        using var ms = new MemoryStream();
        ms.Write("MThd"u8);
        Be32(ms, 6); Be16(ms, 1); Be16(ms, tracks.Count); Be16(ms, Tpq);
        foreach (var tr in tracks) ms.Write(tr);
        return ms.ToArray();
    }

    private static string DrumName(string kind) => kind switch
    {
        "brush" => "Drums: Brushes, very quiet (Impact XT)",
        "soft" => "Drums: Soft kit, quiet (Impact XT)",
        "machine" => "Drums: Drum machine, quiet (Impact XT)",
        _ => "Drums: Rim and soft kick (Impact XT)",
    };

    /// <summary>One bar of a tune, as (tick in the bar, note, length), in the piece's key and mode.</summary>
    private static List<(int At, int Note, int Len)> Line(Piece p, Tune tune, bool bridge, int bar)
    {
        var result = new List<(int, int, int)>();
        if (tune == Tune.None) return result;
        var source = tune == Tune.Bridge ? (p.BeatsPerBar == 3 ? Bridge34 : Bridge44) : (p.BeatsPerBar == 3 ? Theme34 : Theme44);
        var notes = source[bar];
        double at = 0;
        for (int k = 0; k < notes.Length; k++)
        {
            var (n, beats) = notes[k];
            int note = p.Tonic + (p.Minor ? ToMinor(n, bar, bridge || tune == Tune.Bridge) : n);
            // The embellished theme: a passing note on the way to the next one, a third away.
            if (tune == Tune.Embellished && beats >= 1 && k + 1 < notes.Length && Math.Abs(notes[k + 1].Note - n) is 3 or 4)
            {
                int next = p.Tonic + (p.Minor ? ToMinor(notes[k + 1].Note, bar, false) : notes[k + 1].Note);
                int passing = StepBetween(note, next, p.Minor ? MinorScale : MajorScale, p.Tonic);
                int half = (int)(beats * Tpq / 2);
                result.Add(((int)(at * Tpq), note, half));
                result.Add(((int)(at * Tpq) + half, passing, half));
            }
            else result.Add(((int)(at * Tpq), note, (int)(beats * Tpq)));
            at += beats;
        }
        return result;
    }

    /// <summary>Major melody steps moved to minor; the dominant bar keeps its leading note.</summary>
    private static int ToMinor(int n, int bar, bool bridge)
    {
        bool dominant = bridge ? bar == 7 : bar == 5;
        return n switch { 4 => 3, 9 => 8, 16 => 15, 21 => 20, 11 when !dominant => 10, 23 when !dominant => 22, _ => n };
    }

    private static int Degree(int note, int[] scale, int tonic, out int octave)
    {
        int rel = note - tonic;
        octave = (int)Math.Floor(rel / 12.0);
        int pc = rel - octave * 12;
        int idx = 0;
        for (int k = 0; k < scale.Length; k++) if (scale[k] <= pc) idx = k;
        return idx;
    }

    private static int FromDegree(int idx, int octave, int[] scale, int tonic)
    {
        while (idx < 0) { idx += 7; octave--; }
        while (idx > 6) { idx -= 7; octave++; }
        return tonic + octave * 12 + scale[idx];
    }

    private static int ThirdBelow(int note, int[] scale, int tonic)
    {
        int idx = Degree(note, scale, tonic, out int oct);
        return FromDegree(idx - 2, oct, scale, tonic);
    }

    private static int StepBetween(int a, int b, int[] scale, int tonic)
    {
        int idx = Degree(a, scale, tonic, out int oct);
        return FromDegree(idx + (b > a ? 1 : -1), oct, scale, tonic);
    }

    private static int[] ChordTones(Chord c, int tonic)
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

    // --- Accompaniment, in the style of the decade ------------------------------------------------

    private static void Accompany(Piece p, string density, int[] tones, int bassNote, int t, int bar, double loud,
        Notes chords, Notes bass, Random rng)
    {
        int q = Tpq, e = Tpq / 2, s = Tpq / 4;
        if (density == "held")
        {
            foreach (int n in tones) chords.Add(t, n + 12, bar - 10, 44 * loud);
            bass.Add(t, bassNote, bar - 10, 58 * loud);
            return;
        }
        bool full = density == "full";
        switch (p.Style)
        {
            case "broken":
            {
                int[] shape = { tones[0], tones[^1], tones[1] + 12, tones[^1], tones[0] + 12, tones[^1], tones[1] + 12, tones[^1] };
                int step = full ? e : q;
                for (int k = 0; k < bar / step; k++)
                    chords.Add(t + k * step, shape[(k * (full ? 1 : 2)) % 8], step * 2, (50 + (k % 2 == 0 ? 8 : 0)) * loud);
                bass.Add(t, bassNote, bar - 10, 70 * loud);
                break;
            }
            case "sparse":
                bass.Add(t, bassNote, bar - 10, 62 * loud);
                foreach (int n in tones) chords.Add(t + e, n + 12, bar - e - 10, 42 * loud);
                if (full || rng.NextDouble() < 0.5) chords.Add(t + 2 * q + e, tones[rng.Next(tones.Length)] + 24, 2 * q, 36 * loud);
                break;
            case "waltz":
                bass.Add(t, bassNote, q - 10, 72 * loud);
                foreach (int beat in full ? new[] { 1, 2 } : new[] { 1 })
                    foreach (int n in tones) chords.Add(t + beat * q, n + 12, q - 30, 48 * loud);
                if (full && rng.NextDouble() < 0.3) bass.Add(t + 2 * q, bassNote + 7, q - 10, 56 * loud);
                break;
            case "travis":
                for (int beat = 0; beat < 4; beat++)
                {
                    int thumb = beat % 2 == 0 ? bassNote + 12 : bassNote + 19;
                    chords.Add(t + beat * q, thumb, q - 10, 58 * loud);
                    if (full || beat % 2 == 1) chords.Add(t + beat * q + e, tones[1 + beat % (tones.Length - 1)] + 12, e, 46 * loud);
                }
                bass.Add(t, bassNote, 2 * q - 10, 64 * loud);
                if (full) bass.Add(t + 2 * q, bassNote + 7, 2 * q - 10, 58 * loud);
                break;
            case "rhodes":
                foreach (int n in tones)
                {
                    chords.Add(t, n + 12, full ? q + e : bar - 10, 52 * loud);
                    if (full) chords.Add(t + q + e, n + 12, 2 * q + e, 46 * loud);
                }
                bass.Add(t, bassNote, full ? q + e : 2 * q, 72 * loud);
                if (full)
                {
                    bass.Add(t + q + e, bassNote, e, 58 * loud);
                    bass.Add(t + 2 * q, bassNote + 7, q, 64 * loud);
                    bass.Add(t + 3 * q, bassNote + 12, q, 60 * loud);
                }
                else bass.Add(t + 2 * q, bassNote + 7, 2 * q - 10, 60 * loud);
                break;
            case "arp":
            {
                int step = full ? s : e;
                for (int k = 0; k < bar / step; k++)
                    chords.Add(t + k * step, tones[k % tones.Length] + 24 + (k / tones.Length % 2) * 12, step - 10, (44 + (k % 4 == 0 ? 10 : 0)) * loud);
                for (int k = 0; k < (full ? 8 : 4); k++)
                    bass.Add(t + k * (full ? e : q), bassNote, (full ? e : q) - 30, (k % 2 == 0 ? 74 : 60) * loud);
                break;
            }
            case "strum":
                foreach (var (beat, vel) in full ? new[] { (0.0, 64), (1.0, 56), (1.5, 50), (2.5, 50), (3.0, 56), (3.5, 48) } : new[] { (0.0, 60), (2.0, 54) })
                    for (int k = 0; k < tones.Length; k++)
                        chords.Add(t + (int)(beat * q) + k * 12, tones[k] + 12, full ? e : 2 * q - 20, vel * loud);
                bass.Add(t, bassNote, 2 * q - 10, 68 * loud);
                bass.Add(t + 2 * q, bassNote, 2 * q - 10, 60 * loud);
                break;
            default:
                foreach (int n in tones) chords.Add(t, n + 12, bar - 10, 46 * loud);
                bass.Add(t, bassNote, bar - 10, 56 * loud);
                if (full) chords.Add(t + 2 * q, tones[rng.Next(tones.Length)] + 24, 2 * q, 34 * loud);
                break;
        }
    }

    // General MIDI drums: kick 36, side stick 37, snare 38, closed hat 42, ride 51.
    private static void Drum(Piece p, int t, int bar, double loud, Notes drums)
    {
        int q = Tpq, e = Tpq / 2, s = Tpq / 4;
        switch (p.Drums)
        {
            case "brush":
                drums.Add(t, 36, e, 38 * loud);
                for (int beat = 0; beat < p.BeatsPerBar; beat++) drums.Add(t + beat * q, 51, e, 28 * loud);
                for (int beat = 1; beat < p.BeatsPerBar; beat++) drums.Add(t + beat * q, 38, e, 24 * loud);
                break;
            case "soft":
                drums.Add(t, 36, e, 56 * loud); drums.Add(t + 2 * q, 36, e, 50 * loud);
                drums.Add(t + q, 37, e, 44 * loud); drums.Add(t + 3 * q, 37, e, 44 * loud);
                for (int k = 0; k < 8; k++) drums.Add(t + k * e, 42, e - 20, (k % 2 == 0 ? 32 : 24) * loud);
                break;
            case "machine":
                drums.Add(t, 36, e, 62 * loud); drums.Add(t + 2 * q, 36, e, 58 * loud);
                drums.Add(t + q, 38, e, 54 * loud); drums.Add(t + 3 * q, 38, e, 54 * loud);
                for (int k = 0; k < 16; k++) drums.Add(t + k * s, 42, s - 10, (k % 2 == 0 ? 28 : 18) * loud);
                break;
            default:
                drums.Add(t, 36, e, 52 * loud); drums.Add(t + 2 * q + e, 36, e, 44 * loud);
                drums.Add(t + q, 37, e, 42 * loud); drums.Add(t + 3 * q, 37, e, 42 * loud);
                for (int k = 0; k < 8; k++) drums.Add(t + k * e, 42, e - 20, 24 * loud);
                break;
        }
    }

    // --- Writing the MIDI file ---------------------------------------------------------------------

    private static byte[] Conductor(Piece p, List<(int Tick, double Bpm)> tempo)
    {
        var data = new MemoryStream();
        var events = new List<(int Tick, byte Type, byte[] Payload)>
        {
            (0, 0x03, System.Text.Encoding.ASCII.GetBytes($"One More Year: {p.File}")),
            (0, 0x58, new byte[] { (byte)p.BeatsPerBar, 2, 24, 8 }),
        };
        foreach (var (tick, bpm) in tempo)
        {
            int mpq = (int)(60_000_000 / bpm);
            events.Add((tick, 0x51, new[] { (byte)(mpq >> 16), (byte)(mpq >> 8), (byte)mpq }));
        }
        int last = 0;
        foreach (var ev in events.OrderBy(x => x.Tick))
        {
            VarLen(data, ev.Tick - last);
            data.WriteByte(0xFF); data.WriteByte(ev.Type);
            VarLen(data, ev.Payload.Length);
            data.Write(ev.Payload);
            last = ev.Tick;
        }
        VarLen(data, 0); data.WriteByte(0xFF); data.WriteByte(0x2F); data.WriteByte(0);
        return Chunk(data.ToArray());
    }

    private static byte[] Track(string name, int channel, int program, Notes notes)
    {
        var events = new List<(int Tick, int Order, byte[] Bytes)>();
        foreach (var (tick, note, len, vel) in notes)
        {
            byte n = (byte)Math.Clamp(note, 0, 127);
            events.Add((tick, 1, new byte[] { (byte)(0x90 | channel), n, (byte)Math.Clamp(vel, 1, 127) }));
            events.Add((tick + Math.Max(10, len), 0, new byte[] { (byte)(0x80 | channel), n, 0 }));
        }
        var data = new MemoryStream();
        var nameBytes = System.Text.Encoding.ASCII.GetBytes(name);
        VarLen(data, 0); data.WriteByte(0xFF); data.WriteByte(0x03); VarLen(data, nameBytes.Length); data.Write(nameBytes);
        if (channel != 9) { VarLen(data, 0); data.WriteByte((byte)(0xC0 | channel)); data.WriteByte((byte)program); }
        int last = 0;
        foreach (var ev in events.OrderBy(x => x.Tick).ThenBy(x => x.Order))
        {
            VarLen(data, ev.Tick - last);
            data.Write(ev.Bytes);
            last = ev.Tick;
        }
        VarLen(data, 0); data.WriteByte(0xFF); data.WriteByte(0x2F); data.WriteByte(0);
        return Chunk(data.ToArray());
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
