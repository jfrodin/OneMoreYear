# To-do

Only what is still open. Finished items are removed and described in [CHANGELOG.md](../CHANGELOG.md).
Big decisions live in [design-decisions.md](design-decisions.md).

## Next, in this order

1. [ ] **Portraits**: simple generated portraits (and a full-body figure) where looks come from the
   parents' genes – hair, eyes, skin, height, build, ageing.
2. [ ] **Employer names** for jobs (fictional companies, hospitals, schools).
3. [ ] **Investments**: stocks and funds, home values that change, mortgages.

## Before release

- [ ] **Content settings**: let players turn dark themes off, category by category.
  - Categories: sexual abuse, violence at home, murder, drugs and addiction, suicide and self-harm
    (once it exists), infidelity.
  - Each is *On*, *Mentioned only* (happens off-screen, one line in the chronicle, no events or
    choices) or *Off* (never happens).
  - Asked on first start, and changeable at any time in the settings. Stored per save.
  - Events and dark systems carry content tags and check the settings.
  - Content warnings on the title screen and the store page (Steam's mature content survey).
- [ ] **Readable seed codes** like Balatro (e.g. `7LB2WVPK`): shown in the game menu and on the
  game-over screen, typed in on the title screen. See design-decisions.md.
- [ ] **Strip the testing tools** from the release build.
  - Test scenarios are already hidden outside development builds; also leave `scenarios.json` out.
  - The F1 playtest notes, `--load`, `--smoke` and `--screenshots`.
  - The seed field stays (see design-decisions.md, "Shareable seeds").

## Later

- [ ] Type your own name for a baby (needs a controller-friendly text input).
- [ ] Several relationships at once – secret affairs, or open relationships by agreement.
- [ ] Events at work beyond the first few (colleagues, the boss).
- [ ] More events in general – grow them once the core feels right.
- [ ] Chronicle filter per person and per generation (spec §16).
- [ ] A graphical family tree.
- [ ] Localization (Swedish and more).
- [ ] More countries.

## Balancing

- [ ] Some family members become extremely rich after many generations – rebalance savings, returns
  and inheritance.
- [ ] Many relationships drift towards "Neutral" over the decades – check drift and contact rules.
- [ ] The abuse storyline can pile up in one family (seed 1008 from 1950: six abuse secrets in 65 years) –
  check how the predatory trait is inherited and how often it strikes.
- [ ] Played without actions, few players find a partner or have children (see `--find-seed=late_in_life`) –
  check how often dating happens by itself.
