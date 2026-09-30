# To-do

Only what is still open. Finished items are removed and described in [CHANGELOG.md](../CHANGELOG.md).
Big decisions live in [design-decisions.md](design-decisions.md).

## Next, in this order

1. [ ] Playtest the new systems (faces, money, content settings) and triage the notes.

## Before release

- [ ] **Strip the testing tools** from the release build.
  - Test scenarios are already hidden outside development builds; also leave `scenarios.json` out.
  - The F1 playtest notes, `--load`, `--smoke` and `--screenshots`.
  - The seed field stays (see design-decisions.md, "Shareable seeds").

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

- [ ] Few players marry: a player who only answers events is rarely married at 35 (`SimRunner --dating`).
  Look at how partners feel about the player over time, and at proposals.

