# ONE MORE YEAR

**Kravspecifikation och spelkoncept**

*Generationsbaserad livs- och familjesimulator*

> "Live a life. Build a family. Leave a legacy."

---

## 1. Produktvision

One More Year är ett text- och UI-drivet premiumspel där spelaren följer en familj genom flera generationer. Spelet kombinerar den direkta, lättbegripliga livsloopen från life simulators med den emergenta person-, relations- och dynastisimuleringen från grand strategy och managementspel.

Den långsiktiga progressionen är inte en enskild karaktär. Familjen är progressionen. När en spelbar person dör fortsätter världen och spelaren tar över en annan familjemedlem med ett redan existerande liv, relationer, ekonomi, historia och konflikter.

Målet är inte att vinna på traditionellt sätt, utan att skapa och uppleva en unik familjehistoria över många decennier och generationer.

### 1.1 Designlöfte

- Lätt att börja spela men svårt att fullständigt optimera.
- Systemdrivet snarare än beroende av mängder av handskrivna historier.
- Hög återspelningsbarhet genom olika familjer, länder, startår, personligheter och livsval.
- Minimal assetproduktion: UI, text, ikoner, porträtt och enkla visuella representationer bär spelet.
- Korta spelsessioner ska fungera, men simuleringen ska vara tillräckligt djup för långa sessioner.
- Premiumprodukt: köp spelet en gång och få hela basspelet.

## 2. Plattform och målgrupp

Första målplattform är Windows/Steam. Spelet ska från dag ett utformas för att fungera väl med handkontroll och på handheld-PC, vilket underlättar framtida portning till Nintendo Switch eller motsvarande konsoler.

Målgruppen är spelare som uppskattar simulation, management, relationer, storytelling och emergenta system, även om de inte nödvändigtvis spelar traditionella grand-strategy-spel.

| Plattform | Roll i planen | Designkonsekvens |
|---|---|---|
| PC / Steam | Primär releaseplattform | Tillåter djupa informationsvyer, mus/tangentbord och tydlig premiumpositionering. |
| Steam Deck / controller | Designkrav från start | Fokusnavigation, stora klickytor och inga kritiska hover-funktioner. |
| Switch | Naturlig framtida port | Passar korta sessioner och "ett år till"-loopen. |
| Mobil | Möjlig senare version | Inte styrande för basspelets UI eller affärsmodell. |

## 3. Värld och global modell

Spelet ska inte vara låst till Sverige. Land är en del av spelarens startförutsättningar och ska påverka livet mekaniskt, inte bara kosmetiskt. Arkitekturen ska därför vara datadriven så att nya länder kan läggas till utan att kärnsystemen behöver skrivas om.

Sverige kan användas som första referens- och testland under utvecklingen eftersom det är lättare att bedöma trovärdigheten, men produktvisionen är global.

### 3.1 Startval

- Land
- Region eller stadstyp
- Startår
- Grundfamilj eller slumpad familj
- Vid behov svårighets-/realismnivå

### 3.2 Länder kan påverka

- namn och demografi
- valuta och lönenivåer
- utbildningsvägar
- arbetsmarknad och yrken
- skatter och välfärdssystem
- sjukvård och livslängd
- bostadsmarknad
- familje- och samhällsnormer
- historiska och ekonomiska händelser
- möjlighet och incitament till migration

Målet är inte historisk simulering på universitetsnivå. Skillnader ska vara tillräckligt tydliga för att ett liv i exempelvis Sverige, USA, Japan eller Brasilien känns olika utan att projektet kräver fullständig modellering av varje lands lagstiftning.

## 4. Kärnloop

1. Världen simuleras framåt: människor åldras, relationer förändras, karriärer utvecklas, barn föds och personer dör.
2. Spelaren får information om relevanta händelser och förändringar.
3. Spelaren fattar beslut om sitt liv och sina relationer.
4. Konsekvenser appliceras på spelaren, NPC:er och världen.
5. Spelaren väljer **Nästa år** och loopen fortsätter.

Kärnkänslan ska vara: *"Jag vill bara se vad som händer nästa år."* Varje år behöver inte innehålla ett stort event. Tempot ska kunna variera genom livet.

## 5. Livsfaser

System ska låsas upp naturligt med ålder och livssituation så att spelaren inte möts av all komplexitet direkt.

| Fas | Exempel på system och val |
|---|---|
| Barndom | familjerelationer, skola, vänner, intressen, tidiga traits |
| Tonår | utbildning, vänskap, kärlek, konflikter, extrajobb, risktagande |
| Vuxenliv | studier, arbete, partner, barn, ekonomi, bostad, företag, brott, status |
| Senare liv | vuxna barn, barnbarn, pension, sjukdom, arv, familjekonflikter |

## 6. Generationer och arv

När den spelbara karaktären dör avslutas inte spelomgången. Spelaren får en översikt över livet, familjen, tillgångarna och efterlevande, och väljer därefter en lämplig familjemedlem att fortsätta som.

Den nya huvudpersonen har redan ett levt liv. Spelaren tar över dennes existerande relationer, ekonomi, personlighet, minnen, barn, partner, problem och möjligheter.

### 6.1 Det som kan leva vidare mellan generationer

- pengar, skulder och tillgångar
- bostäder och företag
- social status och familjens rykte
- familjerelationer och rivaliteter
- hemligheter
- trauman och minnen
- kontakter
- släktträd och historik

## 7. Personmodell

Varje relevant person ska representeras som en självständig simulerad aktör, inte enbart som ett relationsvärde till spelaren.

| Datatyp | Exempel |
|---|---|
| Identitet | namn, födelseår, land, plats, familjekopplingar |
| Personlighet | traits, drivkrafter, toleranser och preferenser |
| Tillstånd | hälsa, ekonomi, utbildning, arbete, bostad |
| Relationer | partner, familj, vänner, rivaler, kollegor |
| Minnen | viktiga händelser och vem som anses ansvarig |
| Mål | karriär, familj, rikedom, trygghet, status m.m. |

## 8. Traits och personlighet

Traits ska vara mekanik, inte dekorativa etiketter. De påverkar AI-beteende, sannolikheter, reaktioner och vilka handlingsalternativ som är tillgängliga.

- Ambitiös
- Lat
- Snål
- Generös
- Lojal
- Impulsiv
- Social
- Introvert
- Modig
- Konflikträdd
- Manipulativ
- Otrohetsbenägen

Antalet traits per person bör vara begränsat så att de går att förstå och minnas. Systemet ska hellre ge tydliga personligheter än många små modifierare.

## 9. Relationer

Relationer bör vara flerdimensionella. Ett enda 0–100-värde är för grunt för den typ av emergenta berättelser som spelet ska skapa.

- närhet
- respekt
- tillit
- attraktion
- rädsla
- avundsjuka
- bitterhet

NPC:er måste ha relationer med varandra. Därigenom kan situationer uppstå utan att de skrivits som förutbestämda eventkedjor, exempelvis att en vän blir avskedad av spelarens bror och därefter klandrar spelaren för att inte ha ingripit.

## 10. Minnen och långsiktiga konsekvenser

Viktiga händelser sparas som strukturerade minnen hos de personer som berörs. Minnen kan förändras i betydelse över tid men ska kunna påverka relationer och framtida beslut långt senare.

Exempel: *"Pappa lämnade familjen när jag var nio."* Minnet kan påverka tillit, relationen till pappan, framtida event, arv och personens egna relationer som vuxen.

## 11. Familj och släktträd

Familjen är produktens centrala meta-system. Spelet behöver stöd för komplexa familjestrukturer och ett automatiskt släktträd.

- biologiska och adoptiva föräldrar
- syskon och halvsyskon
- partners och ex-partners
- barn och styvbarn
- barnbarn
- kusiner och relevanta släktingar

Efter flera generationer ska släktträdet i sig vara en belöning och ett sätt att läsa tillbaka spelarens historia.

## 12. Ekonomi, arbete och samhällsstatus

### 12.1 Ekonomi

- inkomst
- sparande
- skulder
- bostad
- investeringar
- företag
- andra större tillgångar

Ekonomin ska vara begriplig och främst skapa val och konsekvenser, inte bli en detaljerad privatekonomisimulator.

### 12.2 Karriär

Karriärer byggs som datadrivna kedjor med krav och sannolikheter. Utvecklingen påverkas av utbildning, prestation, traits, relationer, ekonomi och slump.

Exempel på breda yrkesspår är sjukvård, industri, akademi, IT, handel, offentlig sektor, juridik, media, företagande och kriminalitet.

## 13. Hemligheter och information

Vissa fakta ska ha begränsad kunskap. Spelet behöver därför skilja på vad som är sant och vilka personer som känner till det.

- otrohet
- biologiskt föräldraskap
- brott
- dolda skulder
- hemliga relationer
- ekonomiska oegentligheter

När en hemlighet avslöjas kan flera relationer och system påverkas samtidigt. Detta är en viktig motor för emergenta berättelser.

## 14. Eventsystem

Events ska vara en kombination av handskrivna mallar och systemgenererade situationer.

### 14.1 Generiska eventmallar

Exempel: *"Din chef erbjuder dig en befordran, men tjänsten kräver att familjen flyttar."* Mallarna ska innehålla villkor, deltagare, val och konsekvenser och vara möjliga att lokalisera.

### 14.2 Emergent situationsdetektion

Systemet ska kunna upptäcka intressanta kombinationer i simuleringen och presentera dem för spelaren. Exempel: partnern är otrogen med spelarens vän, två syskon konkurrerar om ett arv eller spelarens barn gifter sig med ett barn till en gammal rival.

Designmålet är att systemen skapar historier snarare än att varje historia skrivs på förhand.

## 15. NPC-AI

NPC-AI ska vara tillräckligt enkel för att vara stabil och begriplig men tillräckligt autonom för att världen ska kännas levande.

Varje beslutssteg bör väga samman behov, mål, traits, relationer, minnen, ekonomiskt läge, tillgängliga handlingar och en kontrollerad mängd slump.

AI:n behöver inte planera perfekta liv. Tvärtom är misstag, konflikter och irrationella val viktiga för berättelserna, så länge beteendet går att förklara utifrån personen.

## 16. Historik och familjekrönika

Spelet registrerar viktiga livs- och familjehändelser i en tidslinje. Historiken ska kunna filtreras per person, generation och familj.

I slutet av en spelomgång blir släktträdet och familjekrönikan det primära "resultatet", kompletterat med statistik som generationer, familjemedlemmar, största förmögenhet, företag, skilsmässor och andra relevanta milstolpar.

## 17. UI och visuell riktning

UI:t är spelets huvudsakliga grafik och måste därför kännas avsiktligt, premium och PC-anpassat. Det får inte se ut som ett mobilgränssnitt som portats till Steam.

| Vy | Innehåll |
|---|---|
| Huvudvy | porträtt, ålder, traits, ekonomi, aktuellt event och nästa-år-kontroll |
| Familj | partner, barn, föräldrar, syskon och nära familj |
| Relationer | vänner, rivaler, kollegor och relationsdimensioner |
| Karriär | utbildning, jobb, prestation och möjligheter |
| Ekonomi | inkomst, skulder, bostäder, företag och tillgångar |
| Släktträd | interaktiv generationsöversikt |
| Historik | livslogg och familjekrönika |

Controllerkravet innebär fokusbaserad navigation, tydliga paneler, stora klick-/fokusytor och att ingen central funktion enbart får vara tillgänglig via hover.

## 18. Affärsmodell

Basspelet säljs som en komplett premiumprodukt. Ingen energi, reklam, premiumvaluta, betalvägg för grundläggande yrken, traits eller karaktärsinställningar.

Framtida expansioner kan vara aktuella om de tillför verkligt nytt innehåll eller nya system, exempelvis nya tidsperioder, länder eller större mekaniska områden.

## 19. MVP / första spelbara version

Första målet är inte att bygga hela visionen utan att bevisa att simulationsmotorn kan skapa underhållande och minnesvärda familjehistorier.

| Område | MVP-krav |
|---|---|
| Karaktärer | namn, ålder, traits, hälsa, grundläggande behov |
| Familj | föräldrar, syskon, partner, barn, död och arv |
| Relationer | flera relationsdimensioner och några handlingar |
| Liv | utbildning, arbete, pengar och bostad |
| Events | motor + cirka 50–100 mallar |
| NPC-AI | partnerskap, barn, arbete, separation, flytt, död |
| Generationer | övertagande av nästa familjemedlem efter död |
| Historik | livslogg och enkelt släktträd |
| Värld | ett referensland, men datamodell för flera länder |

## 20. Tekniska designprinciper

- Kärnsystem ska vara datadrivna för att innehåll kan läggas till utan kodändringar.
- Personer ska ha stabila unika ID:n så att relationer, minnen och släktträd kan referera säkert.
- Simuleringen ska vara deterministisk när ett seed används, vilket underlättar testning och felsökning.
- Save-format bör versionshanteras tidigt eftersom långlivade spelvärldar är centrala för produkten.
- Event, länder, yrken och traits ska definieras i innehållsdata snarare än hårdkodas där det är praktiskt.
- Simulering och presentation ska separeras så att samma spelstate kan visas på PC, handheld och senare andra plattformar.

## 21. Designregler

- Spelaren ska regelbundet kunna fatta dåliga men begripliga beslut.
- Features ska i första hand motiveras av om de kan skapa en historia som spelaren minns.
- NPC:er ska kunna överraska utan att kännas slumpmässiga.
- Konsekvenser får gärna leva längre än den person som orsakade dem.
- Fler system är inte automatiskt bättre; system ska korskopplas och skapa följdeffekter.
- UI och text ska visa varför något hände när det är relevant, så att simuleringen inte känns godtycklig.

## 22. Exempel på emergent kedja

1. Spelaren vägrar låna pengar till sin bror.
2. Brodern tvingas sälja sitt företag och får ett negativt minne kopplat till spelaren.
3. Flera år senare blir brodern chef över spelarens dotter.
4. Dottern får sämre möjligheter på jobbet på grund av den dåliga familjerelationen.
5. Spelaren försöker reparera relationen med brodern.
6. Brodern kräver i stället att bli prioriterad i ett framtida arv.
7. När spelaren dör uppstår konflikt mellan barnen om testamentet.

Ingen av dessa punkter behöver vara en förskriven berättelse. Värdet uppstår när flera enkla system producerar kedjan tillsammans.

## 23. Definition av framgång för prototypen

Prototypen är lovande när en spelare efter några generationer spontant kan återberätta personer, konflikter och händelser från sin familj utan att de varit del av en linjär story.

Ett centralt kvalitetsmått är därför inte mängden innehåll utan mängden meningsfulla historier som ett relativt litet antal system kan skapa.

## 24. Kärnformulering

Människor fattar beslut. Besluten påverkar andra människor. De människorna fattar nya beslut. Konsekvenserna lever längre än karaktärerna själva.

Efter några timmars spel ska spelaren kunna öppna släktträdet och känna:

> *"Herregud. Vilken jävla familj."*
