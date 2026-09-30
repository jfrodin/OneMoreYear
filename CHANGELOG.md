# Changelog

What changed in each version of One More Year. Newest first. Versions are tagged in git (`v0.5.0`).

## 0.20.0 – 2026-09-30 · Everyday life, work and parenthood

- **40 events of everyday adult life**: neighbours through the wall, Midsummer, charter trips,
  flat-pack fights, dinner parties, a dog, a speech at a friend's wedding, a burglary, the allotment,
  the kitchen renovation, choosing sides in a friend's divorce, a red summer cottage – and moments
  of their time: the moon landing, Waterloo, the morning after Palme, the first mobile phone, Y2K,
  the storm of 2005, working from the kitchen table in 2020.
- **25 work events**, many tied to the kind of job: the Christmas party, reorganisation, a boss half
  your age, reply-all, strikes, night shifts, the patient in room 4, the pupil at the back, a robbery
  at the till, starting your own business, being asked to cook the books, the payslip that shows
  you're paid less.
- **20 parenthood events**: sleepless nights, the first word, preschool, fever, a child who is bullied
  – or who bullies, the teenager out at two in the morning, a child who comes out, graduation, the
  empty room, the empty nest, the grown child who moves back home, a grandchild on the way.
- The game now has almost 300 events.

## 0.19.0 – 2026-09-30 · The dark themes, in depth

- **Sexual violence between adults** – only ever as something that happens to you or someone
  close, never something you do, and never described (see design-decisions.md). Report it, tell
  someone, stay silent; a relative can be the perpetrator – a secret that one day splits the family,
  and may end in court. Harassment by a boss. #MeToo in 2017.
- **Coercive control and stalking**: a partner who checks your phone and isolates you – leave, reach
  out, or give in; an ex who won't let go.
- **Addiction, deeper**: relapse after years clean, treatment you can choose yourself, a partner who drinks.
- **Suicide as grief**: deep, long depression can take someone you love; the family is left with the
  question why. For you, a darkest night where every choice is a way to reach for help.
- **Losing your home**: deep debt and no income bring the bailiffs – back to your parents, a friend's
  sofa, or the street; homelessness and the way back.
- New content settings: **Sexual violence** (adults) and **Suicide**; *Sexual abuse of children* is its own category.
- Test scenarios now search, in a fixed order, for a family that matches what they promise – so they
  survive balancing changes (names can differ between versions).

## 0.18.0 – 2026-09-30 · The later years, and the mind

- **Ailments that last**: depression, anxiety, burnout, trauma and dementia. They start from what
  happens in a life – low mood, losses, stress, personality, violence, age – cost happiness, health
  and work performance every year, and pass with a chance that treatment raises. Shown on the person page.
- You choose what to do when it hits you (see a doctor, tell someone, push through); the new action
  *Get help* is there while you carry something. Your partner's depression and a parent's dementia
  become your story too.
- **25 events for the later years**: the last day at work, empty Mondays, grandparent weekends,
  the other side of the bed, dancing again at 70, the car keys, a fall, the test results, the golden
  wedding, a phone scam, a smartphone, writing it all down, the last summer – and more.
- **Care homes**: very old or ill people who live alone move into one; you decide for yourself.
- New content setting: **Mental illness** (on, mentioned only, off).
- Events can now depend on the calendar year, years together with a partner, and ailments.

## 0.17.2 – 2026-09-30 · A paper worth reading

- The family newspaper now only comes in **big years** – births, deaths, illness, weddings and
  divorces, secrets, crime, inheritances and history. Quiet years go straight on (about two years
  in three).
- Choose at the bottom of the paper: every year, only in big years (default) or never.
- Close it by clicking anywhere outside it, or with Esc / B.

## 0.17.1 – 2026-09-30 · Choose a decade

- The start year is no longer a number to type: you choose a decade to begin in, from "The 1950s –
  after the war" to "The 2010s – smartphones".

## 0.17.0 – 2026-09-30 · Playtest 3

- **A real family tree**: photo cards in generations – grandparents, parents, siblings with your
  partner, children and grandchildren – joined by lines. Choose anyone to see the family from
  their place in it; choose the person in the middle to open their page.
- **Names that fit**: first names follow the year you were born (a Karl in 1920, a Jimmy in 1978,
  a Selma in 2015) and the family's heritage – Swedish, Finnish, Balkan or Middle Eastern, as common
  as each was in Sweden at the time. Heritage also shapes surnames and looks, and children take it
  from their parents.
- **30 new events for growing up**: monsters under the bed, the first day of school, learning to
  ride a bike, the lake, birthday parties, the school play, the library, try-outs, the band, a
  diary that was read, green hair, the first broken heart, running away – and more.
- **Trait colours** mean something: green for a good side, red for a dark side, blue for neither.
- The full-body figures are gone; the portraits stay.
- Content files are now read strictly: a misspelt field is an error instead of silently ignored.
- Fixed: a saved and reloaded game could continue slightly differently (flag order).
- Test scenarios got new seeds (The family secret: Birger, Helena and Julia; Grown up: Peter;
  Late in life: Yvonne).

## 0.16.0 – 2026-09-30 · The family album

- **A new look**: the game is now a family album on paper instead of a dark app. Cards sit on the
  page with soft shadows, portraits are prints with a white edge, memories are handwritten.
- **The look follows the decade**: colours and heading typefaces change smoothly as the years pass –
  sepia and typewriter in the fifties, brown and orange in the seventies, magenta in the eighties,
  clean after 2000. Photos are tinted like prints of their time.
- **The family newspaper**: every new year opens with *The [Family] Chronicle* – the biggest family
  news as the headline, the rest in brief, and what happened in the world.
- Open fonts bundled: Lora, Playfair Display, Caveat, Special Elite, Fraunces, Rajdhani and Inter.
- Play.cmd imports new assets before starting.

## 0.15.0 – 2026-09-30 · Content settings

- **Choose how dark it gets**: sexual abuse, violence, murder, addiction and infidelity can each be
  *On*, *Mentioned only* (it happens to others, off-screen – a line in the chronicle, never an event
  or a choice for you) or *Off* (it never happens).
- Asked the first time the game starts; change it any time from the title screen or with the new
  *Content* button in a game. Stored per save, and remembered as the default for new games.
- The title screen says the game is for adults and what it contains.
- Events carry content tags, so new dark events are easy to cover.

## 0.14.0 – 2026-09-30 · Shareable seeds

- Every new game gets a **seed code** like `7LB2WVPK` (no I, O, 0 or 1 – easy to read out and type).
  It is shown under the player card and on the game-over screen: give it to a friend, with the start
  year, and they get the same family and the same luck – what happens next depends on their choices.
- Type any text as a seed – `SVENSSON` is a world of its own. Plain numbers still work as before.
- Playtest notes (F1) include the seed code.

## 0.13.0 – 2026-09-30 · Balancing: families

- **Family stays family**: parents and grown children, siblings and grandparents no longer drift
  to "Neutral" just because they live apart. Without a grudge, ties settle at a warm baseline and
  grow back slowly; real conflicts still show as bitterness. Most close relatives now like each other.
- **Partners take the initiative**: your partner may suggest moving in together, or say they want
  a child (or to adopt). You still decide. Partners feel closer over the years, and propose more often.
- **Dark traits don't pile up**: a predatory parent now very rarely passes it on (was 30 %, now 2 %),
  so abuse no longer runs through whole family lines. Each trait can have its own inheritance.
- Test scenarios got new seeds where needed (The family secret: Joakim, Gun and Anna; Late in life: Göran).
- SimRunner: `--dating`, `--wealth` (now with secrets and family opinions) and `--count=N` for seed searches.

## 0.12.0 – 2026-09-30 · Money that works

- **A real market**: inflation follows history, the stock market goes up most years and crashes in
  the crises (1992, 2008, 2020), home prices rise slowly and fall in bad times. Big market years
  make the news. The same seed always has the same market.
- **Invest**: put savings into funds (the whole market) or shares in a single company (can double,
  can go bankrupt); sell whenever you like. Money in the bank now just keeps its value.
- **Homes and mortgages**: a home has a market value and a loan. You pay 15 % down, the bank lends
  the rest if your income is enough, and interest and paying off the loan come out of your income.
  Pay extra on the loan, or sell the home and keep the difference.
- A widowed partner keeps the home; otherwise it is sold with the estate. Couples who move in
  together keep one home.
- Other people invest (risk-takers in shares) and buy homes when they can afford it.
- **Balancing**: people with money now live on it, a few percent of their wealth a year. Fortunes no
  longer grow to hundreds of millions over the generations (the richest after 150 years now have
  tens of millions in today's money).
- The Money tab shows bank, funds, shares, home and mortgage, and what the markets did this year.
- Old saves are converted: homes get a value and a loan.

## 0.11.0 – 2026-09-30 · Workplaces

- Jobs now have **employers**: "Assistant Nurse at Uppsala University Hospital", "Carpenter at
  Granträ Bygg AB", "Case Officer at The Social Insurance Agency". Fictional names from the
  city you live in, made-up places and companies – different for each career (preschools for
  childcare workers, courts and law firms for lawyers, and so on).
- Job offers show where the job is; your colleagues and boss work at the same place.
- The workplace is shown on the School & Work tab, the person page and in the chronicle.

## 0.10.0 – 2026-09-30 · Faces

- **Generated portraits** replace the coloured initials everywhere: face shape, jaw, chin, nose,
  eyes, mouth, lips, brows, ears, skin, hair and eye colour, curls and freckles.
- **Inherited**: children get a mix of their biological parents' features – siblings and cousins
  look related, and a hidden father shows in the child's face.
- **Ageing**: babies and children have their own proportions; hair greys and thins (earlier for
  some than others), wrinkles come, glasses appear – for some as children, for others later in life.
- Men may wear stubble, a beard or a moustache; women's hairstyles vary. Heavier people get fuller
  faces, and the mouth follows the mood. The dead are shown in grey.
- **Full-body figure** on the person page, drawn to scale: height, build and weight.
- Faces are created from the world seed and the person, so they are stable, work with old saves
  and never change what happens in the game.

## 0.9.4 – 2026-09-30 · The family secret: ages

- The mother in *The family secret* was 15 when her daughter was born (not 24, as the generated
  family had it), and the daughter is her first child. The legal father was her 17-year-old boyfriend,
  who has always believed the child is his. New family (seed 11): grandfather Thomas, mother Elsa,
  daughter Sofia.

## 0.9.3 – 2026-09-30 · The family secret, three sides

- **The family secret** is now a hidden father: the grandfather is also the biological father of his
  granddaughter, born from his abuse of his daughter decades earlier. Only he and the mother know,
  and now the granddaughter has ordered a DNA test. The act is never shown or played.
- Three scenarios for the same family: play the **grandfather**, the **mother** or the **granddaughter**.
- New situations: *Roots* and *The DNA kit* (buy silence, threaten, stop the test, tell the truth),
  *The results* and *It's out* (go to Mum, report to the police, cut everyone off, leave town).
- The truth can also come out by itself (DNA tests from 2000 onwards, the mother telling the family),
  and it brings the abuse and the police with it.
- Replaces the 0.9.2 version of the scenario (grandfather and the abused grandson).

## 0.9.2 – 2026-09-30 · The family secret, from the other side

- The scenario **The family secret** is now played as the grandfather, aged 72. His grandchild,
  now an adult, was abused by him years ago. The act is never shown or played; the secret, the guilt
  and the reckoning are.
- New situations: *Sunday dinner* (keep quiet, stay away or confess) and *Everyone knows* (admit,
  deny or move away – with the police involved).
- When a family member's abuse comes out, the police may now charge them too, and they can go to prison.

## 0.9.1 – 2026-09-30 · Test scenarios

- **Scenarios** on the title screen: eight fixed starting situations for testing – old money, a poor
  home with a drinking father, a sunny family, a grandfather with a dark secret, a cruel teenager, a
  family full of affairs, a grown-up life at 26 and a late life at 62. See docs/test-scenarios.md.
- Scenarios that start later in life play the early years automatically; the chronicle shows the life so far.
- A seed typed together with a scenario gives the same kind of family with other people.
- Developers: `--scenario=id` starts one directly; SimRunner has `--scenarios` and `--find-seed=id`.
- Fixed: the smoke test and screenshot tour no longer overwrite your autosave (they use their own slot).

## 0.9.0 – 2026-09-30 · Crime and punishment

- **Outside the law**: shoplifting, burglary, car theft, drug dealing, fraud, robbery – and against
  people you know: beating someone up, blackmail (if you know their secret) and murder.
- Anyone can do it. Criminal-minded and dishonest people are better at not getting caught; kind
  people feel guilty afterwards. Doing it again and again draws the police's attention.
- Temptations come your way – more often if your personality leans that way.
- Caught? A fine, or prison – longer for repeat offenders. Your family is ashamed, your partner may
  leave, your children remember. A criminal record makes job offers rarer.
- **Prison**: keep your head down for parole, study, work out, get into fights, receive visits.
- Unsolved murders can be solved years later.
- Relatives commit crimes too, and violence at home is sometimes reported to the police.

## 0.8.0 – 2026-09-30 · Where you live

- Ten Swedish cities, from Stockholm to Kiruna and Vimmerby. Housing costs depend on the city.
- Children live with their parents. As a young adult you decide when to move out: a flat of your
  own, a shared flat with friends (cheaper) or staying at home a while longer (cheapest).
- **Move to another city** whenever you like – see what homes cost before you choose. Move back home
  if money gets tight.
- A promotion in another city or a partner's dream job now says which city – and moves you there.
  Your partner and children come along.
- Couples who move in together end up in the same city.
- Relatives in other cities slowly drift apart unless you keep in touch.
- Homes, where you live and your living costs are shown everywhere: "Rents a flat in Malmö".

## 0.7.2 – 2026-09-30 · A taste for age gaps

- New adult traits that appear at 18: **Likes them younger** (more common among men) and **Likes them
  older** (more common among women). They go for partners 8–25 years younger or older – always adults.
- The family still talks: big age gaps set tongues wagging and upset the younger one's parents.

## 0.7.1 – 2026-09-30 · Family ties

- Close relatives (parents, children, siblings, half-siblings, grandparents, aunts/uncles,
  nieces/nephews) never become couples – blood counts, including a hidden biological father.
- Cousins can be together where the law allows (it does in Sweden) – and the family whispers.
- Step-siblings who grew up together don't become couples.
- Adults can be with anyone, but a big age gap ("half your age plus seven") sets tongues wagging:
  the younger one's parents are angry, and a grown child the same age as the new partner takes it badly.

## 0.7.0 – 2026-09-30 · Light and dark

- 31 traits instead of 12 – light (Kind, Cheerful, Charming, Devoted parent, Resilient …), dark
  (Hot-tempered, Addictive personality, Dishonest, Jealous, Greedy, Cruel, Criminal-minded …) and
  odd ones (Paranoid, Vain, Eccentric, Hypochondriac). Trait colours: gold, red and blue.
- People have one to four traits, not always three.
- Traits do things: cheerful and gloomy people have different everyday moods, kind people draw others
  closer, paranoid people trust less and less, devoted parents are closer to their children, jealous
  partners grow bitter, greedy heirs fight over inheritances, dishonest people get caught stealing
  and hide affairs longer, charming people do better in love.
- **Addiction** – alcohol, drugs or gambling can take hold, cost money and health, hurt the family
  and scar the children. People can get clean; drugs can kill.
- **Violence at home** – hot-tempered or cruel people may hit a partner or child. Everyone in the
  home remembers. If it happens to you, you decide what to do.
- **Abuse as a family secret** – shown only through its consequences: a memory that can't be spoken
  of, fear, trauma. Years later it may come out and split the family.
- Deep traumas can leave new traits behind (gloomy, paranoid, addictive, hot-tempered) – unless the
  person is resilient. Old age softens some tempers.
- Fixed: generated colleagues sometimes had jobs they weren't qualified for.

## 0.6.0 – 2026-09-30 · Babies, lovers and loose ends

- Babies are born the year after you decide to try (adoptions too) – and **you choose the name**.
- Saying yes to someone new while you're in a relationship no longer dumps your partner: it becomes
  a secret affair, and the choice tells you so beforehand.
- Lovers show up in **People**, labelled as your lover.
- Your parents' partners and exes stay in **People** after a break-up ("your father's ex").
- "Single – Monica broke up with him in 1984", "Widowed – Erik died in 2001" instead of just "Single".
- Which side of the family: paternal and maternal grandparents, aunts and uncles.
- Pay is shown per month, as people talk about it in Sweden.
- Click a partner, parent, sibling or child in someone's profile to jump to them.
- Height and weight for everyone, children included.
- Children live with their parents instead of "renting".
- Office events only happen to people with office jobs; the boss's weekend request fits any job.

## 0.5.0 – 2026-09-30 · People you know

- Classmates in school, colleagues and a boss at work. When you move on they become old
  classmates and former colleagues.
- Friendships grow out of the people you get on with best.
- **Go out with your friends** to meet their friends – sometimes someone you could date.
- Attraction builds between people who see each other; looks and attraction affect asking someone out.
- A boss who likes you makes promotions more likely.
- New events: a school crush, an admirer, an office romance, stolen credit at work, the boss's
  deadline, a classmate's party.
- The **Family & Friends** tab is now called **People**.

## 0.4.0 – 2026-09-30 · School, work and money

- Upper secondary programmes (academic or vocational) and ten university programmes with admission
  grades. Degrees qualify you for specific careers – only doctors with a medical degree, and so on.
- Grades that follow your smarts, effort and part-time jobs.
- Job offers to choose between; promotions need the right education.
- New **School & Work** tab: grades, programme, diplomas, job, performance and the career path with
  what each step requires.
- New **Money** tab: every krona in and out this year and last year – salary, tax, living costs,
  children, interest, inheritance, welfare.
- Smarts, looks and fitness, partly inherited, plus height, build, hair and eye colour. They affect
  grades, work, dating and health.
- If you have savings you live on them before you get welfare.
- The family always continues as long as the bloodline lives.

## 0.3.0 – 2026-09-30 · The law and the first playtest

- Relationships follow Swedish law: real relationships and pregnancy from 15; innocent "going
  steady" for younger kids of the same age; moving in from 16 with your parents' consent; marriage
  from 18. Set per country.
- Under 18 you can ask your parents to let you move in with your partner.
- Fixed: university offered while already studying; graduates ending up in jobs that don't use their
  degree; events repeating or happening at the wrong age (an event can now happen only once per
  person); the same people shown twice in the family tree.

## 0.2.0 – 2026-09-30 · Who is who

- Everyone mentioned in the news, events, chronicle and memories shows how they relate to you:
  "Anna (your sister)", "Agneta (Oskar's girlfriend)".
- Press **F1** (controller: Select) to write a playtest note, saved with a screenshot and the
  situation.

## 0.1.0 – 2026-09-30 · First playable version

- A family simulated year by year over generations: relationships in seven dimensions, memories,
  secrets (affairs, hidden fathers), careers, money, wills and inheritance.
- When you die you continue as someone in the family.
- Godot interface with This Year, Family, Family Tree and Chronicle, playable with mouse, keyboard
  or controller. Autosave every year.
