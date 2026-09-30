# Test scenarios

Fixed starting situations for playtesting. Each one is a seed plus changes to the starting family,
and some start later in life (the years before are played automatically, and they show up in the
chronicle as the life so far). The same scenario always gives the same family.

**Start one:** on the title screen, choose it under *Scenario* and press *New Life*.
If you also type a seed, it replaces the scenario's own seed: same kind of family, different people.

Shortcut without the menu: `Godot --path game -- --scenario=old_money`

The scenarios are defined in [content/scenarios.json](../content/scenarios.json). Add more there.

| Scenario | Id | Seed | Starts | What it tests |
|---|---|---|---|---|
| **Old money** | `old_money` | 1001 | 1965, newborn | Rich, greedy parents with a home of their own and a very rich grandfather. You are ambitious and charming. Inheritance, spoiled children, extreme wealth. |
| **Nothing to lose** | `nothing_to_lose` | 1002 | 1975, newborn | Poor parents in debt, both unemployed. Your father drinks and has a temper. Poverty, addiction, violence at home, breaking the pattern. |
| **Sunshine** | `sunshine` | 1003 | 1980, newborn | Kind, cheerful, devoted parents, and you are kind and resilient. The good life: does it last? |
| **The family secret** | `the_family_secret` | 1004 | 1990, you are the grandfather (72) | You play the grandfather. Years ago he abused his grandchild Elias, who is 22 now. It happened before you take over, and it is never shown or played. What you play is the secret, the guilt and the reckoning: Sunday dinner, confessing or keeping quiet, the family finding out, the police, prison or denial. **Dark theme.** |
| **Born bad** | `born_bad` | 1005 | 1995, age 17 | You are cruel, hot-tempered and criminal-minded. Crime, temptations, the police, prison, a way back. |
| **Scandal** | `scandal` | 1006 | 1997, age 12 | Both parents are unfaithful, dishonest or jealous. Affairs, revealed secrets, divorce, a half-sibling. |
| **Grown up** | `grown_up` | 1007 | 1996, age 26 | Skips childhood. You have a partner, 150,000 kr and job offers waiting. Adult life, love, money and work. |
| **Late in life** | `late_in_life` | 28 | 2014, age 62 | Married, with two grown children and grandchildren. 900,000 kr and a home of your own. Retirement, wills, health, death and succession. |

## Tips

- *Nothing to lose* and *Born bad* are good for the crime system. Try the actions under *Crime*.
- *The family secret*: the grandchild may tell the family any year. Try confessing, denying and moving away.
- *Late in life* is the quickest way to test death, the will and choosing an heir.
- Found something? Press F1 in the game and write it down, as usual. The note gets the scenario's save file.

## For developers

- `dotnet tools/OneMoreYear.SimRunner/bin/Debug/net8.0/OneMoreYear.SimRunner.dll --scenarios` prints where every scenario starts: the family, their traits, money, secrets and the first events.
- `[seed] [years] --scenario=id` (seed and years first; leave out the seed to use the scenario's) plays automatically on from a scenario.
- `--find-seed=id` tries seeds 1–30 for a scenario (children, grandchildren, partner, prison) when you need a better seed.
- A test checks that every scenario starts at the promised age with the promised traits.
