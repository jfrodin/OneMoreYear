# Musiken i One More Year

Underlag för inspelning i Studio One, bara med det som följer med programmet. Till varje låt finns ett
färdigt arrangemang i `docs/music/midi/`, 2,5 till 3,5 minuter långt, med ett spår per instrument:
melodi, andrastämma, ackord, matta (stråkmattor uppdelade på violin, viola och cello), bas och
trummor där det passar. Spåren heter som instrumentet
de ska spelas på. Dra in filen i Studio One och välj ljuden; allt annat finns redan i filen.

Skisserna görs om med `SimRunner --midi=docs/music/midi` om vi ändrar något.

---

## Det gemensamma

**Känslan:** ett familjealbum man bläddrar i. Varmt, nära, lite vemodigt, aldrig sorgligt. Ett piano i
ett vardagsrum, inte i en konserthall.

**Tumregler för alla låtar**
- Tempo 60 till 90 bpm, ingen stark rytm. Någon läser medan det spelar.
- Ett eller två instrument bär, resten är luft. Hellre för lite än för mycket.
- Nära inspelat ljud med lite rum. Hamrarna i pianot och fingrarna på strängarna får höras.
- Inga plötsliga crescendon.
- 2 till 3 minuter. Låten behöver inte loopa: spelet lägger in tystnad mellan låtarna själv. Låt
  slutet klinga ut.

**Leverans**
- WAV, 44,1 kHz, 24 bitar. Jag gör om till OGG.
- Ungefär −18 LUFS integrerat, toppar under −1 dBTP.
- Filnamnen nedan (`title.wav`, `1950s.wav` och så vidare).

**Studio Ones egna instrument som används**
- **Presence XT:** piano, elpiano, stråkar, cello, klarinett, flöjt, gitarrer, bas, klockspel och kör.
  Presetnamnen skiljer sig mellan versioner av ljudbiblioteket; leta i kategorierna (Keyboards,
  Strings, Woodwinds, Guitars, Bass, Mallets, Choir).
- **Mai Tai:** mattor (pads), mjuka syntljud och arpeggion.
- **Mojito:** enkel syntbas och syntmelodi.
- **Impact XT:** trummor, bara mycket försiktigt (vispar, mjuk kick).
- **Sample One XT:** egna ljud, till exempel regn, ett rum, någon som nynnar.

**Effekter:** Room Reverb eller Open AIR för rummet, Analog Delay för 80-talet, Pro EQ för att ta bort
mullret i botten.

### Var hittar jag instrumenten?

Öppna webbläsaren till höger (F5) och fliken **Instruments**. Under **PreSonus** finns Presence XT,
Mai Tai, Mojito, Impact XT och Sample One XT. Fäll ut ett instrument för att se dess presets, eller
skriv sökordet i sökrutan överst i webbläsaren: det är det säkraste sättet, eftersom mappar och
presetnamn skiljer sig mellan versioner och ljudbibliotek. Dra presetet till ett tomt område i
arrangemanget så skapas ett spår med det.

| Instrument i underlaget | Insticksprogram | Sök efter | Om det saknas |
|---|---|---|---|
| Piano | Presence XT | piano, grand, upright | Det finns i alla versioner |
| Elpiano (Rhodes) | Presence XT | electric piano, e-piano, EP | Ett mjukt keys-preset i Mai Tai |
| Violin, viola (stråkarna) | Presence XT | violin, viola | En långsam pad i Mai Tai |
| Cello | Presence XT | cello | Stråkarna, spelade lågt |
| Klarinett | Presence XT | clarinet | Flöjt, eller ett mjukt lead i Mai Tai |
| Flöjt | Presence XT | flute | Ett mjukt lead i Mai Tai |
| Nylongitarr | Presence XT | nylon, classical guitar | Akustisk gitarr |
| Akustisk gitarr | Presence XT | acoustic guitar, steel | Nylongitarr |
| Kontrabas | Presence XT | upright bass, acoustic bass | Elbas, mjukt |
| Elbas | Presence XT | bass, electric bass, finger bass | Bas i Mojito |
| Klockspel, celesta, vibrafon | Presence XT | bell, celesta, vibraphone, mallet | Ett bell-preset i Mai Tai |
| Kör | Presence XT | choir, voices, aah | En luftig pad i Mai Tai |
| Mattor (pads) | Mai Tai | pad | |
| Arpeggio | Mai Tai | arp | Spela in sextondelarna själv, se MIDI |
| Syntbas | Mojito | bass | Bas i Mai Tai |
| Syntmelodi (lead) | Mojito | lead | Ett lead i Mai Tai |
| Vispar, mjukt trumset | Impact XT | brush, jazz kit, soft | Spela bara en mjuk kick |
| Egna ljud (regn, rum, nynnande) | Sample One XT | | Spela in med mikrofonen, dra in ljudfilen |

---

## Familjetemat

Åtta takter som återkommer i varje låt, i nya kläder. Det ska gå att nynna. Här i C-dur:

| Takt | Ackord | Melodi | Rytm |
|---|---|---|---|
| 1 | C | E G C' | fjärdedel, fjärdedel, halvnot |
| 2 | G/B | B A G | punkterad fjärdedel, åttondel, halvnot |
| 3 | Am | A C' E' D' | fyra fjärdedelar |
| 4 | F | C' | punkterad halvnot, paus |
| 5 | C | E G C' D' | fyra fjärdedelar |
| 6 | G | E' D' B | halvnot, fjärdedel, fjärdedel |
| 7 | Fmaj7 | A C' E' D' | fyra fjärdedelar |
| 8 | C | C' | helnot |

(C' och E' är oktaven över.)

**Mellanspelet (B)**, utan melodi eller med en fri improvisation över:
Am, Em, F, C, Dm, G, Em, Gsus4

**Form för alla låtar (så är MIDI-filerna byggda):**
1. Intro, 4 takter, hållna ackord
2. Temat, enkelt och glest
3. Temat med små utsmyckningar och en andrastämma under
4. Mellanspelet, med en egen melodi
5. Temat igen, högre och fylligare, med matta där det finns
6. Ett lugnt mellanspel, där andrastämman tar mellanspelets melodi
7. Temat avskalat (de långsammaste låtarna hoppar över den här och/eller del 6)
8. Avslut som saktar in, och ett slutackord som klingar ut

Varje spår i MIDI-filen heter som instrumentet du ska lägga på, till exempel "Melody: Clarinet
(Presence XT)". Tempot saktar in av sig självt på slutet (tempospåret följer med i filen).

---

## title.wav: Titelskärmen

- **När:** på titelskärmen och första gången spelet startar. Spelets "ansikte".
- **Känsla:** varmt och hoppfullt, som att öppna ett album man inte sett på länge.
- **Tonart och tempo:** C-dur, 72 bpm, 4/4.
- **Instrument:** piano (Presence XT) bär allt. Stråkar (Presence XT, Strings) kommer in mjukt i andra
  temat, långt bak i mixen.
- **Spela:** vänster hand grundton, höger hand brutna ackord i åttondelar (se MIDI). Temat i andra
  varvet en oktav upp, med stråkarna som håller ackorden under.
- **Ackord:** Intro C, F, C, Gsus4. Tema: C, G/B, Am, F, C, G, Fmaj7, C. Avslut F, Gsus4, C (håll).

## memoriam.wav: In memoriam

- **När:** när en person dör och du väljer vem som går vidare.
- **Känsla:** stilla sorg, men med värme. Ett tack, inte en tragedi.
- **Tonart och tempo:** a-moll, 60 bpm, 4/4.
- **Instrument:** piano och cello (Presence XT). Inget annat.
- **Spela:** långa ackord, sparsamt. Cellon tar temat i andra varvet. Låt det finnas tystnad mellan
  fraserna.
- **Melodi (temat i moll):** C E A | G F E | F A C' B | A | C E A B | C' B G# | F A C' B | A
- **Ackord:** Tema: Am, Em/G, F, Dm, Am, E, F, Am. Mellanspel: F, C, Dm, Am, Dm, Em/G, F, E.

## 1950s.wav: Femtiotalet

- **När:** när året är 1950 till 1959.
- **Känsla:** sepia, söndagseftermiddag, en radio i köket.
- **Tonart och tempo:** F-dur, 76 bpm, **3/4 (vals)**.
- **Instrument:** klarinett tar melodin, piano ackompanjerar, kontrabas (Presence XT, akustisk bas).
  Eventuellt vispar på en virvel i Impact XT, mycket tyst.
- **Spela:** vals, "bom-tjing-tjing": bas på ettan, ackord på två och tre.
- **Melodi:** A C F | E D C | D F A | G F | A C F | A G | D F A | F
- **Ackord:** F, C/E, Dm, Bb, F, C, Bbmaj7, F. Mellanspel: Dm, Am, Bb, F, Gm, C, Am, Csus4.

## 1960s.wav: Sextiotalet

- **När:** 1960 till 1969.
- **Känsla:** folkvisa, sommar, en gitarr på en trappa.
- **Tonart och tempo:** G-dur, 84 bpm, 4/4.
- **Instrument:** akustisk gitarr eller nylongitarr som fingerplockas (Presence XT, Guitars), flöjt
  tar melodin, lätta stråkar i andra varvet.
- **Spela:** "Travis-plock": växelbas med tummen på taktslagen, ackordtoner emellan (se MIDI).
- **Melodi:** B D G | F# E D | E G B A | G | B D G A | B A F# | E G B A | G
- **Ackord:** G, D/F#, Em, C, G, D, Cmaj7, G. Mellanspel: Em, Bm, C, G, Am, D, Bm, Dsus4.

## 1970s.wav: Sjuttiotalet

- **När:** 1970 till 1979.
- **Känsla:** varmt och brunt, lite soul, en lugn kväll.
- **Tonart och tempo:** D-dur, 80 bpm, 4/4.
- **Instrument:** elpiano av Rhodes-typ (Presence XT, Electric Piano) med lite tremolo, elbas,
  flöjt på melodin. Ett mjukt trumkomp i Impact XT går bra: kick på ettan, vispar, inget mer.
- **Spela:** ackord på ettan och på "och" efter tvåan, bas som går grundton, kvint, oktav.
- **Melodi:** F# A D | C# B A | B D F# E | D | F# A D E | F# E C# | B D F# E | D
- **Ackord:** D, A/C#, Bm, G, D, A, Gmaj7, D. Mellanspel: Bm, F#m, G, D, Em, A, F#m, Asus4.

## 1980s.wav: Åttiotalet

- **När:** 1980 till 1989.
- **Känsla:** neon, men stilla. En kväll vid ett fönster med stadens ljus.
- **Tonart och tempo:** a-moll, 90 bpm, 4/4.
- **Instrument:** Mai Tai för en bred matta med långsam attack och ett sextondelsarpeggio. Mojito
  för syntbasen i åttondelar. Melodin på ett klockljud (Presence XT, Mallets) eller en mjuk
  Mojito-lead. Mycket reverb, och Analog Delay på arpeggiot.
- **Spela:** mattan håller ackorden, arpeggiot går uppåt i sextondelar, basen pulserar.
- **Melodi:** samma som In memoriam (temat i moll).
- **Ackord:** Am, Em/G, F, Dm, Am, E, F, Am. Mellanspel: F, C, Dm, Am, Dm, Em/G, F, E.

## 1990s.wav: Nittiotalet

- **När:** 1990 till 1999.
- **Känsla:** öppet, lite melankoliskt, en bilresa med vindrutetorkare.
- **Tonart och tempo:** E-dur, 78 bpm, 4/4.
- **Instrument:** piano på melodin, akustisk stålsträngad gitarr som slår ackord, en tunn Mai
  Tai-matta under, elbas.
- **Spela:** gitarren slår ned, ned-upp, upp-ned-upp (se MIDI). Pianot spelar temat enkelt, utan
  utsmyckning.
- **Melodi:** G# B E | D# C# B | C# E G# F# | E | G# B E F# | G# F# D# | C# E G# F# | E
- **Ackord:** E, B/D#, C#m, A, E, B, Amaj7, E. Mellanspel: C#m, G#m, A, E, F#m, B, G#m, Bsus4.

## 2000s.wav: Tvåtusentalet

- **När:** 2000 till 2009.
- **Känsla:** avskalat och intimt. Bara ett piano i ett tyst rum.
- **Tonart och tempo:** C-dur, 70 bpm, 4/4.
- **Instrument:** bara piano. Gärna ett mjukt, nära ljud. Lite pedalljud och rumsbrus får vara kvar.
- **Spela:** långa ackord i vänster hand, temat i höger, och en ensam ton högt upp som svarar.
- **Melodi och ackord:** som titellåten.

## 2010s.wav: Tjugotiotalet

- **När:** 2010 till 2019.
- **Känsla:** filmiskt men stilla. En stad i skymning.
- **Tonart och tempo:** Bb-dur, 66 bpm, 4/4.
- **Instrument:** piano, stråkar som sväller långsamt in, cello på basen.
- **Spela:** brutna ackord i pianot, stråkarna kommer först i mellanspelet och bär det sista temat.
- **Melodi:** D F Bb | A G F | G Bb D C | Bb | D F Bb C | D C A | G Bb D C | Bb
- **Ackord:** Bb, F/A, Gm, Eb, Bb, F, Ebmaj7, Bb. Mellanspel: Gm, Dm, Eb, Bb, Cm, F, Dm, Fsus4.

## 2020s.wav: Tjugotjugotalet

- **När:** 2020 till 2029.
- **Känsla:** nutid: lite elektroniskt men mänskligt.
- **Tonart och tempo:** D-dur, 72 bpm, 4/4.
- **Instrument:** piano och en mjuk Mai Tai-matta. Sample One XT för en egen textur: spela in något
  vardagligt (en kaffebryggare, regn mot ett fönster, någon som nynnar temat) och lägg det långt
  bak. Ett baklänges pianoackord inför andra temat är snyggt.
- **Melodi och ackord:** som sjuttiotalet (D-dur), men pianot bär allt.

## future.wav: Framtiden (2030 och framåt)

- **När:** från 2030 och resten av spelet.
- **Känsla:** svävande och lite främmande, men varmt. Något man inte riktigt känner igen.
- **Tonart och tempo:** C-dur, 60 bpm, 4/4. Testa att höja F till F# här och var: det ger en
  drömsk, "lydisk" känsla.
- **Instrument:** Mai Tai-mattor med lång attack och lång release, klockspel eller celesta (Presence
  XT, Mallets) på temat i mycket långa toner, en kör (Presence XT, Choir) långt bak.
- **Melodi och ackord:** som titellåten, men melodin i halva tempot.

---

## Om du vill börja litet

1. `title.wav`
2. `memoriam.wav`
3. `1950s.wav`, `1970s.wav` och `2000s.wav`, de som skiljer sig mest

Resten när du hört hur de första känns i spelet.
