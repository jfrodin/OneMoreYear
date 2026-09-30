using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Content;

public sealed class TraitDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Opposite { get; set; }
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
}

public sealed class OccupationDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public double Weight { get; set; } = 1;
    /// <summary>Trait id → weight multiplier when choosing this track.</summary>
    public Dictionary<string, double> TraitAffinity { get; set; } = new();
    public List<OccupationLevelDef> Levels { get; set; } = new();
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
    public EducationLevel? MinEducation { get; set; }
    public EducationLevel? MaxEducation { get; set; }
    public int? MinChildren { get; set; }
    public int? MaxChildren { get; set; }
    public double? MinMoney { get; set; }
    public double? MaxMoney { get; set; }
    public double? MinHealth { get; set; }
    public double? MaxHealth { get; set; }
    public bool? OwnsHome { get; set; }
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
    public ConditionDef? Conditions { get; set; }
    public RoleDef? Target { get; set; }
    public RoleDef? Other { get; set; }
    public Dictionary<string, VarDef> Vars { get; set; } = new();
    public List<ChoiceDef> Choices { get; set; } = new();
}
