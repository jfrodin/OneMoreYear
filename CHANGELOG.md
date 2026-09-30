# Changelog

What changed in each version of One More Year. Newest first. Versions are tagged in git (`v0.5.0`).

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
