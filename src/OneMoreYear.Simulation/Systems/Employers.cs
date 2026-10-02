using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Fictional employers ("Nurse at Umeå University Hospital"). Names come from content/employers and
/// a random generator of their own, so they never change what happens in the simulation.
/// </summary>
public static class Employers
{

    /// <summary>An employer for this person and career. <paramref name="salt"/> separates several offers the same year.</summary>
    public static string? Name(SimContext ctx, Person p, string occupationId, int level, int salt = 0)
    {
        if (!ctx.Content.Employers.TryGetValue(ctx.Country.Id, out var names)
            || !(names.ByOccupation.TryGetValue($"{occupationId}:{level}", out var templates) || names.ByOccupation.TryGetValue(occupationId, out templates))
            || templates.Count == 0)
            return null;
        var rng = new SimRandom(ctx.World.Seed ^ ((ulong)p.Id * 0x9E3779B97F4A7C15UL) ^ ((ulong)ctx.Year << 20) ^ ((ulong)(salt + 1) * 0xC2B2AE3D27D4EB4FUL)
                                ^ StableHash.Of(occupationId) * 0x165667B19E3779F9UL);
        string city = HousingSystem.City(ctx, p).Name;
        string text = rng.Pick(templates);
        // Each placeholder is filled separately, so "{surname} & {surname}" gets two different names.
        while (text.Contains('{'))
        {
            int start = text.IndexOf('{');
            int end = text.IndexOf('}', start);
            if (end < 0) break;
            string value = text[(start + 1)..end] switch
            {
                "city" => city,
                "surname" => rng.Pick(ctx.Country.LastNames),
                "place" => rng.Pick(names.Places),
                "brand" => rng.Pick(names.BrandStarts) + rng.Pick(names.BrandEnds),
                _ => "",
            };
            text = text[..start] + value + text[(end + 1)..];
        }
        return text;
    }
}
