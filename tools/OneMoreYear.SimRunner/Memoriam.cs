/// <summary>
/// In memoriam, written out by hand rather than from the shared form: the music for a death in the
/// family. D minor at 56 beats a minute, slowing to 42 at the end. The bass walks the old lament down
/// (D, C, B flat, A), the harmony leans on a half diminished chord and, near the end, a Neapolitan
/// one, and it ends on a bare D without a third, so it never comes to rest.
///
/// Nothing drops out once it has come in: the piano tolls from the first bar to the last, the low
/// strings and the double bass hold the floor, a solo cello sings the theme, a solo violin takes the
/// bridge high and thin, a choir comes in far off at the height of it and fades with the end, and in the coda the cello and
/// the violin fall step by step to the D while the piano remembers the first bars of the tune.
/// </summary>
static partial class MusicSketches
{
    private record MChord(int Bass, int[] Pcs);

    private static MChord Ch(int bass, params int[] pcs) => new(bass, pcs);

    private static byte[] WriteMemoriam(Piece p, out double seconds)
    {
        const double bpm = 56, slowTo = 42;
        int q = Tpq, bar = 4 * q;

        // Pitch classes: C 0, C# 1, D 2, Eb 3, E 4, F 5, G 7, A 9, Bb 10. Bass notes between G1 and F2.
        var dm = Ch(38, 2, 5, 9); var dmC = Ch(36, 2, 5, 9, 0); var bbMaj7 = Ch(34, 10, 2, 5, 9);
        var aSus = Ch(33, 9, 2, 4); var a = Ch(33, 9, 1, 4); var gm = Ch(31, 7, 10, 2); var dmF = Ch(41, 2, 5, 9);
        var em7b5 = Ch(40, 4, 7, 10, 2); var fA = Ch(33, 5, 9, 0); var ebG = Ch(31, 3, 7, 10); var bare = Ch(38, 2, 9);

        (double Beats, MChord Chord)[] W(MChord c) => new[] { (4.0, c) };
        (double Beats, MChord Chord)[] H(MChord x, MChord y) => new[] { (2.0, x), (2.0, y) };
        var intro = new[] { W(dm), W(bbMaj7), W(gm), H(aSus, a) };
        var theme = new[] { W(dm), W(dmC), W(bbMaj7), H(aSus, a), W(gm), W(dmF), W(em7b5), H(aSus, a) };
        var bridge = new[] { W(bbMaj7), W(fA), W(gm), W(dm), W(bbMaj7), W(gm), W(aSus), W(a) };
        var coda = new[] { W(gm), W(dmF), W(ebG), H(aSus, a), W(dm), W(bare), W(bare) };

        var bars = new List<(string Sec, int Index, (double Beats, MChord Chord)[] Chords)>();
        void Section(string name, (double, MChord)[][] list) { for (int i = 0; i < list.Length; i++) bars.Add((name, i, list[i])); }
        Section("intro", intro); Section("a1", theme); Section("a2", theme); Section("b", bridge); Section("a3", theme); Section("coda", coda);
        int StartOf(string sec) => bars.FindIndex(b => b.Sec == sec) * bar;
        int end = bars.Count * bar;

        // How loud each bar is: a slow climb to the theme in octaves, then a long fall.
        double Loud(string sec, int i) => sec switch
        {
            "intro" => 0.5,
            "a1" => 0.56,
            "a2" => 0.64,
            "b" => 0.68 + i * 0.03,
            "a3" => i < 6 ? 0.94 : 0.94 - (i - 5) * 0.06,
            _ => Math.Max(0.38, 0.74 - i * 0.06),
        };

        var cello = new Notes(); var violin = new Notes(); var piano = new Notes();
        var viola = new Notes(); var celloSection = new Notes(); var contrabass = new Notes(); var choir = new Notes();

        // A tune from a tick on, as (note, beats); legato, each note running into the next.
        void Tune(Notes into, int from, (int Note, double Beats)[] line, double vel, int shift = 0, Func<int, double>? loudAt = null)
        {
            int at = from;
            foreach (var (note, beats) in line)
            {
                int len = (int)(beats * q);
                into.Add(at, note + shift, len, vel * (loudAt?.Invoke(at) ?? 1));
                at += len;
            }
        }
        double LoudAt(int tick) { var b = bars[Math.Min(bars.Count - 1, tick / bar)]; return Loud(b.Sec, b.Index); }

        // The theme (cello), the bridge tune (violin), and the lines that run under and after them.
        var themeLine = new (int, double)[]
        {
            (62, 3), (64, 1), (65, 2), (64, 1), (62, 1), (62, 2), (60, 1), (58, 1), (62, 2), (61, 2),
            (58, 3), (57, 1), (57, 2), (53, 1), (55, 1), (55, 2), (58, 1), (57, 1), (57, 4),
        };
        var bridgeLine = new (int, double)[]
        {
            (62, 2), (65, 2), (69, 3), (67, 1), (65, 2), (62, 2), (64, 2), (62, 2),
            (62, 1), (65, 1), (70, 2), (69, 2), (67, 2), (65, 2), (64, 2), (61, 3), (64, 1),
        };
        var celloUnderBridge = new (int, double)[] { (62, 4), (60, 4), (58, 4), (57, 4), (58, 4), (55, 4), (57, 4), (61, 4) };
        var celloCoda = new (int, double)[] { (55, 4), (53, 4), (51, 4), (49, 4), (50, 12) };
        var violinCoda = new (int, double)[] { (74, 4), (72, 4), (70, 4), (69, 4), (69, 12) };
        var pianoRemembers = new (int, double)[] { (70, 2), (69, 2), (65, 4), (67, 2), (63, 2), (64, 2), (61, 2), (62, 4) };

        Tune(cello, StartOf("a1"), themeLine, 92, loudAt: LoudAt);
        Tune(cello, StartOf("a2"), themeLine, 92, loudAt: LoudAt);
        Tune(cello, StartOf("b"), celloUnderBridge, 80, loudAt: LoudAt);
        Tune(cello, StartOf("a3"), themeLine, 92, loudAt: LoudAt);
        Tune(cello, StartOf("coda"), celloCoda, 88, loudAt: LoudAt);
        Tune(violin, StartOf("b"), bridgeLine, 86, shift: 12, loudAt: LoudAt);
        Tune(violin, StartOf("a3"), themeLine, 84, shift: 12, loudAt: LoudAt);
        Tune(violin, StartOf("coda"), violinCoda, 80, loudAt: LoudAt);
        Tune(piano, StartOf("coda"), pianoRemembers, 72, loudAt: LoudAt);

        int[] Lead(int[] prev, int[] pcs, (int Lo, int Hi)[] ranges) => VoiceLead(prev, pcs, ranges);
        void Hold(Notes into, int tick, int note, int len, double vel) => HoldNote(into, tick, note, len, vel);

        var violaRanges = new[] { (53, 62), (57, 67) };
        var celloRanges = new[] { (43, 55) };
        var choirRanges = new[] { (57, 64), (62, 69), (65, 74) };
        var pianoRanges = new[] { (55, 62), (59, 66), (62, 70) };
        int[] violaPrev = { 57, 62 }, celloPrev = { 50 }, choirPrev = { 60, 65, 69 }, pianoPrev = { 57, 62, 65 };

        int t = 0;
        foreach (var (sec, index, chords) in bars)
        {
            double loud = Loud(sec, index);
            int at = t;
            foreach (var (beats, chord) in chords)
            {
                int len = (int)(beats * q);

                // The floor: the double bass and the low cellos, from the first bar to the last.
                Hold(contrabass, at, chord.Bass, len, 62 * loud);
                celloPrev = Lead(celloPrev, chord.Pcs, celloRanges);
                Hold(celloSection, at, celloPrev[0], len, 58 * loud);

                // The violas join for the second time through the theme.
                if (sec != "intro" && sec != "a1")
                {
                    violaPrev = Lead(violaPrev, chord.Pcs, violaRanges);
                    foreach (int n in violaPrev) Hold(viola, at, n, len, 54 * loud);
                }

                // The choir, far back, from the middle of the bridge to the end, fading with the coda.
                if ((sec == "b" && index >= 4) || sec == "a3" || sec == "coda")
                {
                    choirPrev = Lead(choirPrev, chord.Pcs, choirRanges);
                    foreach (int n in choirPrev) Hold(choir, at, n, len, 46 * loud);
                }

                // The piano: the bass in octaves under every chord, and above it a toll, soft chords,
                // a slow arpeggio, or full chords, as the piece grows.
                int pb = chord.Bass;
                piano.Add(at, pb, len, 58 * loud);
                piano.Add(at, pb + 12, len, 50 * loud);
                pianoPrev = Lead(pianoPrev, chord.Pcs, pianoRanges);
                switch (sec)
                {
                    case "intro":
                    {
                        int top = Enumerable.Range(67, 10).First(n => chord.Pcs.Contains(n % 12));
                        piano.Add(at, top, len, 44 * loud);
                        break;
                    }
                    case "a1":
                        for (int k = 0; k < beats; k += 2)
                            foreach (int n in pianoPrev) piano.Add(at + k * q, n, 2 * q, 52 * loud);
                        break;
                    case "a2" or "b":
                    {
                        int[] order = { 0, 1, 2, 1 };
                        for (int k = 0; k < beats; k++)
                            piano.Add(at + k * q, pianoPrev[order[k % 4]], 2 * q, (k % 2 == 0 ? 56 : 48) * loud);
                        break;
                    }
                    case "a3":
                        for (int k = 0; k < beats; k += 2)
                        {
                            foreach (int n in pianoPrev) piano.Add(at + k * q, n, 2 * q, 60 * loud);
                            piano.Add(at + k * q, pianoPrev[^1] + 12, 2 * q, 54 * loud);
                        }
                        break;
                    default:
                        // Two soft notes under the remembered tune.
                        foreach (int n in pianoPrev.Take(2)) piano.Add(at, n, len, 40 * loud);
                        break;
                }
                at += len;
            }
            t += bar;
        }

        // The tempo: steady, then slowing through the coda to a stop.
        var tempo = new List<(int Tick, double Bpm)> { (0, bpm) };
        int slowFrom = StartOf("coda");
        int steps = (end - slowFrom) / q;
        for (int k = 1; k <= steps; k++) tempo.Add((slowFrom + k * q, bpm - (bpm - slowTo) * k / (double)steps));
        seconds = 0;
        for (int k = 0; k < tempo.Count; k++)
        {
            int to = k + 1 < tempo.Count ? tempo[k + 1].Tick : end;
            seconds += (to - tempo[k].Tick) / (double)Tpq * 60 / tempo[k].Bpm;
        }

        var tracks = new List<byte[]> { Conductor(p, tempo) };
        var parts = new (string Name, int Program, Notes Notes)[]
        {
            ("Melody: Cello, solo, legato (Presence XT)", 42, cello),
            ("High voice: Violin, solo, legato, quiet (Presence XT)", 40, violin),
            ("Piano: soft, with the sustain pedal (Presence XT)", 0, piano),
            ("Strings: Viola, legato, quiet in the mix (Presence XT)", 41, viola),
            ("Strings: Cello section, legato, quiet in the mix (Presence XT)", 42, celloSection),
            ("Bass: Contrabass, legato, quiet (Presence XT)", 43, contrabass),
            ("Choir: oohs, far back (Presence XT)", 52, choir),
        };
        int channel = 0;
        foreach (var (name, program, notes) in parts)
            tracks.Add(Track(name, channel == 9 ? ++channel : channel++, program, notes));

        using var ms = new MemoryStream();
        ms.Write("MThd"u8);
        Be32(ms, 6); Be16(ms, 1); Be16(ms, tracks.Count); Be16(ms, Tpq);
        foreach (var tr in tracks) ms.Write(tr);
        return ms.ToArray();
    }
}
