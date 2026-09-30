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

## From playtesting

- [x] **Show relations next to names in all text.** It's hard to tell who is who when reading –
  names get mixed up. Add the relation to the current player in parentheses, e.g.
  "Anna (your sister) and Erik (your brother-in-law) got married." Applies to the year's news,
  event texts, the chronicle and memories. The chronicle should probably store person ids and render
  the relation at display time (relative to the current player, since that changes across generations).

## Known issues / balancing

- [ ] Economy: some family members become extremely rich after many generations – rebalance savings, returns and inheritance.
- [ ] Many relationships drift towards "Neutral" over the decades – check drift and contact rules.

## Planned

- [ ] More events – grow them once the core feels right.
- [ ] Chronicle filter per person and per generation (spec §16).
- [ ] Real portraits instead of initials.
- [ ] Localization (Swedish and more) – English first.
- [ ] More countries.
