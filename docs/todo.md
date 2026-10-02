# To-do

Only what is still open. Finished items are removed and described in [CHANGELOG.md](../CHANGELOG.md).
Big decisions live in [design-decisions.md](design-decisions.md).

## Next, in this order (from playtest 4, 2026-10-01)

1. [ ] **Rethink the newspaper.** Several lines about the same person in one paper (moved in, split up,
   an affair) read oddly, and the order is unclear. Options: one story per person, told in order;
   a front page with one headline and a few short items; or something else than a newspaper
   (a Christmas letter, a page in the album). The producer has not decided yet – wait for them.
2. [ ] **A new decade, a new chapter**: a title page when a decade begins ("The 1980s – …"), and a
   more visible change of look at that moment instead of only a slow blend.
3. [ ] Keep checking texts for anachronisms (use `[[year: before || after]]`). Also programme
   names, like "Computer science" before the 1970s.

## Closed test build (planned, not yet)

When the game is ready for outside testers. Ask the producer before starting.
- itch.io: a hidden, password- or key-protected page, marked 18+, Windows first. Updates with butler.
- A "test build" mode: F1 stays, development tools go, and a start notice says "Test version – F1 for feedback".
- F1 posts the note, screenshot, version, seed and save to a Discord channel through a webhook (fine
  for a closed group, not for public builds).
- Saves must survive updates in the middle of a life.
- The producer sets up the itch.io account, the Discord server and webhook, and invites the testers.

## Before release

- [ ] **Strip the testing tools** from the release build.
  - Test scenarios are already hidden outside development builds; also leave `scenarios.json` out.
  - The F1 playtest notes, `--load`, `--smoke` and `--screenshots`.
  - The seed field stays (see design-decisions.md, "Shareable seeds").

## Content sprint (before more countries)

1. [x] **Old age** – done in 0.18.0 (with ailments: depression, anxiety, burnout, trauma, dementia).
2. [x] **Dark themes in depth** – done in 0.19.0.
3. [x] **Adult everyday life, work and parenthood** – done in 0.20.0 (85 events).
4. [x] **Passive trait checks** – done in 0.21.0 (insights, trait choices, chance factors).
5. [x] **Settings, save slots, introduction, sound** – done in 0.22.0. Music (real recordings, by era) is still to come.

## Later

- [ ] Type your own name for a baby (needs a controller-friendly text input).
- [ ] Several relationships at once – secret affairs, or open relationships by agreement.
- [ ] Events at work beyond the first few (colleagues, the boss).
- [ ] More events in general – childhood and teens got 30 in 0.17.0; adult life, work and old age next.
- [ ] Chronicle filter per person and per generation (spec §16).
- [ ] Localization (Swedish and more).
- [ ] **More countries**, in this order:
  1. Prepare: move everything that is Swedish in the code into country data (welfare, schools,
     pensions, currency, laws, holidays) – so a new country is only data and events.
  2. **USA** – the biggest market, English, and the biggest contrast: healthcare that can ruin a
     family, student debt, suburbs, the Vietnam draft, no parental leave.
  3. **United Kingdom** – class, council estates, the NHS, Thatcher, the miners' strike.
  4. **Germany** – a family split by the Wall in 1961, East and West, reunited in 1990.
  5. Later: Poland, Italy or Spain, Japan, Finland.
  - With several countries, **emigration** becomes a story of its own (to America in the fifties,
    to Sweden with your names and your heritage).

## Balancing

- Nothing open. `SimRunner --dating` shows partners, marriages and children at 35 (random answers vs. a player who says yes to love).

