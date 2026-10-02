# Adding a country

A new country is data and events. No code needs to change. Sweden (`content/countries/sweden.json`)
is the reference; copy it and change what differs. The test `CountryTests` plays a made-up country
built from Sweden's numbers without any of its names, employers, decades or events, so the code
already copes with all of that missing.

## 1. The country file: `content/countries/<id>.json`

| Field | What it is | Sweden |
|---|---|---|
| `id`, `name`, `currencySymbol` | | `sweden`, `Sweden`, `kr` |
| `minStartYear`, `maxStartYear` | Which decades a life can begin in | 1950, 2020 |
| `priceIndex` | Price level by year, 2020 = 1.0. All money in content is in 2020 money | 0.025 in 1950 |
| `taxRate`, `livingCostAdult`, `livingCostChild`, `homePrice` | The economy, in 2020 money | 32 %, 150 000, 40 000, 3 000 000 |
| `unemploymentIncome`, `studentIncome`, `partTimeIncome` | Welfare and student aid; 0 where there is none | |
| `monthlyPay` | Talk about pay per month (Sweden) or per year (USA) | true |
| `ageOfConsent`, `adultAge`, `cohabitWithConsentAge`, `marriageAge`, `cousinMarriageAllowed` | The law | 15, 18, 16, 18, true |
| `schoolStartAge`, `secondaryAge`, `gradesFromAge` | The school system | 7, 16, 14 |
| `adultEducation` | What adult education is called, for hints; leave out if there is none | Komvux |
| `pensionAge`, `pensionRate`, `minimumPension` | Retirement | 65, 0.65, 110 000 |
| `mortalityScale` | Life expectancy compared to Sweden | 1.0 |
| `divorceIndex`, `fertilityIndex` | How common divorce and children are, by year | |
| Market fields | Real returns and spreads for savings, debt, stocks and housing; mortgage rate, amortization, down payment | |
| `spouseInheritanceShare`, `sameSexCoupleChance`, `wifeTakesNameChance` | Family and law | |
| `decades` | Name and a few lines for each decade: the title screen and the chapter pages | "the record years" |
| `investments` | Funds and fictional companies, with history's shocks for that country | Vasabanken 1992 |
| `homeTypes` | Kinds of home with price and rent factors | from a room to a house |
| `cities` | Name, size, weight (how many live there) and price factor | Stockholm 1.6 |
| `historicalEvents` | Front-page history, job losses and crashes by year | Palme 1986 |
| `maleNames`, `femaleNames`, `lastNames` | Fallback names | |

## 2. Names: `content/names/<id>.json`

Heritages with their share of the population by year (the first one is the majority), and first
names by heritage, sex and birth years. Without this file the fallback names in the country file
are used.

## 3. Employers: `content/employers/<id>.json`

Name parts for fictional employers per kind of job. Without it, jobs have no employer name.

## 4. Events

Events are shared by all countries unless they list `"countries": [ "<id>" ]`. Tag anything that only
makes sense in one country (Midsummer, Dagen H, Komvux, military service rules) and write the new
country's own: holidays, history, school, healthcare, the draft.

- Money in text: `{money:40}` shows 40 (2020 money) in the country's currency at that year's prices.
- Things that change with time: `[[1990: before || from then on]]`.
- History that should come for sure uses `"trigger": "history"`; life's big moments use `"trigger": "milestone"`.
- No dashes in game text (the content check rejects them).

## 5. Education and jobs

Programmes in `content/education.json` can list `"countries"` too (Komvux is Swedish). Occupations
are shared; give a job a `minYear` if it did not exist earlier (IT from 1965).

## 6. Check

- `dotnet test` (the content check validates countries, events and programmes).
- `SimRunner --coverage=60` to see which events never reach the player.
- Play a life from the title screen: the country choice appears as soon as there are two countries.

## Next: the USA

What differs most, and needs new mechanics or events rather than only numbers:
healthcare that costs money (medical bills, insurance through work), student debt, the draft for
Vietnam (1964–1973), no paid parental leave, school from 5 or 6 and high school graduation at 18,
monthly pay off, tipping, credit scores, a much wider spread of incomes.
