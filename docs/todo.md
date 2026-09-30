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
- [ ] More countries.

## Balancing

- [ ] Few players marry: a player who only answers events is rarely married at 35 (`SimRunner --dating`).
  Look at how partners feel about the player over time, and at proposals.

