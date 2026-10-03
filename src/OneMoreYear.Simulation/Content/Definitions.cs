using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Content;

public sealed class TraitDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Opposite { get; set; }
    /// <summary>"light", "dark" or "odd" – shown as a colour in the UI.</summary>
    public string Tone { get; set; } = "light";
    /// <summary>How common the trait is when people are born (1 = normal).</summary>
    public double Weight { get; set; } = 1;
    /// <summary>Only appears in adulthood (e.g. partner preferences); rolled at 18 instead of at birth.</summary>
    public bool AdultOnly { get; set; }
    /// <summary>Chance that a child picks the trait up from a parent who has it.</summary>
    public double Inherit { get; set; } = 0.3;
    /// <summary>Extra weight by sex, e.g. more men than women prefer much younger partners.</summary>
    public double MaleWeight { get; set; } = 1;
    public double FemaleWeight { get; set; } = 1;
    /// <summary>
    /// Named modifiers read by the simulation, e.g. "career", "social", "infidelity".
    /// Summed over a person's traits.
    /// </summary>
    public Dictionary<string, double> Modifiers { get; set; } = new();
}

public sealed class HistoricalEventDef
{
    public int Year { get; set; }
    public string Text { get; set; } = "";
    /// <summary>Extra chance for every working person to lose their job this year.</summary>
    public double JobLossChance { get; set; }
    /// <summary>Multiplier applied to positive savings (e.g. 0.85 for a crash).</summary>
    public double SavingsFactor { get; set; } = 1;
}

/// <summary>
/// Something to invest in. Each year it returns the bank rate plus <see cref="Beta"/> times the stock
/// market's excess return, plus luck of its own (<see cref="Spread"/>), unless history decides
/// (<see cref="Shocks"/>). Shares pay a dividend; funds keep theirs.
/// </summary>
public sealed class AssetDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>"fund" or "shares".</summary>
    public string Kind { get; set; } = "fund";
    public string Description { get; set; } = "";
    public double Beta { get; set; } = 1;
    public double Spread { get; set; }
    /// <summary>Yearly dividend as a share of the value (shares only).</summary>
    public double Dividend { get; set; }
    /// <summary>Yearly chance of going bankrupt (companies).</summary>
    public double Bankruptcy { get; set; }
    public int MinYear { get; set; } = 1900;
    /// <summary>Returns forced by history, by year: the IT crash, the bank crisis.</summary>
    public Dictionary<int, double> Shocks { get; set; } = new();
}

/// <summary>A kind of home. Price is relative to the country's typical home in the city; rent to the usual housing cost.</summary>
public sealed class HomeTypeDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>0 = not for sale (a room in a shared flat).</summary>
    public double PriceFactor { get; set; }
    /// <summary>0 = not for rent (most houses).</summary>
    public double RentFactor { get; set; }
    /// <summary>How many people it suits, for the hint ("fits a family").</summary>
    public int Sleeps { get; set; } = 2;
    /// <summary>A house with its own garden (garden projects are possible).</summary>
    public bool Garden { get; set; }
    public int MinYear { get; set; } = 1900;
}

/// <summary>A decade in a country: "the record years", and a few lines about what it was like.</summary>
public sealed class DecadeDef
{
    public int Year { get; set; }
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class CityDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>"city", "town" or "village".</summary>
    public string Size { get; set; } = "city";
    public double Weight { get; set; } = 1;
    /// <summary>How expensive housing is compared to the country average (1 = average).</summary>
    public double PriceFactor { get; set; } = 1;
}

public sealed class CountryDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string CurrencySymbol { get; set; } = "kr";
    /// <summary>
    /// Shared content (salaries, event amounts, crimes) is written in Swedish kronor of 2020. This converts
    /// it to the country's own 2020 money (about 0.13 for dollars). The country's own fields are already local.
    /// </summary>
    public double ContentMoneyScale { get; set; } = 1;
    /// <summary>The symbol goes before the amount ($12,500) instead of after (12,500 kr).</summary>
    public bool CurrencyBefore { get; set; }
    /// <summary>Yearly university tuition, 2020 money (0 where university is free). Paid as debt: student loans.</summary>
    public double UniversityFee { get; set; }
    /// <summary>What a serious illness costs the patient, 2020 money (0 where healthcare is free).</summary>
    public double MedicalBill { get; set; }
    /// <summary>Share of a shortfall that welfare covers when savings are gone (the rest becomes debt).</summary>
    public double WelfareShare { get; set; } = 0.5;
    /// <summary>How stretched the salary ladder is: 1 keeps the content's Swedish spread, 1.3 is American.</summary>
    public double IncomeSpread { get; set; } = 1.0;
    /// <summary>The share of a year's pay a working parent loses when a child arrives, where leave is unpaid (USA: 12 weeks). 0 where leave is paid.</summary>
    public double UnpaidLeave { get; set; }
    /// <summary>Daycare per child under 6 a year, when no parent stays home. 0 where it is part of the child's living cost.</summary>
    public double ChildcareCost { get; set; }
    public int MinStartYear { get; set; } = 1950;
    public int MaxStartYear { get; set; } = 2020;
    public List<string> MaleNames { get; set; } = new();
    public List<string> FemaleNames { get; set; } = new();
    public List<string> LastNames { get; set; } = new();
    /// <summary>Year → price level relative to 2020 (=1.0). Interpolated between anchors.</summary>
    public Dictionary<int, double> PriceIndex { get; set; } = new();
    /// <summary>Year → multiplier for how common divorce is.</summary>
    public Dictionary<int, double> DivorceIndex { get; set; } = new();
    /// <summary>Year → multiplier for how many children couples have.</summary>
    public Dictionary<int, double> FertilityIndex { get; set; } = new();
    public double TaxRate { get; set; } = 0.3;
    /// <summary>Basic yearly cost of living per adult including housing, 2020-kronor.</summary>
    public double LivingCostAdult { get; set; }
    public double LivingCostChild { get; set; }
    public double HomePrice { get; set; }
    public double UnemploymentIncome { get; set; }
    public double StudentIncome { get; set; }
    /// <summary>Extra yearly income from a part-time job while studying, 2020-kronor.</summary>
    public double PartTimeIncome { get; set; } = 70000;
    /// <summary>Show pay per month (as people talk about it in Sweden) instead of per year.</summary>
    public bool MonthlyPay { get; set; }
    /// <summary>Age of consent: romantic/sexual relationships and pregnancy in normal life start here.</summary>
    public int AgeOfConsent { get; set; } = 15;
    /// <summary>Legal adulthood: own decisions such as moving in together without parents' consent.</summary>
    public int AdultAge { get; set; } = 18;
    /// <summary>Youngest age to move in with a partner, and then only with the parents' consent.</summary>
    public int CohabitWithConsentAge { get; set; } = 16;
    public int MarriageAge { get; set; } = 18;
    /// <summary>Whether first cousins may become a couple (legal in Sweden).</summary>
    public bool CousinMarriageAllowed { get; set; } = true;
    // The school system.
    /// <summary>Age children start school, and the age compulsory school ends and upper secondary (or work) begins.</summary>
    public int SchoolStartAge { get; set; } = 7;
    public int SecondaryAge { get; set; } = 16;
    /// <summary>The age school starts giving grades (year 8 in Sweden).</summary>
    public int GradesFromAge { get; set; } = 14;
    /// <summary>What adult education is called here ("Komvux"), for hints; null if there is none.</summary>
    public string? AdultEducation { get; set; }
    /// <summary>What the school after compulsory school is called ("upper secondary school", "high school").</summary>
    public string SecondarySchool { get; set; } = "upper secondary school";
    /// <summary>The language people speak at home ("Swedish", "English").</summary>
    public string Language { get; set; } = "English";
    /// <summary>The everyday word for a rented home ("flat", "apartment").</summary>
    public string Flat { get; set; } = "flat";
    public int PensionAge { get; set; } = 65;
    public double PensionRate { get; set; } = 0.6;
    public double MinimumPension { get; set; }
    public double MortalityScale { get; set; } = 1;
    // Money markets, as real returns on top of inflation (from priceIndex). See Systems.Market.
    /// <summary>Real interest on money in the bank (about zero: savings keep their value, no more).</summary>
    public double SavingsRealReturn { get; set; } = 0.005;
    /// <summary>Real interest on unsecured debt.</summary>
    public double DebtRealInterest { get; set; } = 0.06;
    /// <summary>Average real return of the stock market, and its yearly spread.</summary>
    public double MarketRealReturn { get; set; } = 0.05;
    public double MarketVolatility { get; set; } = 0.16;
    /// <summary>Average real growth of home prices, and its yearly spread.</summary>
    public double HousingRealGrowth { get; set; } = 0.015;
    public double HousingVolatility { get; set; } = 0.05;
    public double MortgageRealRate { get; set; } = 0.025;
    /// <summary>Share of the original loan paid off each year.</summary>
    public double Amortization { get; set; } = 0.02;
    /// <summary>Share of the home price you must pay yourself.</summary>
    public double DownPayment { get; set; } = 0.15;
    /// <summary>Share of the estate that goes to a surviving spouse.</summary>
    public double SpouseInheritanceShare { get; set; } = 0.5;
    public double SameSexCoupleChance { get; set; } = 0.04;
    public double WifeTakesNameChance { get; set; } = 0.7;
    public List<CityDef> Cities { get; set; } = new();
    public List<HistoricalEventDef> HistoricalEvents { get; set; } = new();
    /// <summary>What each decade felt like in this country: the title screen and the chapter pages.</summary>
    public List<DecadeDef> Decades { get; set; } = new();
    /// <summary>Funds and companies the player can invest in (fictional companies, real kinds of risk).</summary>
    public List<AssetDef> Investments { get; set; } = new();
    /// <summary>Kinds of home, from a room in a shared flat to a house; prices and rents relative to the city.</summary>
    public List<HomeTypeDef> HomeTypes { get; set; } = new();
}

public sealed class OccupationLevelDef
{
    public string Title { get; set; } = "";
    /// <summary>Yearly gross salary, 2020-kronor.</summary>
    public double Salary { get; set; }
    public EducationLevel MinEducation { get; set; }
    /// <summary>Whether someone can be hired directly into this level.</summary>
    public bool Entry { get; set; }
    public double PromotionChance { get; set; } = 0.1;
    /// <summary>If set, one of these degrees (programme ids) is needed for this level.</summary>
    public List<string>? RequiresDegree { get; set; }
}

/// <summary>A crime someone can commit (content/crimes.json).</summary>
public sealed class CrimeDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Past tense for the chronicle: "stole a car", "beat up" (followed by the victim).</summary>
    public string Did { get; set; } = "";
    public int MinAge { get; set; }
    public double GainMin { get; set; }
    public double GainMax { get; set; }
    public double CatchChance { get; set; }
    public double Fine { get; set; }
    public int PrisonMin { get; set; }
    public int PrisonMax { get; set; }
    public double Guilt { get; set; }
    public bool Targeted { get; set; }
    public bool Violent { get; set; }
}

/// <summary>A secondary or university programme. Completing it gives a degree (its id).</summary>
public sealed class ProgrammeDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>The name in other countries, by country id; <see cref="Name"/> elsewhere.</summary>
    public Dictionary<string, string> LocalNames { get; set; } = new();
    public string NameIn(string countryId) => LocalNames.TryGetValue(countryId, out var n) ? n : Name;
    public string Description { get; set; } = "";
    public EducationLevel Level { get; set; }
    public int Years { get; set; } = 3;
    public double MinGrades { get; set; }
    /// <summary>An academic secondary programme; vocational ones need higher grades for university.</summary>
    public bool Academic { get; set; }
    /// <summary>Courses for adults (Komvux, vocational courses): nobody starts them straight from school.</summary>
    public int MinAge { get; set; }
    public int MinYear { get; set; } = 1900;
    /// <summary>The countries that have this programme (Komvux is Swedish); empty = everywhere.</summary>
    public List<string> Countries { get; set; } = new();
    public List<string> LeadsTo { get; set; } = new();
    public Dictionary<string, double> TraitAffinity { get; set; } = new();
}

public sealed class OccupationDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public double Weight { get; set; } = 1;
    /// <summary>Trait id → weight multiplier when choosing this track.</summary>
    public Dictionary<string, double> TraitAffinity { get; set; } = new();
    public List<OccupationLevelDef> Levels { get; set; } = new();
    /// <summary>Kind of workplace, used by events: "office", "manual", "care", "school" ...</summary>
    public List<string> Tags { get; set; } = new();
    /// <summary>The first year anyone does this job (IT work did not exist in 1950).</summary>
    public int MinYear { get; set; } = 1900;
}

/// <summary>
/// Conditions on a person. All set fields must match. Money is in 2020-kronor.
/// Relationship conditions (Opinion / Rel*) describe how this person feels about the player.
/// </summary>
public sealed class ConditionDef
{
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public Sex? Sex { get; set; }
    public bool? HasPartner { get; set; }
    public bool? Married { get; set; }
    public PartnerStatus? PartnerStatus { get; set; }
    /// <summary>For other people: whether they and the player could be a couple (orientation, age 16+).</summary>
    public bool? CompatibleWithPlayer { get; set; }
    public bool? SameSexAsPlayer { get; set; }
    public bool? HasJob { get; set; }
    public Activity? Activity { get; set; }
    public List<Activity>? NotActivity { get; set; }
    public EducationLevel? MinEducation { get; set; }
    public EducationLevel? MaxEducation { get; set; }
    public int? MinChildren { get; set; }
    public int? MaxChildren { get; set; }
    public double? MinMoney { get; set; }
    public double? MaxMoney { get; set; }
    public double? MinHealth { get; set; }
    public double? MaxHealth { get; set; }
    public bool? OwnsHome { get; set; }
    public bool? HasInvestments { get; set; }
    /// <summary>Has the down payment and an income the bank accepts.</summary>
    public bool? CanBuyHome { get; set; }
    public bool? HasMortgage { get; set; }
    /// <summary>Owns the home themselves (not just living in a partner's).</summary>
    public bool? HoldsHome { get; set; }
    public bool? LivesWithParents { get; set; }
    /// <summary>The person's heritage is one of these (see content/names).</summary>
    public List<string>? Heritage { get; set; }
    /// <summary>Lives in another country than the story (relatives who stayed behind).</summary>
    public bool? Abroad { get; set; }
    public List<string>? TraitsAny { get; set; }
    public List<string>? TraitsNone { get; set; }
    public List<string>? Flags { get; set; }
    public List<string>? NotFlags { get; set; }
    public double? MinOpinion { get; set; }
    public double? MaxOpinion { get; set; }
    public Dictionary<RelDim, double>? RelMin { get; set; }
    public Dictionary<RelDim, double>? RelMax { get; set; }
    /// <summary>For the player: requires a living person in this role (e.g. "sibling").</summary>
    public List<string>? HasRole { get; set; }
    /// <summary>The person's job must have one of these workplace tags.</summary>
    public List<string>? JobTags { get; set; }
    /// <summary>Works in one of these occupations (ids from occupations.json).</summary>
    public List<string>? Jobs { get; set; }
    /// <summary>Has a living pet: "any", "dog" or "cat"; "none" for no pet.</summary>
    public string? Pet { get; set; }
    /// <summary>At least this level in these skills.</summary>
    public Dictionary<string, int>? MinSkills { get; set; }
    /// <summary>Has this hobby now.</summary>
    public string? Hobby { get; set; }
    /// <summary>Runs a family business (true) or does not (false).</summary>
    public bool? RunsBusiness { get; set; }
    public bool? OwnsRental { get; set; }
    public double? MinGrades { get; set; }
    public double? MaxGrades { get; set; }
    /// <summary>Calendar year limits (for events of their time: a mobile phone, the fall of the Wall).</summary>
    public int? MinYear { get; set; }
    public int? MaxYear { get; set; }
    /// <summary>Years together with the current partner.</summary>
    public int? MinPartnerYears { get; set; }
    /// <summary>Has at least one of these ailments / none of these.</summary>
    public List<string>? AilmentsAny { get; set; }
    public List<string>? NotAilments { get; set; }
    /// <summary>Struggling with an addiction right now.</summary>
    public bool? Addicted { get; set; }
    /// <summary>Attracted to people of the same sex.</summary>
    public bool? SameSexAttraction { get; set; }
    /// <summary>At most this many children still living at home.</summary>
    public int? MaxChildrenAtHome { get; set; }
}

/// <summary>Picks another participant in an event, relative to the player.</summary>
public sealed class RoleDef
{
    /// <summary>
    /// partner, parent, child, sibling, grandparent, grandchild, relative, friend, ex,
    /// partner_of_target, new_person.
    /// </summary>
    public string Role { get; set; } = "";
    public ConditionDef? Conditions { get; set; }
    /// <summary>For new_person: age relative to the player.</summary>
    public int AgeOffsetMin { get; set; } = -4;
    public int AgeOffsetMax { get; set; } = 4;
    /// <summary>For new_person: "same", "opposite" or "attracted" (matches player's orientation).</summary>
    public string? Sex { get; set; }
}

public sealed class VarDef
{
    /// <summary>Base amount in 2020-kronor.</summary>
    public double Base { get; set; }
    /// <summary>Add this fraction of the named person's yearly income.</summary>
    public double IncomeFactor { get; set; }
    public string IncomeOf { get; set; } = "player";
    public double RandomMin { get; set; } = 1;
    public double RandomMax { get; set; } = 1;
}

public sealed class EffectDef
{
    public string Type { get; set; } = "";
    public string Who { get; set; } = "player";
    public string? To { get; set; }
    public RelDim? Dim { get; set; }
    public double Amount { get; set; }
    /// <summary>Name of an event variable to use as amount (multiplied by <see cref="Amount"/> if set, else 1).</summary>
    public string? Var { get; set; }
    public bool Mutual { get; set; }
    public string? Kind { get; set; }
    public string? Text { get; set; }
    public double Impact { get; set; }
    public string? Trait { get; set; }
    public string? Flag { get; set; }
    public int Importance { get; set; } = 2;
    public EducationLevel? Level { get; set; }
    /// <summary>Programme id for "study".</summary>
    public string? Programme { get; set; }
    /// <summary>Event id for "queue_event".</summary>
    public string? Event { get; set; }
    /// <summary>emigrate: the country to move to.</summary>
    public string? Country { get; set; }
    public string? Cause { get; set; }
    public double Chance { get; set; } = 1;
}

public sealed class OutcomeDef
{
    public string? Text { get; set; }
    public List<EffectDef> Effects { get; set; } = new();
}

public sealed class ChoiceDef
{
    public string Text { get; set; } = "";
    /// <summary>Short explanation shown under the choice (why it is available / what it risks).</summary>
    public string? Hint { get; set; }
    public ConditionDef? Requires { get; set; }
    /// <summary>A choice only people with this trait even think of ("[Charming] Talk your way out of it"). Hidden for others.</summary>
    public string? Trait { get; set; }
    /// <summary>Plain effects that always happen.</summary>
    public List<EffectDef> Effects { get; set; } = new();
    public string? Result { get; set; }
    /// <summary>If set, the choice succeeds with this probability (modified by traits).</summary>
    public double? Chance { get; set; }
    /// <summary>Trait id → added success chance (for the player).</summary>
    public Dictionary<string, double> ChanceTraits { get; set; } = new();
    /// <summary>Added chance per point of target opinion of the player (e.g. 0.005).</summary>
    public double ChanceOpinion { get; set; }
    /// <summary>Attribute ("smarts", "looks", "fitness", "grades") → added chance per point above 50.</summary>
    public Dictionary<string, double> ChanceAttributes { get; set; } = new();
    /// <summary>Chance per skill level: { "cooking": 0.03 } adds 3 percent for every level of cooking.</summary>
    public Dictionary<string, double> ChanceSkills { get; set; } = new();
    /// <summary>How the target feels about the player → added chance per point above 40 (e.g. attraction).</summary>
    public Dictionary<RelDim, double> ChanceRelation { get; set; } = new();
    public OutcomeDef? Success { get; set; }
    public OutcomeDef? Failure { get; set; }
}

public sealed class EventDef
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    /// <summary>"random" (drawn yearly) or "situation" (only created by simulation code).</summary>
    public string Trigger { get; set; } = "random";
    public double Weight { get; set; } = 1;
    /// <summary>Years before it can fire again for the same player. 0 = only once per player.</summary>
    public int Cooldown { get; set; } = 10;
    /// <summary>Never happens twice with the same target person (e.g. a sibling's alibi).</summary>
    public bool OncePerTarget { get; set; }
    /// <summary>"career", "money" or "life" – where an action is shown in the UI.</summary>
    public string Category { get; set; } = "life";
    /// <summary>Choices generated by code before the static ones, e.g. "job_offers".</summary>
    public string? DynamicChoices { get; set; }
    /// <summary>Dark themes the event is about (Model.ContentCategories); hidden when the player turned them down.</summary>
    public List<string> Content { get; set; } = new();
    /// <summary>The countries the event belongs to (Midsummer is Swedish); empty = everywhere.</summary>
    public List<string> Countries { get; set; } = new();
    /// <summary>What the player's personality or gifts let them notice (passive checks, shown under the text).</summary>
    public List<InsightDef> Insights { get; set; } = new();
    public ConditionDef? Conditions { get; set; }
    public RoleDef? Target { get; set; }
    public RoleDef? Other { get; set; }
    public Dictionary<string, VarDef> Vars { get; set; } = new();
    public List<ChoiceDef> Choices { get; set; } = new();
}

/// <summary>A test scenario: a fixed seed plus tweaks to the starting family (content/scenarios.json).</summary>
public sealed class ScenarioDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ulong Seed { get; set; }
    public int StartYear { get; set; } = 1970;
    /// <summary>The player's age when the scenario hands over; earlier years are played automatically.</summary>
    public int Age { get; set; }
    public ScenarioTweak? Player { get; set; }
    public ScenarioTweak? Parents { get; set; }
    public ScenarioTweak? Father { get; set; }
    public ScenarioTweak? Mother { get; set; }
    public ScenarioTweak? Grandfather { get; set; }
    /// <summary>A storyline started when the scenario hands over ("hidden_father": the grandfather is also the father of his grandchild).</summary>
    public string? Storyline { get; set; }
    /// <summary>Who the player plays when the scenario hands over: null (the child), "grandfather" or "mother".</summary>
    public string? PlayAs { get; set; }
    /// <summary>What must be true when the scenario hands over; other seeds are tried in a fixed order until it is.</summary>
    public ScenarioRequirements? Requires { get; set; }
}

public sealed class ScenarioTweak
{
    public List<string>? Traits { get; set; }
    public double? Smarts { get; set; }
    public double? Looks { get; set; }
    public double? Fitness { get; set; }
    /// <summary>Savings in 2020-kronor (negative = debt), per person.</summary>
    public double? Money { get; set; }
    public bool? OwnsHome { get; set; }
    public bool? Unemployed { get; set; }
    public string? Addiction { get; set; }
}

/// <summary>Name parts for fictional employers in one country (content/employers).</summary>
public sealed class EmployerNamesDef
{
    public string Country { get; set; } = "";
    public List<string> Places { get; set; } = new();
    public List<string> BrandStarts { get; set; } = new();
    public List<string> BrandEnds { get; set; } = new();
    public Dictionary<string, List<string>> ByOccupation { get; set; } = new();
}

/// <summary>Names and heritages for one country (content/names).</summary>
public sealed class NamesDef
{
    public string Country { get; set; } = "";
    public List<HeritageDef> Heritages { get; set; } = new();
    public List<NameGroupDef> Groups { get; set; } = new();
}

public sealed class HeritageDef
{
    public string Id { get; set; } = "";
    /// <summary>How common the heritage is among people the family meets, by year.</summary>
    public Dictionary<int, double> Share { get; set; } = new();
    /// <summary>Surnames typical for the heritage; empty = the country's surnames.</summary>
    public List<string> LastNames { get; set; } = new();
    /// <summary>Skin tone range (0 light – 1 dark) for people with no parents in the world.</summary>
    public List<double> Skin { get; set; } = new() { 0.02, 0.25 };
    /// <summary>Chance of black or dark brown hair and brown eyes.</summary>
    public double DarkHair { get; set; } = 0.15;
}

public sealed class NameGroupDef
{
    public string Heritage { get; set; } = "";
    public string Sex { get; set; } = "";
    public int From { get; set; }
    public int To { get; set; }
    public List<string> Names { get; set; } = new();
}

/// <summary>A lasting health state such as depression or dementia (content/ailments.json).</summary>
public sealed class AilmentDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Content-settings category that governs it (e.g. "mental_health"), if any.</summary>
    public string? Content { get; set; }
    public double Happiness { get; set; }
    public double Health { get; set; }
    public double Performance { get; set; }
    public double Recovery { get; set; }
    public double TreatedRecovery { get; set; }
}

public sealed class ScenarioRequirements
{
    public bool? Partner { get; set; }
    public int? MinChildren { get; set; }
    public int? MinGrandchildren { get; set; }
    public bool? Working { get; set; }
    /// <summary>The player (before any play-as switch) must be of this sex ("male" / "female").</summary>
    public string? Sex { get; set; }
}

/// <summary>
/// A passive check: something in an event only a person with this trait (or enough of an attribute)
/// notices – "[Paranoid] He turns his phone away when you come in."
/// </summary>
public sealed class InsightDef
{
    public string? Trait { get; set; }
    /// <summary>"smarts", "looks" or "fitness", with the lowest value that notices.</summary>
    public string? Attribute { get; set; }
    public double Min { get; set; }
    public string Text { get; set; } = "";
}

/// <summary>Insights and trait choices added to an existing event (content/insights.json).</summary>
public sealed class EventPatchDef
{
    public string Event { get; set; } = "";
    public List<InsightDef> Insights { get; set; } = new();
    public List<ChoiceDef> Choices { get; set; } = new();
}

/// <summary>
/// Something to strive for across families (docs/endgame.md). The check is code (AchievementSystem), keyed by id.
/// Common ones are shown in full; rare and legendary ones show only their hint until unlocked; secret ones are not shown at all.
/// </summary>
public sealed class AchievementDef
{
    public string Id { get; set; } = "";
    /// <summary>common, rare, legendary or secret.</summary>
    public string Tier { get; set; } = "common";
    public string Name { get; set; } = "";
    /// <summary>A cryptic line shown while it is locked (rare and legendary).</summary>
    public string Hint { get; set; } = "";
    /// <summary>Shown once unlocked: in the game's voice, not "Achievement unlocked".</summary>
    public string Text { get; set; } = "";
}

/// <summary>
/// A life dream (docs/endgame.md): something a played person wants from life. The goal kinds are
/// code (DreamSystem); everything else is data.
/// </summary>
/// <summary>
/// Something to do to the home one owns (HomeProjects, from The Sims' build mode): a new kitchen, a
/// garden, a sauna. It costs money, adds part of it to the home's value and makes life there better.
/// Kitchens and bathrooms wear out and can be done again.
/// </summary>
public sealed class HomeProjectDef
{
    public string Id { get; set; } = "";
    /// <summary>"A new kitchen": the button.</summary>
    public string Name { get; set; } = "";
    /// <summary>One line on what it is.</summary>
    public string Text { get; set; } = "";
    /// <summary>What it is like when it is done.</summary>
    public string Done { get; set; } = "";
    /// <summary>When it goes over budget.</summary>
    public string Overrun { get; set; } = "";
    /// <summary>Cost in Swedish kronor of 2020.</summary>
    public double Cost { get; set; }
    /// <summary>Share of the cost added to the home's value.</summary>
    public double Value { get; set; } = 0.6;
    public double Happiness { get; set; } = 4;
    /// <summary>Only for a house with a garden.</summary>
    public bool Garden { get; set; }
    /// <summary>Only with children living at home.</summary>
    public bool Children { get; set; }
    /// <summary>Years until it is worn and can be done again (0: lasts).</summary>
    public int Lasts { get; set; }
    public int MinYear { get; set; } = 1900;
    public List<string> Countries { get; set; } = new();
    /// <summary>A skill that helps (doing it yourself costs less).</summary>
    public string? Skill { get; set; }
}

/// <summary>
/// A small wish for one year (The Sims' wants, WishSystem): something the player can do this year,
/// by an action or by getting somewhere. Kept, it gives a little happiness.
/// </summary>
public sealed class WishDef
{
    public string Id { get; set; } = "";
    /// <summary>"Spend an afternoon with {t.name}": shown in the side panel.</summary>
    public string Text { get; set; } = "";
    public ConditionDef? Conditions { get; set; }
    /// <summary>Who the wish is about (a role as in events), if anyone.</summary>
    public RoleDef? Target { get; set; }
    /// <summary>Kept by doing one of these actions this year, with the target when there is one.</summary>
    public List<string> Actions { get; set; } = new();
    /// <summary>Or kept by being like this before the year is over.</summary>
    public ConditionDef? Done { get; set; }
    public double Weight { get; set; } = 1;
    public double Happiness { get; set; } = 4;
    public List<string> Countries { get; set; } = new();
}

public sealed class DreamDef
{
    public string Id { get; set; } = "";
    /// <summary>"Become a doctor": shown as the choice and in the side panel.</summary>
    public string Name { get; set; } = "";
    /// <summary>One line on why this person wants it.</summary>
    public string Text { get; set; } = "";
    /// <summary>job, top_job, degree, children, grandchildren, married, home, big_home, wealth, emigrate, age, cottage, close_family, golden_wedding, never_divorced.</summary>
    public string Goal { get; set; } = "";
    public string? Param { get; set; }
    public double Amount { get; set; }
    /// <summary>How much more (or less) likely the dream is offered to people with these traits.</summary>
    public Dictionary<string, double> Traits { get; set; } = new();
    /// <summary>Offered only between these ages.</summary>
    public int MinAge { get; set; } = 16;
    public int MaxAge { get; set; } = 60;
    /// <summary>Given up at this age if not fulfilled (null: it lasts a lifetime).</summary>
    public int? Deadline { get; set; }
    public int MinYear { get; set; } = 1900;
    public List<string> Countries { get; set; } = new();
    public string Fulfilled { get; set; } = "";
    public string Failed { get; set; } = "";
}

/// <summary>
/// Something the family becomes known for when one of its reputation meters passes a threshold
/// (ReputationSystem), and loses again when it falls below another. It changes life for the family's own.
/// </summary>
public sealed class FamilyTraitDef
{
    public string Id { get; set; } = "";
    /// <summary>learning, wealth, warmth or notoriety.</summary>
    public string Meter { get; set; } = "";
    public double Gain { get; set; }
    public double Lose { get; set; }
    /// <summary>"A family of readers": shown in the family tree and as the reason in factor lists.</summary>
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    public string Gained { get; set; } = "";
    public string Lost { get; set; } = "";
}

/// <summary>A kind of heirloom (content/heirlooms.json).</summary>
public sealed class HeirloomDef
{
    public string Id { get; set; } = "";
    /// <summary>"pocket watch": the heirloom is called "Erik's pocket watch".</summary>
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    /// <summary>What it would sell for when new to the family, in reference kronor; antiques gain with age.</summary>
    public double Value { get; set; }
}

/// <summary>A kind of pet (content/pets.json).</summary>
public sealed class PetKindDef
{
    public string Id { get; set; } = "";
    /// <summary>"dog": "Bella the dog".</summary>
    public string Name { get; set; } = "";
    public int Lifespan { get; set; } = 12;
    public List<string> Names { get; set; } = new();
    /// <summary>Natures and how each shows: "playful" → "steals socks and brings them back".</summary>
    public Dictionary<string, string> Natures { get; set; } = new();
}

/// <summary>A hobby and the skill it builds (content/hobbies.json).</summary>
public sealed class HobbyDef
{
    public string Id { get; set; } = "";
    /// <summary>"Painting": the skill and the hobby share the name.</summary>
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    public int MinYear { get; set; } = 1900;
    /// <summary>People with these traits take it up more often (NPCs) and learn it faster.</summary>
    public List<string> Traits { get; set; } = new();
}

/// <summary>A kind of business someone can start (content/businesses.json). Money in reference kronor of 2020.</summary>
public sealed class BusinessKindDef
{
    public string Id { get; set; } = "";
    /// <summary>"bakery": "Start a bakery".</summary>
    public string Name { get; set; } = "";
    /// <summary>Name patterns: {surname}, {first}, {city}.</summary>
    public List<string> Names { get; set; } = new();
    public string Text { get; set; } = "";
    public double StartCost { get; set; }
    /// <summary>A normal year's profit at the start.</summary>
    public double Profit { get; set; }
    /// <summary>How much a year can swing, as a share of the profit.</summary>
    public double Risk { get; set; } = 0.5;
    /// <summary>The skill that helps (content/hobbies.json), if any.</summary>
    public string? Skill { get; set; }
    public int MinYear { get; set; } = 1900;
}
