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
    public int PensionAge { get; set; } = 65;
    public double PensionRate { get; set; } = 0.6;
    public double MinimumPension { get; set; }
    public double MortalityScale { get; set; } = 1;
    public double SavingsReturn { get; set; } = 0.03;
    public double DebtInterest { get; set; } = 0.06;
    /// <summary>Share of the estate that goes to a surviving spouse.</summary>
    public double SpouseInheritanceShare { get; set; } = 0.5;
    public double SameSexCoupleChance { get; set; } = 0.04;
    public double WifeTakesNameChance { get; set; } = 0.7;
    public List<CityDef> Cities { get; set; } = new();
    public List<HistoricalEventDef> HistoricalEvents { get; set; } = new();
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
    public string Description { get; set; } = "";
    public EducationLevel Level { get; set; }
    public int Years { get; set; } = 3;
    public double MinGrades { get; set; }
    /// <summary>An academic secondary programme; vocational ones need higher grades for university.</summary>
    public bool Academic { get; set; }
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
    public bool? LivesWithParents { get; set; }
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
    public double? MinGrades { get; set; }
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
    /// <summary>A storyline started when the scenario hands over ("abuse": the grandfather's secret).</summary>
    public string? Storyline { get; set; }
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
