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

    /// <summary>Picks a choice for some events instead of chance (for balancing: "a player who wants this").</summary>
    public Func<EventView, ChoiceView?>? Prefer { get; set; }

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

        // Now and then: a fund or a company, or selling something (so long runs exercise the market).
        if (_useActions && !session.NeedsSuccession && session.Money().CannotInvest == null && _rng.Chance(0.15))
        {
            var options = session.InvestmentOptions();
            if (options.Count > 0) session.BuyInvestment(options[_rng.Next(options.Count)].Id, session.Savings * 0.2);
        }
        else if (_useActions && !session.NeedsSuccession && session.Player.Holdings.Count > 0 && _rng.Chance(0.05))
            session.SellInvestment(session.Player.Holdings[0].AssetId, 0.5);
        // Now and then, something done to the home.
        if (_useActions && !session.NeedsSuccession && _rng.Chance(0.08) && session.HomeProjects().FirstOrDefault(h => h.DoneYear == null && h.CanAfford) is { } project)
            session.DoHomeProject(project.Id);
        // A family seat, if there is a fortune for it.
        if (_useActions && !session.NeedsSuccession && _rng.Chance(0.1) && session.Estate() is { Since: 0, CannotBuy: null })
            session.BuyEstate();
        // Rarely, a flat to let, when there is money for it.
        if (_useActions && !session.NeedsSuccession && _rng.Chance(0.04) && session.RentalOptions().FirstOrDefault(o => o.CanAfford) is { } let)
            session.BuyRental(let.TypeId);

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
            var choice = Prefer?.Invoke(ev) is { Available: true } preferred ? preferred : options[_rng.Next(options.Count)];
            var outcome = session.Choose(ev.Uid, choice.Index);
            OnText?.Invoke(outcome);
        }
    }
}
