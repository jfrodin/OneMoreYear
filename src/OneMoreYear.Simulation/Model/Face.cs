namespace OneMoreYear.Simulation.Model;

/// <summary>
/// Facial genes, 0–1 unless noted. Children get a mix of their biological parents' genes plus a
/// little chance, so family resemblance shows – including a hidden father's. Created lazily with a
/// generator of its own (see Systems.Faces), so faces never change the simulation's random numbers.
/// </summary>
public sealed class Face
{
    /// <summary>0 = very light, 1 = very dark.</summary>
    public double Skin { get; set; }
    public double Width { get; set; }
    public double Jaw { get; set; }
    public double Chin { get; set; }
    public double Nose { get; set; }
    public double Eyes { get; set; }
    public double EyeSpacing { get; set; }
    public double Mouth { get; set; }
    public double Lips { get; set; }
    public double Brows { get; set; }
    public double Ears { get; set; }
    public double Curl { get; set; }
    public double Freckles { get; set; }
    /// <summary>How early and how much hair is lost (mostly men).</summary>
    public double Baldness { get; set; }
    /// <summary>How early the hair turns grey.</summary>
    public double Greying { get; set; }

    // Added in version 2 (older saves get them filled in once, see Faces.Of).
    /// <summary>Short and round (0) to long and narrow (1).</summary>
    public double Length { get; set; }
    /// <summary>Button (0), straight (0.5), a bump on the bridge (1).</summary>
    public double NoseShape { get; set; }
    /// <summary>Round eyes (0) to narrow ones (1).</summary>
    public double EyeShape { get; set; }
    /// <summary>Outer corners down (0) to up (1).</summary>
    public double EyeTilt { get; set; }
    /// <summary>Straight brows (0) to high arches (1).</summary>
    public double BrowArch { get; set; }
    public double Cheekbones { get; set; }
    /// <summary>Above 0.7: a mole; where it sits comes from the person.</summary>
    public double Mole { get; set; }
    /// <summary>Above 0.75: a cleft chin.</summary>
    public double Cleft { get; set; }
    public int Version { get; set; }

    // Not inherited: personal style.
    /// <summary>Which side the hair is parted on, and glasses, earrings and lipstick when the fashion allows.</summary>
    public double Style2 { get; set; }
    public int HairStyle { get; set; }
    /// <summary>0 none, 1 stubble, 2 full beard, 3 moustache (adult men).</summary>
    public int Beard { get; set; }
    /// <summary>The age from which the person wears glasses (999 = never).</summary>
    public int GlassesFromAge { get; set; } = 999;
}
