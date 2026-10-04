# Roadmap

*Senast uppdaterad 2026-10-04 (version 0.69). Lever: uppdateras när en milstolpe nås eller planen ändras.*

## 1. Var vi är nu

One More Year är ett generationsspel: du lever ett liv år för år, och när du dör tar ett barn över.
Tekniskt och innehållsmässigt är grunden bred.

**Det som finns och fungerar**
- Två länder (Sverige och USA) från 1950 till långt in i framtiden, med emigration mellan dem.
- Cirka 1 180 händelser, med grupper som hindrar upprepningar, historiska ögonblick, epoker och kapitel.
- Liv från födsel till död: skola (ämne och kompisgäng), utbildning, 34 yrkesspår, kreativa och
  politiska karriärer med rykte, kärlek, äktenskap, barn, uppfostran, vänner, husdjur, sjukdom, åldrande.
- Ekonomi: lön, skatt, levnadskostnader, bostad och lån, fonder och aktier, eget företag, hyreshem,
  renoveringar, sommarstuga, arvegods, gåvor i släktens namn, släktgods.
- Mörkare teman bakom innehållsinställningar: missbruk, psykisk ohälsa, otrohet, våld, brott och fängelse.
- Långsiktigt: livsdrömmar, årets önskan, släktens rykte och släktdrag, myter, prestationer (cirka 45),
  epilog och familjearkiv, fotoalbum.
- Genererade porträtt som ärver drag, åldras och följer mode och yrke.
- Verktyg: speltester med F1, simuleringsrapporter, sparfilskontroller, automatiska tester.

**Det som håller tillbaka spelet just nu** (från speltesterna)
- **Hur val känns.** Sidan hoppar, och ett val krymper till en blå mening utan synlig konsekvens. Det
  känns som en hemsida, inte ett spel. Det är den största enskilda bristen.
- **Text som inte passar sammanhanget.** Fel ålder, kön, antal syskon eller situation (bilen man inte
  äger, sjuksköterskan som är tolv). Varje sådan rad bryter illusionen.
- **Ekonomin är svår att förstå och styra.** Levnadsstandard väljs inte, pengar flyttas automatiskt.
- **Ingen introduktion.** En ny spelare möter allt på en gång.
- **Tyst och ikonlöst.** Nästan inga ljud, ingen musik, inga ikoner.

## 2. Del för del: vad som behöver byggas ut

Varje del har ett nuläge, ett mål och en bedömning av hur viktig den är för en release.

| Del | Nu | Mål | Före release? |
|---|---|---|---|
| **Val och händelser (kärnan)** | Kort text, valet försvinner | Valet syns kvar med vad som hände och vad det gav (+pengar, ±lycka, nya drag). Stabil layout, inga hopp. Lugn takt mellan händelser. | **Måste** |
| **Språk och sammanhang** | Mycket bra text, men glapp | En genomgång av alla 1 180 händelser: ålder, kön, familj, ägodelar, epok. Automatiska kontroller där det går. | **Måste** |
| **Introduktion** | Ingen | De tre första åren som en mjuk guide: en sak i taget, korta tips, valfritt att stänga av. | **Måste** |
| **Förklaringar** | Lite | Vad hälsa, lycka, smarthet med mera gör, synligt där de visas. Ord som "the record years" förklaras. | **Måste** |
| **Ekonomi** | Djup men styr sig själv | Välj levnadsstandard (snål, vanlig, bekväm, lyx), se varför pengar går upp och ner, köpa in sig i partnerns hem. | **Måste** |
| **Brott och straff** | Platt | Återfall ger hårdare straff, olika per land, rykte i kriminella kretsar, livet efter fängelset. | Bör |
| **Relationer** | Bra grund | Partners och familj reagerar på varandras kriser (missbruk, fängelse, otrohet), inte bara på spelaren. | Bör |
| **Ljud och musik** | Nästan inget | Lugn musik som byter med årtiondet, ljud för val, nytt år, födsel, död. | **Måste** |
| **Ikoner och utseende** | Text och färger | Ikoner för värden, flikar och handlingar. Val av tema: växlande med åren eller en fast stil. | **Måste** |
| **Porträtt** | Nyligen omgjorda | Ansiktsuttryck i händelser, fler klädstilar per epok. | Kan vänta |
| **Sparfiler** | Tre platser | Fler platser (eller obegränsat), tydlig lista. | **Måste** |
| **Fler länder** | Sverige, USA | Storbritannien eller Tyskland som tredje. | Efter release |
| **Översättning** | Engelska | Svenska först, sedan fler. | Efter release |
| **Steam** | Inget | Prestationer kopplade till Steam, molnsparning, butikssida, handkontroll. | **Måste** för Steam |
| **Städning** | Testverktyg kvar | Testscenarier, F1 och utvecklarflaggor bort ur releasebygget. | **Måste** |

## 3. Milstolpar

**M1. Spelbar alfa** (nu → cirka 3 veckor)
- Allt i den öppna speltestlistan rättat.
- Valen omgjorda (synlig konsekvens, stabil layout).
- Språk- och sammanhangsgenomgång, plus automatiska kontroller.
- Introduktion och förklaringar.
- **Mål:** ett stängt test med 5–15 utomstående via itch.io och Discord. Vi tittar på var de fastnar,
  vad de inte förstår och hur länge de spelar.

**M2. Beta** (cirka 4–6 veckor efter M1)
- Ljud och musik, ikoner, temaval.
- Ekonomin styrbar, fler sparplatser.
- Balans från alfatestets data (hur ofta man dör, blir rik, slår igenom).
- Brott och relationer utbyggda.
- Steams butikssida öppnas för önskelistor ("wishlist") så tidigt som möjligt.
- **Mål:** ett större test (30–100 personer), inga kända krascher eller trasiga sparfiler.

**M3. Release** (när kvalitetskraven nedan är uppfyllda, inte på ett datum)

## 3b. Ordning för mest värde (beslutad 2026-10-04)

Först det som avgör om någon fortsätter spela efter tio minuter, sedan djup.

1. **Introduktion.** Tips en i taget när de blir användbara (klart i 0.71, justeras efter test).
2. **Steams butikssida**, parallellt och tidigt: önskelistor samlas över tid.
3. **Ljud, musik och ikoner.** Första versionen i 0.71 (musik och ljud skrivna i koden, kan bytas mot inspelningar).
4. **Stängt test** med utomstående (itch.io, Discord). Fråga producenten innan bygget.
5. **Balans och innehåll** där testarna faktiskt spelar; tre generationer utan upprepningar.
6. **Steam-krav och städning:** prestationer, molnsparning, handkontroll; F1, testverktyg och
   karaktärsskaparens spärr ur releasebygget.
7. **Early Access.**
8. **Efter:** svensk översättning, tredje land, karaktärsskaparen som liten DLC.

**Ljud och musik: sparsamt.** Spelet läses, som Football Manager eller Crusader Kings, och ljudet
ska bära stämningen, inte ta plats.
- Gränssnittsljud bara där något *händer*: val, nytt år, sidvändning, tidningen. Inga ljud på
  hovring eller vanliga knappar utöver ett mycket svagt klick.
- Några få känslomässiga ögonblick får ett eget kort ljud: en födsel, en död, ett bröllop, en
  examen. Mer än så blir tröttsamt efter tio år i spelet.
- Musik: lugn, instrumental och lågt mixad, ett eller två spår per epok (piano, gitarr, stråkar,
  sedan syntar på 80-talet och så vidare), med långa tystnader mellan spåren. Egen volym i
  inställningarna, och lätt att stänga av.
- Musiken köps som licensierade paket eller beställs av en kompositör. Ingen AI-genererad musik.

## 4. När vågar vi släppa?

Spelet släpps när alla de här är sanna, inte tidigare:
1. En ny spelare förstår de första tio minuterna utan hjälp (testat på minst fem utomstående).
2. Inga kända krascher, och sparfiler överlever varje uppdatering.
3. Språkgenomgången är klar och testarna hittar sällan text som inte passar.
4. Man kan spela tre generationer utan att tycka att händelser upprepas.
5. Ljud, musik och ikoner finns.
6. Steams krav är uppfyllda (prestationer, molnsparning, butikssida med trailer och bilder).

**Min rekommendation: Early Access på Steam först.** Spelet är innehållsdrivet och blir bättre av
spelares återkoppling. Early Access sänker förväntningarna på "färdigt", ger en publik som rapporterar,
och låter oss släppa länder och innehåll löpande. Version 1.0 kommer 6–12 månader senare.

## 5. Efter release: bygga vidare eller bara rätta buggar?

**Bygga vidare, men planerat.** Ett spel som det här lever på att det kommer nytt: nya länder, nya
händelser, nya epoker. Samtidigt får det inte bli ett projekt utan slut.

Förslag:
- **De första tre månaderna:** buggfixar och balans varje vecka eller varannan vecka. Lyssna på
  spelarna.
- **Var tredje månad:** en innehållsuppdatering med ett tema, till exempel "Storbritannien",
  "Krig och fred", "Kärlek" eller "Svensk översättning". Gratis i Early Access.
- **Vid 1.0:** spelet är komplett i sig. Efter det kan större tillägg (fler länder, eller en helt ny
  epok som 1800-talet) bli betalda tillägg om spelet säljer, annars avslutas utvecklingen med en sista
  stabil version.
- **Varje uppdatering** ska vara bakåtkompatibel med gamla sparfiler.

## 6. Öppna beslut för producenten

- Early Access eller direkt 1.0?
- Pris (förslag: 10–15 euro i Early Access).
- Plattform: bara PC först, eller även mobil senare? Mobil passar genren men kräver ett eget
  gränssnitt.
- Namn och butikssida: när öppnar vi för önskelistor?
- Vilket land kommer tredje?
