/// <summary>
/// The eighties, written out on their own as a proper synth pop track: A minor at 116 beats a minute,
/// a plucked arpeggio in sixteenths from the first bar, octave bass in eighths, a wide pad, a saw lead
/// with a hook, FM bells in the breakdown, and a drum machine with a big gated snare. Six tracks, like the
/// other decades: it is the liveliest of them, but it still plays behind the game.
/// Intro, verse, a pre chorus that builds, the chorus, a breakdown at half time, a snare roll, the last
/// chorus a whole step up, and an outro that ends on a held chord.
/// </summary>
static partial class MusicSketches
{
    /// <summary>Voices that move as little as they can from chord to chord, each within its range.</summary>
    private static int[] VoiceLead(int[] prev, int[] pcs, (int Lo, int Hi)[] ranges)
    {
        var result = new int[ranges.Length];
        var used = new HashSet<int>();
        for (int v = 0; v < ranges.Length; v++)
        {
            int best = -1; double bestScore = double.MaxValue;
            for (int n = ranges[v].Lo; n <= ranges[v].Hi; n++)
            {
                if (!pcs.Contains(n % 12)) continue;
                double score = Math.Abs(n - prev[v]) + (used.Contains(n % 12) ? 6 : 0);
                if (score < bestScore) { bestScore = score; best = n; }
            }
            result[v] = best;
            used.Add(best % 12);
        }
        return result;
    }

    /// <summary>A held note: the same note in the next chord is tied over, not played again.</summary>
    private static void HoldNote(Notes into, int tick, int note, int len, double vel)
    {
        for (int k = into.Count - 1; k >= 0; k--)
        {
            var n = into[k];
            if (n.Note == note && n.Tick + n.Len == tick) { into[k] = n with { Len = n.Len + len }; return; }
            if (n.Tick + n.Len < tick) break;
        }
        into.Add(tick, note, len, vel);
    }

    private static MChord Up(MChord c, int semitones) => semitones == 0 ? c : new(c.Bass + semitones, c.Pcs.Select(x => (x + semitones) % 12).ToArray());

    private static byte[] WriteEighties(Piece p, out double seconds)
    {
        const double bpm = 116;
        int q = Tpq, e = Tpq / 2, s = Tpq / 4, bar = 4 * q;

        // Pitch classes: C 0, D 2, E 4, F 5, G 7, G# 8, A 9, B 11. Bass notes between F1 and E2.
        var am = Ch(33, 9, 0, 4); var f = Ch(29, 5, 9, 0); var c = Ch(36, 0, 4, 7); var g = Ch(31, 7, 11, 2);
        var dm = Ch(38, 2, 5, 9); var em = Ch(40, 4, 7, 11); var eMaj = Ch(40, 4, 8, 11); var eSus = Ch(40, 4, 9, 11);

        var bars = new List<(string Sec, int Index, MChord Chord, int Key)>();
        void Section(string name, int key, params MChord[] chords) { for (int i = 0; i < chords.Length; i++) bars.Add((name, i, Up(chords[i], key), key)); }
        MChord[] Times(int n, params MChord[] loop) => Enumerable.Repeat(loop, n).SelectMany(x => x).ToArray();
        Section("intro", 0, Times(2, am, f, c, g));
        Section("verse", 0, Times(4, am, f, c, g));
        Section("pre", 0, dm, em, f, g, dm, em, f, eMaj);
        Section("chorus", 0, Times(2, f, g, c, am, f, g, am, am));
        Section("breakdown", 0, Times(2, f, g, c, am));
        Section("build", 0, f, g, eSus, eMaj);
        Section("chorus2", 2, Times(2, f, g, c, am, f, g, am, am));
        Section("outro", 2, Times(2, am, f, c, g));
        Section("end", 2, am, am);
        int StartOf(string sec) => bars.FindIndex(b => b.Sec == sec) * bar;
        int end = bars.Count * bar;

        var lead = new Notes(); var bells = new Notes(); var arp = new Notes();
        var pad = new Notes(); var bass = new Notes(); var drums = new Notes();

        void Tune(Notes into, int from, (int Note, double Beats)[] line, double vel, int shift = 0)
        {
            int at = from;
            foreach (var (note, beats) in line)
            {
                int len = (int)(beats * q);
                if (note > 0) into.Add(at, note + shift, len - 15, vel + (at % bar == 0 ? 6 : 0));
                at += len;
            }
        }

        // The hook (chorus), the verse tune and the pre chorus that climbs to it. A4 is 69, C5 72, E5 76.
        var hook = new (int, double)[]
        {
            (69, .5), (72, .5), (76, 1), (74, 1), (72, 1),   (74, 1.5), (71, .5), (67, 2),
            (67, .5), (72, .5), (76, 1), (74, 1), (72, 1),   (72, 1.5), (71, .5), (69, 2),
            (69, .5), (72, .5), (77, 1.5), (76, .5), (74, 1), (74, 1), (76, .5), (74, .5), (71, 2),
            (72, 1), (71, .5), (69, .5), (76, 2),            (74, .5), (72, .5), (71, 1), (69, 2),
        };
        var hookFirstHalf = hook.Take(16).ToArray();
        var verse = new (int, double)[]
        {
            (0, 1), (64, .5), (69, .5), (71, .5), (72, .5), (71, 1),   (69, 1.5), (67, .5), (69, 2),
            (0, 1), (64, .5), (67, .5), (72, 1), (71, .5), (67, .5),   (71, 1), (69, .5), (67, 2.5),
            (0, 1), (64, .5), (69, .5), (71, .5), (72, .5), (71, 1),   (72, 1.5), (69, .5), (65, 2),
            (0, 1), (64, .5), (67, .5), (72, 1), (74, .5), (72, .5),   (71, .5), (72, .5), (74, 3),
        };
        var pre = new (int, double)[]
        {
            (65, 1), (69, 1), (74, 2), (67, 1), (71, 1), (76, 2), (69, 1), (72, 1), (77, 2), (74, 2), (71, 2),
            (65, 1), (69, 1), (74, 2), (67, 1), (71, 1), (76, 2), (72, 1), (74, 1), (76, 1), (77, 1), (76, 4),
        };
        var build = new (int, double)[] { (72, 4), (74, 4), (76, 4), (76, 2), (80, 2) };

        Tune(lead, StartOf("verse"), verse, 84);
        Tune(lead, StartOf("verse") + 8 * bar, verse, 88);
        Tune(lead, StartOf("pre"), pre, 92);
        Tune(lead, StartOf("chorus"), hook, 104);
        Tune(lead, StartOf("chorus") + 8 * bar, hook, 106);
        Tune(lead, StartOf("build"), build, 96);
        Tune(lead, StartOf("chorus2"), hook, 108, 2);
        Tune(lead, StartOf("chorus2") + 8 * bar, hook, 110, 2);
        Tune(lead, StartOf("outro"), verse, 80, 2);
        lead.Add(StartOf("end"), 69 + 2, 2 * bar - 30, 76);
        Tune(bells, StartOf("breakdown"), hookFirstHalf, 82, 12);
        Tune(bells, StartOf("breakdown") + 4 * bar, hookFirstHalf, 88, 12);

        // Harmony and rhythm, bar by bar.
        int[] padPrev = { 57, 60, 64, 69 };
        var padRanges = new[] { (52, 60), (57, 64), (60, 67), (64, 72) };
        int t = 0;
        foreach (var (sec, i, chord, key) in bars)
        {
            int root = chord.Bass;
            bool chorus = sec is "chorus" or "chorus2";

            // The pad, all the way through.
            padPrev = VoiceLead(padPrev, chord.Pcs, padRanges);
            foreach (int n in padPrev) HoldNote(pad, t, n, bar, chorus ? 70 : sec == "end" ? 64 : 58);

            // The arpeggio: root, third, fifth and octave above middle C, in sixteenths (eighths in the breakdown).
            int r = Enumerable.Range(57, 12).First(n => n % 12 == chord.Pcs[0]);
            int[] tones = { r, r + (chord.Pcs[1] - chord.Pcs[0] + 12) % 12, r + (chord.Pcs[2] - chord.Pcs[0] + 12) % 12, r + 12 };
            int[] order = { 0, 1, 2, 3, 2, 3, 1, 2 };
            if (sec == "end") HoldNote(arp, t, tones[0], bar, 60);
            else
            {
                int step = sec == "breakdown" ? e : s;
                for (int k = 0; k < bar / step; k++)
                    arp.Add(t + k * step, tones[order[k % 8]] + (sec is "chorus" or "chorus2" or "build" ? 12 : 0), step - 20, (k % 4 == 0 ? 78 : 62) + (chorus ? 8 : 0));
            }

            // The bass: octaves in eighths, roots in eighths when it builds, long notes when it rests.
            switch (sec)
            {
                case "intro" when i < 4:
                    break;
                case "intro" or "pre" or "build":
                    for (int k = 0; k < 8; k++) bass.Add(t + k * e, root, e - 30, k % 2 == 0 ? 100 : 86);
                    break;
                case "breakdown" or "end":
                    HoldNote(bass, t, root, bar, 96);
                    break;
                case "outro" when i >= 4:
                    HoldNote(bass, t, root, bar, 90);
                    break;
                default:
                    for (int k = 0; k < 8; k++) bass.Add(t + k * e, root + (k % 2 == 1 ? 12 : 0), e - 30, (k % 2 == 0 ? 104 : 90) + (chorus ? 8 : 0));
                    break;
            }

            Drums(sec, i, t);
            t += bar;
        }

        // A drum machine: kick 36, snare 38, clap 39, closed hat 42, open hat 46, crash 49, toms 41 to 50.
        void Hit(int tick, int note, double vel) => drums.Add(tick, note, e - 20, vel);
        void Roll(int from, int count, int step, double fromVel, double toVel)
        {
            for (int k = 0; k < count; k++) Hit(from + k * step, 38, fromVel + (toVel - fromVel) * k / Math.Max(1, count - 1));
        }
        void Toms(int from, int count)
        {
            int[] toms = { 50, 48, 47, 45, 43, 41 };
            for (int k = 0; k < count; k++) Hit(from + k * s, toms[k * toms.Length / count], 100 + k * 2);
        }
        void Drums(string sec, int i, int t0)
        {
            switch (sec)
            {
                case "intro" when i < 4:
                    return;
                case "intro":
                    for (int b = 0; b < 4; b++) Hit(t0 + b * q, 36, 108);
                    for (int k = 0; k < 8; k++) Hit(t0 + k * e, 42, k % 2 == 0 ? 70 : 52);
                    if (i == 7) Roll(t0 + 2 * q, 8, s, 70, 118);
                    return;
                case "verse":
                    if (i == 0) Hit(t0, 49, 104);
                    Hit(t0, 36, 112); Hit(t0 + 2 * q, 36, 104); Hit(t0 + 2 * q + e, 36, 92);
                    Hit(t0 + q, 38, 112); Hit(t0 + 3 * q, 38, 114);
                    for (int k = 0; k < 8; k++) Hit(t0 + k * e, 42, k % 2 == 0 ? 74 : 56);
                    if (i == 15) Toms(t0 + 3 * q, 4);
                    return;
                case "pre":
                    for (int b = 0; b < 4; b++) Hit(t0 + b * q, 36, 110);
                    if (i == 7) { Roll(t0, 16, s, 64, 124); return; }
                    Hit(t0 + q, 38, 112); Hit(t0 + 3 * q, 38, 114);
                    for (int k = 0; k < 16; k++) Hit(t0 + k * s, 42, k % 4 == 0 ? 76 : k % 2 == 0 ? 62 : 48);
                    return;
                case "chorus" or "chorus2":
                    if (i % 8 == 0) Hit(t0, 49, 116);
                    for (int b = 0; b < 4; b++) { Hit(t0 + b * q, 36, 116); Hit(t0 + b * q, 42, 64); Hit(t0 + b * q + e, 46, 82); }
                    Hit(t0 + q, 38, 120); Hit(t0 + q, 39, 96); Hit(t0 + 3 * q, 38, 122); Hit(t0 + 3 * q, 39, 98);
                    if (i == 15 && sec == "chorus") Toms(t0 + 2 * q, 8);
                    return;
                case "breakdown":
                    Hit(t0, 36, 104); Hit(t0 + 2 * q, 39, 92);
                    return;
                case "build":
                    for (int b = 0; b < 4; b++) Hit(t0 + b * q, 36, 100 + i * 5);
                    if (i < 2) Roll(t0, 8, e, 70 + i * 16, 86 + i * 16);
                    else Roll(t0, 16, s, 90 + (i - 2) * 16, 106 + (i - 2) * 18);
                    return;
                case "outro" when i < 4:
                    if (i == 0) Hit(t0, 49, 104);
                    Hit(t0, 36, 108); Hit(t0 + 2 * q, 36, 100); Hit(t0 + 2 * q + e, 36, 88);
                    Hit(t0 + q, 38, 106); Hit(t0 + 3 * q, 38, 108);
                    for (int k = 0; k < 8; k++) Hit(t0 + k * e, 42, k % 2 == 0 ? 70 : 52);
                    return;
                case "outro":
                    Hit(t0, 36, 96 - (i - 4) * 8);
                    Hit(t0 + 2 * q, 39, 80 - (i - 4) * 8);
                    return;
                case "end" when i == 0:
                    Hit(t0, 49, 110); Hit(t0, 36, 110);
                    return;
            }
        }

        var tempo = new List<(int Tick, double Bpm)> { (0, bpm) };
        seconds = end / (double)Tpq * 60 / bpm;

        var tracks = new List<byte[]> { Conductor(p, tempo) };
        var parts = new (string Name, int Program, Notes Notes)[]
        {
            ("Lead: bright saw lead, a touch of glide (Mai Tai)", 81, lead),
            ("Bells: FM bells or bright e-piano, breakdown only (Presence XT)", 11, bells),
            ("Arpeggio: short pluck, 16ths, with delay (Mai Tai)", 84, arp),
            ("Pad: wide warm pad, slow attack (Mai Tai)", 89, pad),
            ("Bass: punchy octave bass (Mojito)", 38, bass),
        };
        int channel = 0;
        foreach (var (name, program, notes) in parts)
            tracks.Add(Track(name, channel == 9 ? ++channel : channel++, program, notes));
        tracks.Add(Track("Drums: 80s drum machine, big gated snare (Impact XT)", 9, 0, drums));

        using var ms = new MemoryStream();
        ms.Write("MThd"u8);
        Be32(ms, 6); Be16(ms, 1); Be16(ms, tracks.Count); Be16(ms, Tpq);
        foreach (var tr in tracks) ms.Write(tr);
        return ms.ToArray();
    }
}
