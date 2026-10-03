using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// School as more than grades: a favourite subject (chosen around nine) that trains a skill while
/// at school, so a child who loves music can arrive at adulthood already playing, and the crowd one
/// sits with at lunch (chosen around thirteen), which pulls grades up or down and decides which
/// school stories come. Both are set by events (content/events/school.json).
/// </summary>
public static class SchoolSystem
{
    /// <summary>Subjects: the skill they train, and what they do for grades.</summary>
    public static readonly (string Id, string Name, string? Skill, double Grades)[] Subjects =
    {
        ("maths", "Maths", null, 5),
        ("science", "Science", null, 4),
        ("languages", "Languages", "writing", 3),
        ("music", "Music", "music", 0),
        ("art", "Art", "painting", 0),
        ("sport", "Sport", "sport", -1),
        ("crafts", "Woodwork", "handiness", 0),
        ("computers", "Computers", "programming", 2),
    };

    /// <summary>Crowds at lunch, and their pull on grades.</summary>
    public static readonly (string Id, string Name, double Grades)[] Cliques =
    {
        ("sporty", "the sporty crowd", -1),
        ("nerds", "the ones who like school", 5),
        ("rebels", "the rebels", -7),
        ("quiet", "the quiet ones", 2),
        ("popular", "the popular crowd", -3),
    };

    /// <summary>The most a skill grows from school lessons alone; beyond that it takes a hobby.</summary>
    public const int SchoolSkillCap = 4;

    public static IEnumerable<(string Label, double Points)> GradeFactors(Person p)
    {
        if (p.Activity != Activity.School) yield break;
        if (Subjects.FirstOrDefault(s => s.Id == p.Subject) is { Id: not null, Grades: not 0 } subject)
            yield return ($"Loves {subject.Name.ToLowerInvariant()}", subject.Grades);
        if (Cliques.FirstOrDefault(c => c.Id == p.Clique) is { Id: not null, Grades: not 0 } clique)
            yield return ($"Friends: {clique.Name}", clique.Grades);
    }

    /// <summary>A year of lessons in the favourite subject: a chance to get better at it.</summary>
    public static void Update(SimContext ctx, Person p)
    {
        if (p.Activity != Activity.School) return;
        if (Subjects.FirstOrDefault(s => s.Id == p.Subject).Skill is { } skill && SkillSystem.Level(p, skill) < SchoolSkillCap && ctx.Rng.Chance(0.35))
            SkillSystem.Add(p, skill, 1);
    }

    /// <summary>"Loves music, sits with the rebels", or null before either is chosen.</summary>
    public static string? Describe(Person p)
    {
        var parts = new List<string>();
        if (Subjects.FirstOrDefault(s => s.Id == p.Subject) is { Id: not null } subject) parts.Add($"Favourite subject: {subject.Name.ToLowerInvariant()}");
        if (Cliques.FirstOrDefault(c => c.Id == p.Clique) is { Id: not null } clique) parts.Add($"sits with {clique.Name}");
        return parts.Count == 0 ? null : string.Join(", ", parts) + ".";
    }
}
