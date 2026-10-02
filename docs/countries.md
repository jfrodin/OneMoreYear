# Adding a country

A new country is data and events. No code needs to change. Sweden (`content/countries/sweden.json`)
is the reference; copy it and change what differs. The test `CountryTests` plays a made-up country
built from Sweden's numbers without any of its names, employers, decades or events, so the code
already copes with all of that missing.

## 1. The country file: `content/countries/<id>.json`

| Field | What it is | Sweden |
|---|---|---|
| `id`, `name`, `currencySymbol`, `currencyBefore` | `currencyBefore` puts the symbol first ($12,500) | `sweden`, `Sweden`, `kr`, false |
| `contentMoneyScale` | Shared content (salaries, event amounts, crimes) is written in Swedish kronor of 2020; this converts it to the country's money | 1 (USA 0.15) |
| `minStartYear`, `maxStartYear` | Which decades a life can begin in | 1950, 2020 |
| `priceIndex` | Price level by year, 2020 = 1.0. All money in content is in 2020 money | 0.025 in 1950 |
| `taxRate`, `livingCostAdult`, `livingCostChild`, `homePrice` | The economy, in 2020 money | 32 %, 150 000, 40 000, 3 000 000 |
| `unemploymentIncome`, `studentIncome`, `partTimeIncome` | Welfare and student aid; 0 where there is none | |
| `welfareShare` | How much of a shortfall welfare covers | 0.5 (USA 0.25) |
| `universityFee`, `medicalBill` | Tuition per year (becomes debt) and the bill for a serious illness (15 % with a job or pension) | 0, 0 |
| `monthlyPay` | Talk about pay per month (Sweden) or per year (USA) | true |
| `ageOfConsent`, `adultAge`, `cohabitWithConsentAge`, `marriageAge`, `cousinMarriageAllowed` | The law | 15, 18, 16, 18, true |
| `schoolStartAge`, `secondaryAge`, `gradesFromAge` | The school system | 7, 16, 14 |
| `adultEducation` | What adult education is called, for hints; leave out if there is none | Komvux |
| `incomeSpread`, `unpaidLeave`, `childcareCost` | Salary ladder stretch around the median, pay lost to unpaid parental leave, daycare per child under six | 1, 0, 0 (USA 1.3, 0.23, 9 000) |
| `secondarySchool`, `flat` | Everyday words, also as `{secondary}`, `{flat}` and `{a.flat}` in event text | upper secondary school, flat |
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
- A situation the code queues (`after_primary`, `choose_adult_education`) can have a country version: an event called `after_primary_usa` is used instead in the USA.
- History that should come for sure uses `"trigger": "history"`; life's big moments use `"trigger": "milestone"`.
- No dashes in game text (the content check rejects them).

## 5. Education and jobs

Programmes in `content/education.json` can list `"countries"` too (Komvux is Swedish), and `"localNames": { "usa": "College prep track" }` renames a programme in one country. Occupations
are shared; give a job a `minYear` if it did not exist earlier (IT from 1965).

## 6. Check

- `dotnet test` (the content check validates countries, events and programmes).
- `SimRunner --coverage=60` to see which events never reach the player.
- Play a life from the title screen: the country choice appears as soon as there are two countries.

## The USA (0.34.0)

Done: dollars first, US prices and incomes, tuition that becomes debt, medical bills by insurance
status, a thinner safety net, American names by heritage and era, employers, cities, decades,
investments with US crashes, high school tracks, community college, and events for the draft,
Kennedy, the March on Washington, Woodstock, the bicentennial, Challenger, September 11, the 2008
foreclosures, prom, the driving test, Thanksgiving, the Fourth, Halloween and more.

In 0.35.0: `incomeSpread` (1.3) stretches the salary ladder, `unpaidLeave` (0.23 of a year's pay
when a child arrives) and `childcareCost` (daycare under six when nobody is home), family insurance
through a spouse or parent, the `heritage` condition, and 32 more events of everyday American life.

Not yet: emigration between countries.
