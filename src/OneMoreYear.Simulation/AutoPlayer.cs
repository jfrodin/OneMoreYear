using OneMoreYear.Simulation.Core;

namespace OneMoreYear.Simulation;

/// <summary>
/// Plays the game without a human: picks choices, sometimes performs actions and continues the
/// family after deaths. Used by tests and the balancing tool. Uses its own random generator so
/// it never disturbs the simulation's.
/// </summary>
public sealed class AutoPlayer
{
    private readonly SimRandom _rng;
    private readonly bool _useActions;

    /// <summary>Called with every text shown to the player (event texts, choices, outcomes).</summary>
    public Action<string>? OnText { get; set; }

    public AutoPlayer(ulong seed, bool useActions = true)
    {
        _rng = new SimRandom(seed);
        _useActions = useActions;
    }

    /// <summary>Plays one year: answers events, maybe takes actions, advances. Returns false when the game is over.</summary>
    public bool PlayYear(GameSession session)
    {
        if (session.GameOver) return false;
        if (session.NeedsSuccession)
        {
            var heirs = session.HeirCandidates();
            if (heirs.Count == 0) { session.EndGame(); return false; }
            session.ChooseHeir(heirs[0].Id);
        }

        AnswerEvents(session);

        if (_useActions && !session.NeedsSuccession)
        {
            for (int i = 0; i < 2 && session.World.ActionPoints > 0; i++)
            {
                var people = session.Family().Select(p => (int?)p.Id).Append(null).ToList();
                var target = people[_rng.Next(people.Count)];
                // Crimes only now and then, so simulations stay representative.
                var actions = session.Actions(target).Where(a => a.Enabled && (a.Category != "crime" || _rng.Chance(0.05))).ToList();
                if (actions.Count == 0) continue;
                var action = actions[_rng.Next(actions.Count)];
                OnText?.Invoke(action.Title);
                var result = session.PerformAction(action.Id, target);
                OnText?.Invoke(result);
                if (session.NeedsSuccession) break;
            }
        }

        // Actions can create new events (job offers, university applications).
        AnswerEvents(session);

        if (session.NeedsSuccession) return true;
        session.AdvanceYear();
        return true;
    }

    private void AnswerEvents(GameSession session)
    {
        int guard = 0;
        while (!session.NeedsSuccession && session.CurrentEvents().FirstOrDefault(e => !e.Resolved) is { } ev)
        {
            if (++guard > 50)
                throw new InvalidOperationException($"Event loop stuck on {string.Join(", ", session.World.PendingEvents.Select(p => $"{p.EventId}#{p.Uid} resolved={p.Resolved}"))}");
            OnText?.Invoke(ev.Title);
            OnText?.Invoke(ev.Text);
            foreach (var c in ev.Choices) { OnText?.Invoke(c.Text); if (c.Hint != null) OnText?.Invoke(c.Hint); }
            var options = ev.Choices.Where(c => c.Available).ToList();
            var choice = options[_rng.Next(options.Count)];
            var outcome = session.Choose(ev.Uid, choice.Index);
            OnText?.Invoke(outcome);
        }
    }
}
