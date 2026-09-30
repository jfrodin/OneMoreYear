# To-do

Backlog for One More Year. Newest ideas at the top of each section.

## Design decisions

- **Age rating 18+ (PEGI 18 / Mature).** The game is made for adults, so content can be graphic and
  mature: sex, violence, drugs, crime, abuse, addiction and dark family secrets can be shown openly
  rather than toned down. Affects event writing, text, images and store pages (Steam mature content
  survey, age gate). Consider a content option for players who want to tone it down.
- **Full tonal range: from sunshine to pitch black.** Family happiness, love, weddings, grandchildren
  and roses – but also drugs, violent crime, addiction and a grandfather who abuses his grandchildren.
  Both ends should exist in the same family, often in the same life. The light moments matter as much
  as the dark ones; the contrast is what makes the family feel real.
- **How the darkest themes are handled.** Abuse of children exists as a story element – a secret,
  a trauma, memories that shape a life, a revelation that splits the family – shown through its
  consequences and how people react. It is never depicted in sexually explicit detail. (This is also
  what keeps the game within PEGI 18 and Steam's rules.)

- **Relationships follow the country's law.** In Sweden the age of consent is 15, so that is where
  romantic/sexual relationships can start (pregnancy, living together etc. still follow their own ages;
  marriage from 18). Below that only innocent crushes and "going steady" between kids of similar age.
  Stored per country (`ageOfConsent`, `marriageAge`) so other countries follow their own laws. No
  romance between an adult and someone under the age of consent – that belongs only to the dark
  themes above (abuse), never to normal dating.

## From playtesting

- [x] **Show relations next to names in all text.** It's hard to tell who is who when reading –
  names get mixed up. Add the relation to the current player in parentheses, e.g.
  "Anna (your sister) and Erik (your brother-in-law) got married." Applies to the year's news,
  event texts, the chronicle and memories. The chronicle should probably store person ids and render
  the relation at display time (relative to the current player, since that changes across generations).

### Playtest 1 (2026-09-30) – bugs

- [x] "Go to university" is offered again while already studying (self_study needs "not studying").
- [x] University graduates often end up as shop assistants – job matching must prefer jobs that use the education.
- [x] "The alibi" (sibling sneaking home) fires for 25-year-olds and repeats – childhood/teen events should
  be once per person and age-appropriate. General rule needed: some events only once per target.
- [x] Family tree shows the same people twice (once under each set of grandparents).

### Playtest 1 – ideas

- [x] **Appearance and attributes**: height, build, hair, eyes (inherited), plus more bars – e.g. Looks,
  Smarts, Fitness, Stress. They should matter: smarts for school/career, looks for dating, etc.
- [ ] **Relationships while young**: innocent crushes and "going steady" between kids of similar age,
  real relationships from the age of consent (15 in Sweden) – see design decision above.
- [ ] **Finding love among people you know**: classmates, colleagues and friends-of-friends as a pool of
  acquaintances who can become friends or partners – not only a random "Look for love" button.
- [x] **School & career tab**: grades, programme/major, current job, performance, boss and colleagues,
  job offers to choose between.
- [x] **Economy tab**: income, taxes, living costs, student loans, savings, debt, home – and a yearly
  breakdown of why the money changed.
- [x] **Deeper study/work loop**: choose a programme and major that leads to specific careers, part-time
  jobs while studying, applying for jobs. (Still to do: events at work, boss and colleagues – comes with acquaintances.)

## Known issues / balancing

- [ ] Economy: some family members become extremely rich after many generations – rebalance savings, returns and inheritance.
- [ ] Many relationships drift towards "Neutral" over the decades – check drift and contact rules.

## Planned

- [ ] More events – grow them once the core feels right.
- [ ] Chronicle filter per person and per generation (spec §16).
- [ ] Real portraits instead of initials.
- [ ] Localization (Swedish and more) – English first.
- [ ] More countries.
