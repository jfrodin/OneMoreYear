using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Core;

/// <summary>Everything a system needs: the world, the content and a few shared helpers.</summary>
public sealed class SimContext
{
    public World World { get; }
    public ContentDb Content { get; }
    /// <summary>The country the story is in; it changes when the player emigrates.</summary>
    public CountryDef Country => Content.Countries[World.CountryId];

    public SimContext(World world, ContentDb content)
    {
        World = world;
        Content = content;
        if (!content.Countries.ContainsKey(world.CountryId)) throw new InvalidDataException($"Unknown country: {world.CountryId}");
    }

    public SimRandom Rng => World.Rng;
    public int Year => World.Year;

    public double Mod(Person p, string key) => Content.TraitModifier(p, key);

    /// <summary>The player's setting for a dark theme (content settings).</summary>
    public ContentLevel Level(string category) =>
        ContentCategories.Withdrawn.Contains(category) ? ContentLevel.Off : World.ContentSettings.GetValueOrDefault(category, ContentLevel.On);

    /// <summary>Can this theme happen at all in this world?</summary>
    public bool Happens(string category) => Level(category) != ContentLevel.Off;

    /// <summary>May the player get events and choices about this theme?</summary>
    public bool Shown(string category) => Level(category) == ContentLevel.On;

    /// <summary>
    /// How important news about these people is for the chronicle: 3 if it involves the player,
    /// 2 for the player's partner, parents, children and siblings, otherwise 1.
    /// <paramref name="major"/> events (births, deaths, marriages, divorces) are one step more important.
    /// </summary>
    public int Importance(bool major, params Person?[] people)
    {
        var w = World;
        if (w.TryGet(w.PlayerId) is not { } player) return 1; // still setting up the world
        int best = 1;
        foreach (var p in people)
        {
            if (p == null) continue;
            if (p.Id == player.Id) { best = 3; break; }
            bool close = player.PartnerId == p.Id || player.ParentIds.Contains(p.Id) || player.ChildIds.Contains(p.Id)
                         || p.ParentIds.Intersect(player.ParentIds).Any();
            if (close) best = Math.Max(best, major ? 3 : 2);
            else if (major && p.IsBlood) best = Math.Max(best, 2);
        }
        return best;
    }

    /// <summary>Nominal money level for a year relative to 2020 (=1). Interpolates the country's anchors.</summary>
    public double MoneyIndex(int year) => Interpolate(Country.PriceIndex, year, extrapolateGrowth: 0.025);

    public double DivorceIndex => Interpolate(Country.DivorceIndex, Year, 0);
    public double FertilityIndex => Interpolate(Country.FertilityIndex, Year, 0);

    /// <summary>Converts an amount in 2020-kronor to this year's nominal kronor.</summary>
    public double Nominal(double amount2020) => amount2020 * MoneyIndex(Year);

    /// <summary>A shared-content amount (Swedish kronor of 2020) in this country's 2020 money.</summary>
    public double Ref(double referenceAmount) => referenceAmount * Country.ContentMoneyScale;

    /// <summary>A shared-content amount in this year's local money: <see cref="Ref"/> then <see cref="Nominal"/>.</summary>
    public double NominalRef(double referenceAmount) => Nominal(Ref(referenceAmount));

    /// <summary>Converts this year's nominal kronor to 2020-kronor.</summary>
    public double Real(double nominal) => nominal / MoneyIndex(Year);

    public static double Interpolate(Dictionary<int, double> anchors, int year, double extrapolateGrowth)
    {
        if (anchors.Count == 0) return 1;
        var keys = anchors.Keys.OrderBy(k => k).ToList();
        if (year <= keys[0]) return anchors[keys[0]];
        if (year >= keys[^1]) return anchors[keys[^1]] * Math.Pow(1 + extrapolateGrowth, year - keys[^1]);
        for (int i = 0; i < keys.Count - 1; i++)
        {
            if (year >= keys[i] && year <= keys[i + 1])
            {
                double t = (double)(year - keys[i]) / (keys[i + 1] - keys[i]);
                return anchors[keys[i]] + (anchors[keys[i + 1]] - anchors[keys[i]]) * t;
            }
        }
        return 1;
    }
}
