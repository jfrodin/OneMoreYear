using System.Text.Json.Serialization;

namespace OneMoreYear.Simulation.Model;

/// <summary>
/// How <see cref="FromId"/> feels about <see cref="ToId"/>. Relationships are directional:
/// a brother can be bitter towards you while you feel nothing special about him.
/// All dimensions range 0–100.
/// </summary>
public sealed class Relationship
{
    public int FromId { get; set; }
    public int ToId { get; set; }
    public double Closeness { get; set; }
    public double Respect { get; set; } = 50;
    public double Trust { get; set; } = 50;
    public double Attraction { get; set; }
    public double Fear { get; set; }
    public double Envy { get; set; }
    public double Bitterness { get; set; }
    /// <summary>Last year the two actively spent time together.</summary>
    public int LastContactYear { get; set; }

    public double this[RelDim dim]
    {
        get => dim switch
        {
            RelDim.Closeness => Closeness,
            RelDim.Respect => Respect,
            RelDim.Trust => Trust,
            RelDim.Attraction => Attraction,
            RelDim.Fear => Fear,
            RelDim.Envy => Envy,
            RelDim.Bitterness => Bitterness,
            _ => 0
        };
        set
        {
            double v = Math.Clamp(value, 0, 100);
            switch (dim)
            {
                case RelDim.Closeness: Closeness = v; break;
                case RelDim.Respect: Respect = v; break;
                case RelDim.Trust: Trust = v; break;
                case RelDim.Attraction: Attraction = v; break;
                case RelDim.Fear: Fear = v; break;
                case RelDim.Envy: Envy = v; break;
                case RelDim.Bitterness: Bitterness = v; break;
            }
        }
    }

    /// <summary>Overall opinion from -100 (hatred) to +100 (love).</summary>
    [JsonIgnore] public double Opinion =>
        Math.Clamp(Closeness * 0.6 + Trust * 0.3 + Respect * 0.2 - Bitterness * 0.9 - Envy * 0.3 - Fear * 0.2 - 35, -100, 100);
}
