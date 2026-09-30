# Design decisions

Decisions that shape the whole game. Add new ones at the bottom with a date.

## Age rating 18+ (PEGI 18 / Mature) – 2026-09-30

The game is made for adults, so content can be graphic and mature: sex, violence, drugs, crime,
abuse, addiction and dark family secrets can be shown openly rather than toned down. Affects event
writing, text, images and store pages (Steam mature content survey, age gate). Consider a content
option for players who want to tone it down (done in 0.15.0: content settings per theme – On,
Mentioned only or Off; new dark events must get a "content" tag).

## Full tonal range: from sunshine to pitch black – 2026-09-30

Family happiness, love, weddings, grandchildren and roses – but also drugs, violent crime, addiction
and a grandfather who abuses his grandchildren. Both ends should exist in the same family, often in
the same life. The light moments matter as much as the dark ones; the contrast is what makes the
family feel real.

## How the darkest themes are handled – 2026-09-30

Abuse of children exists as a story element – a secret, a trauma, memories that shape a life, a
revelation that splits the family – shown through its consequences and how people react. It is
never depicted in sexually explicit detail. (This is also what keeps the game within PEGI 18 and
Steam's rules.)

## Relationships follow the country's law – 2026-09-30

In Sweden the age of consent is 15, so that is where romantic and sexual relationships – and
pregnancy in normal life – can start. Below that only innocent crushes and "going steady" between
kids of similar age. Moving in together from 16 with the parents' consent, from 18 on your own;
marriage from 18. Stored per country (`ageOfConsent`, `adultAge`, `cohabitWithConsentAge`,
`marriageAge`) so other countries follow their own laws. No romance between an adult and someone
under the age of consent – that belongs only to the dark themes above (abuse), never to normal dating.

## English first – 2026-09-30

All game text is English. Localization (Swedish and more) comes later; keep text in content files
where possible.

## Workflow – 2026-09-30

- Playtest notes go in `docs/playtest-notes.md` (F1 in the game).
- They are triaged into `docs/todo.md`, which only holds what is still open.
- When something is done it leaves the to-do list and is described in `CHANGELOG.md` under the
  version it shipped in. Each version is tagged in git (`v0.5.0`).

## The player can choose (almost) anything – 2026-09-30

Crime, violence (also at home, also against your own children), murder, drugs, cheating,
manipulation, abandoning your family: all of it can be the player's choice, with consequences.
Personality decides how tempting and how risky something is – criminal-minded or greedy people get
more opportunities and better odds, kind people can do the same things but feel guilt – but traits
never forbid a choice. Chance decides the outcome (getting caught, how the family reacts).

**The one exception:** sexual abuse of children is never something the player can choose to do. It
exists only as something other characters do – a secret and a trauma the family has to live with.
This is a firm line (and it also keeps the game releasable on Steam, consoles and within the law).

*Addition 2026-09-30:* the player may **take over** a character who did it in the past (the scenario
"The family secret", or an heir). The act is then backstory: never shown, never played, and never
repeated. Characters are not abusers while the player plays them. What gets played is the guilt,
the silence and the reckoning: confessing, denying, the family's reaction, the police and prison.
A content check stops the crime from ever being used as a player choice.

*Addition 2026-09-30 (2):* the producer wanted a 15-year-old granddaughter currently pregnant by
her grandfather, with the player as the grandfather. Declined: it makes the player the perpetrator
of ongoing abuse of a child. What was built instead is the *Chinatown* structure: the abuse of the
daughter lies decades back, the grandchild born from it is an adult, and the story is played from
the grandfather's, the mother's or the grandchild's side. (The mother was 15 when the child was born – a
fact of the backstory, never depicted.)

## Relationships within the family – 2026-09-30

- **Close relatives never become couples** – parents and children, siblings and half-siblings,
  grandparents, aunts/uncles and nieces/nephews. Not the player, not anyone else. (Illegal in
  Sweden for the closest relations, and incest themes are not accepted on Steam.) Biological
  relations count, including hidden fathers.
- **Cousins** can become a couple where the country's law allows it (`cousinMarriageAllowed`) –
  rare, and the family talks about it.
- **Step-siblings who grew up together** do not become couples. Step-siblings who meet as adults can.
- **Age gaps**: adults can be with anyone – some prefer much younger or much older partners (a trait,
  more common among men for younger, women for older). Big gaps make the family react. Under 18 the
  age rules above apply; an adult with a minor is never a normal romance – if it appears at all, it is
  as exploitation in the dark storylines, shown through its consequences.

## Shareable seeds, no ready-made scenarios – 2026-09-30

Like Balatro: every game has a seed, and players can share it ("play 7LB2WVPK – what a family!").
The same seed gives the same starting family and the same luck; your choices take it from there.
The game ships with **no** ready-made scenarios or seed lists – the test scenarios are a
development tool only. Seed codes are eight letters and digits (no I, O, 0, 1), shown under the
player card and on the game-over screen, typed in on the title screen (done in 0.14.0).

## The look: a family album that follows the decades – 2026-09-30

Chosen from three style probes (docs/style-probes):

- **The base is a family album**: paper instead of a dark app, cards like things glued into the
  album, portraits as prints with a white edge, memories in handwriting.
- **The look follows the decade being played**: colours blend smoothly and headings change
  typeface – sepia and typewriter in the fifties, brown and orange in the seventies, cool with
  magenta in the eighties, clean and flat after 2000. Photos are tinted like prints of their time.
- **Each new year opens with the family's newspaper**: the biggest family news as the headline,
  the rest in brief, and the world in the side column.
- Fonts are open (OFL / Apache) and live in `game/fonts` with their licences.

## Sexual violence between adults, and the dark themes in depth – 2026-09-30

Sexual violence is far more common between adults than against children, and a game about real
lives should include it. The same line applies as for children: **it exists in the world, but the
player can never choose to commit it.** (Steam removed a game with playable rape in 2019; consoles
do not allow it; and the story is in the consequences, not the act.) Never depicted explicitly.

What is played: the player as a survivor (assault, harassment at work, a partner who coerces) –
telling, reporting, staying silent, a trial, who believes you, trauma and healing, with the era
mattering (the silence of the seventies, #MeToo in 2017); a loved one as a survivor (how do you
support them?); a relative or partner as the perpetrator (a secret that splits the family).

All dark themes are to be built out properly, not left as footnotes: psychological violence and
coercive control, stalking, relapse and treatment in addiction, mental illness (depression,
anxiety, burnout), suicide and self-harm as grief and consequence, dementia, eviction and
homelessness. Each gets a content-settings category where it needs one.
