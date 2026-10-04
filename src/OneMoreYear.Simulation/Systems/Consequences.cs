using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// What a choice actually did, so the player can see it: a snapshot of the player (and the people in
/// the event) before the choice, compared with after. Becomes short labels on the answered card:
/// "+$2,000", "Happiness +3", "Now brave", "Anna: closer".
/// </summary>
public sealed class Consequences
{
    private readonly SimContext _ctx;
    private readonly Person _p;
    private readonly double _money, _happy, _health, _smarts, _looks, _fitness, _grades, _performance, _fame;
    private readonly HashSet<string> _traits;
    private readonly Dictionary<string, int> _skills;
    private readonly string? _job;
    private readonly int _level;
    private readonly int? _partner;
    private readonly Dictionary<int, (double Closeness, double Trust, double Respect, double Bitterness)> _rels = new();

    private Consequences(SimContext ctx, PendingEvent pending)
    {
        _ctx = ctx;
        _p = ctx.World.Player;
        _money = _p.Money; _happy = _p.Happiness; _health = _p.Health; _smarts = _p.Smarts; _looks = _p.Looks;
        _fitness = _p.Fitness; _grades = _p.Grades; _performance = _p.Performance; _fame = _p.Fame;
        _traits = new HashSet<string>(_p.Traits);
        _skills = new Dictionary<string, int>(_p.Skills);
        _job = _p.Activity == Activity.Working ? _p.OccupationId : null;
        _level = _p.OccupationLevel;
        _partner = _p.PartnerId;
        foreach (var id in People(pending)) _rels[id] = Rel(id);
    }

    public static Consequences Before(SimContext ctx, PendingEvent pending) => new(ctx, pending);

    private IEnumerable<int> People(PendingEvent pending) =>
        pending.Roles.Values.Append(_p.PartnerId ?? 0).Where(id => id > 0 && id != _p.Id).Distinct();

    private (double, double, double, double) Rel(int id)
    {
        var r = _ctx.World.FindRel(id, _p.Id);
        return r == null ? (0, 0, 0, 0) : (r.Closeness, r.Trust, r.Respect, r.Bitterness);
    }

    /// <summary>The differences, in the order a player cares about them.</summary>
    public List<Consequence> After(PendingEvent pending)
    {
        var list = new List<Consequence>();
        void Add(string text, string tone) => list.Add(new Consequence { Text = text, Tone = tone });
        void Stat(string name, double before, double after, double min = 1)
        {
            double d = Math.Round(after - before);
            if (Math.Abs(d) >= min) Add($"{name} {(d > 0 ? "+" : "")}{d:0}", d > 0 ? "good" : "bad");
        }

        double money = _p.Money - _money;
        if (Math.Abs(money) >= 1)
            Add((money > 0 ? "+" : "-") + EconomySystem.Format(_ctx, Math.Abs(money)), money > 0 ? "good" : "bad");
        Stat("Happiness", _happy, _p.Happiness);
        Stat("Health", _health, _p.Health);
        Stat("Smarts", _smarts, _p.Smarts);
        Stat("Looks", _looks, _p.Looks);
        Stat("Fitness", _fitness, _p.Fitness);
        if (_p.Activity is Activity.School or Activity.Studying) Stat("Grades", _grades, _p.Grades);
        if (_p.Activity == Activity.Working && _job == _p.OccupationId) Stat("At work", _performance, _p.Performance, 2);
        Stat("Fame", _fame, _p.Fame);

        foreach (var t in _p.Traits.Where(t => !_traits.Contains(t)))
            if (_ctx.Content.Traits.TryGetValue(t, out var def))
                Add($"Now {def.Name.ToLowerInvariant()}", def.Tone == "dark" ? "bad" : def.Tone == "light" ? "good" : "neutral");
        foreach (var t in _traits.Where(t => !_p.Traits.Contains(t)))
            if (_ctx.Content.Traits.TryGetValue(t, out var def))
                Add($"No longer {def.Name.ToLowerInvariant()}", def.Tone == "dark" ? "good" : "neutral");
        foreach (var (skill, level) in _p.Skills)
            if (level > _skills.GetValueOrDefault(skill) && _ctx.Content.Hobbies.TryGetValue(skill, out var hobby))
                Add($"{hobby.Name} {level}", "good");

        string? job = _p.Activity == Activity.Working ? _p.OccupationId : null;
        if (job != _job && job != null) Add($"New job: {CareerSystem.Title(_ctx, _p)}", "good");
        else if (job == null && _job != null) Add("Out of work", "bad");
        else if (job != null && _p.OccupationLevel > _level) Add($"Promoted: {CareerSystem.Title(_ctx, _p)}", "good");

        if (_p.PartnerId != _partner)
        {
            if (_ctx.World.TryGet(_p.PartnerId) is { } partner) Add($"Together with {partner.FirstName}", "good");
            else Add("Single now", "neutral");
        }

        foreach (var (id, before) in _rels)
        {
            var person = _ctx.World.TryGet(id);
            if (person == null) continue;
            var now = Rel(id);
            string name = person.FirstName;
            if (now.Item1 - before.Closeness >= 2) Add($"{name}: closer", "good");
            else if (before.Closeness - now.Item1 >= 2) Add($"{name}: more distant", "bad");
            if (now.Item2 - before.Trust >= 2) Add($"{name}: trusts you more", "good");
            else if (before.Trust - now.Item2 >= 2) Add($"{name}: trusts you less", "bad");
            if (now.Item3 - before.Respect >= 2) Add($"{name}: respects you more", "good");
            if (now.Item4 - before.Bitterness >= 3) Add($"{name}: bitter", "bad");
            if (!person.IsAlive && pending.Roles.ContainsValue(id) && _rels.ContainsKey(id) && person.DeathYear == _ctx.Year) Add($"{name} died", "bad");
        }
        return list;
    }
}
