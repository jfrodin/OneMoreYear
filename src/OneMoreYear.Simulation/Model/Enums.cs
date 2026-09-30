namespace OneMoreYear.Simulation.Model;

public enum Sex { Male, Female }

public enum EducationLevel { None = 0, Primary = 1, Secondary = 2, University = 3 }

public enum LifePhase { Childhood, Teen, Adult, Senior }

public enum PartnerStatus { None, Dating, Cohabiting, Married }

public enum RelDim { Closeness, Respect, Trust, Attraction, Fear, Envy, Bitterness }

/// <summary>What a person is currently doing with their days.</summary>
public enum Activity { Child, School, Studying, Working, Unemployed, Retired }

/// <summary>How closely two people are related by blood.</summary>
public enum BloodTie { None, Distant, FirstCousins, Close }
