using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>Applies the effects listed in event and action content.</summary>
public static class EffectApplier
{
    public static readonly HashSet<string> KnownTypes = new()
    {
        "money", "transfer", "health", "happiness", "relation", "memory", "trait_add", "trait_remove",
        "flag", "flag_remove", "log", "job_find", "job_quit", "promote", "fire", "study", "start_dating",
        "move_in", "marry", "breakup", "child", "friend_add", "friend_remove", "will_favorite", "disinherit",
        "buy_home", "death", "start_affair", "reveal_secret", "end_affair", "grades", "attribute", "queue_event",
        "meet_through_friend", "performance", "recover", "violence", "reveal_abuse", "move_out", "move_city", "move_back_home",
        "crime", "parole"
    };

    public static Person? Resolve(SimContext ctx, string? who, PendingEvent pending)
    {
        var w = ctx.World;
        return who switch
        {
            null or "player" => w.Player,
            "partner" => w.TryGet(w.Player.PartnerId),
            _ => pending.Roles.TryGetValue(who, out var id) ? w.Get(id) : null
        };
    }

    public static void Apply(SimContext ctx, EffectDef e, PendingEvent pending)
    {
        if (e.Chance < 1 && !ctx.Rng.Chance(e.Chance)) return;
        var w = ctx.World;
        var who = Resolve(ctx, e.Who, pending);
        var to = e.To != null ? Resolve(ctx, e.To, pending) : null;
        double amount = e.Var != null && pending.Vars.TryGetValue(e.Var, out var v) ? v * (e.Amount == 0 ? 1 : e.Amount) : e.Amount;
        if (who == null) return;

        switch (e.Type)
        {
            case "money":
                who.Money += ctx.Nominal(amount);
                EconomySystem.Record(ctx, who, EventTitle(ctx, pending), ctx.Nominal(amount));
                break;
            case "transfer":
                if (to == null) return;
                who.Money -= ctx.Nominal(amount);
                to.Money += ctx.Nominal(amount);
                EconomySystem.Record(ctx, who, $"To {to.FirstName}: {EventTitle(ctx, pending)}", -ctx.Nominal(amount));
                EconomySystem.Record(ctx, to, $"From {who.FirstName}: {EventTitle(ctx, pending)}", ctx.Nominal(amount));
                break;
            case "health":
                who.Health = Math.Clamp(who.Health + amount, 1, 100);
                break;
            case "happiness":
                who.Happiness = Math.Clamp(who.Happiness + amount, 0, 100);
                break;
            case "relation":
                if (to == null || e.Dim == null) return;
                RelationshipSystem.Change(ctx, who.Id, to.Id, e.Dim.Value, amount, e.Mutual);
                RelationshipSystem.MarkContact(ctx, who.Id, to.Id);
                break;
            case "memory":
                RelationshipSystem.AddMemory(ctx, who, e.Kind ?? "event",
                    TextFormatter.Format(ctx, e.Text ?? "", pending, who), e.Impact, to?.Id);
                break;
            case "trait_add":
                if (e.Trait != null && PersonFactory.TryAddTrait(ctx, who, e.Trait) && who.Id == w.PlayerId)
                    w.Log($"{who.FirstName} became more {ctx.Content.Traits[e.Trait].Name.ToLowerInvariant()}.", 1, "trait", who.Id);
                break;
            case "trait_remove":
                if (e.Trait != null) who.Traits.Remove(e.Trait);
                break;
            case "flag":
                if (e.Flag != null) who.Flags.Add(e.Flag);
                break;
            case "flag_remove":
                if (e.Flag != null) who.Flags.Remove(e.Flag);
                break;
            case "log":
            {
                var people = new List<int> { w.PlayerId };
                people.AddRange(pending.Roles.Values);
                w.Log(TextFormatter.Format(ctx, e.Text ?? "", pending), e.Importance, "event", people.ToArray());
                break;
            }
            case "job_find":
                // The player gets offers to choose between; everyone else just finds something.
                if (who.Id == w.PlayerId) CareerSystem.QueueJobOffers(ctx, who);
                else CareerSystem.Hire(ctx, who);
                break;
            case "grades":
                who.Grades = Math.Clamp(who.Grades + amount, 0, 100);
                break;
            case "attribute":
                switch (e.Kind)
                {
                    case "smarts": who.Smarts = Math.Clamp(who.Smarts + amount, 1, 100); break;
                    case "looks": who.Looks = Math.Clamp(who.Looks + amount, 1, 100); break;
                    case "fitness": who.Fitness = Math.Clamp(who.Fitness + amount, 1, 100); break;
                }
                break;
            case "crime":
                if (e.Kind != null && ctx.Content.Crimes.TryGetValue(e.Kind, out var crime))
                    pending.ExtraText.Add(CrimeSystem.Commit(ctx, who, crime, to));
                break;
            case "parole":
                if (who.Activity == Activity.Prison && who.PrisonYearsLeft > 1) who.PrisonYearsLeft--;
                break;
            case "move_out":
                HousingSystem.MoveOut(ctx, who, share: e.Kind == "share");
                break;
            case "move_city":
                HousingSystem.MoveTo(ctx, who, e.Kind);
                break;
            case "move_back_home":
                HousingSystem.MoveBackHome(ctx, who);
                break;
            case "recover":
                DarkSystem.Recover(ctx, who);
                break;
            case "violence":
                if (to != null) DarkSystem.Hit(ctx, who, to);
                break;
            case "reveal_abuse":
            {
                var abuse = pending.Vars.TryGetValue("secret", out var sid) ? w.Secrets.FirstOrDefault(s => s.Id == (int)sid)
                    : w.Secrets.FirstOrDefault(s => s.Kind == "abuse" && s.VictimId == who.Id && !s.Revealed);
                if (abuse != null) DarkSystem.Reveal(ctx, abuse);
                break;
            }
            case "meet_through_friend":
                if (SocialSystem.MeetThroughFriend(ctx, who) is { } met) pending.Roles["other"] = met.Id;
                break;
            case "performance":
                who.Performance = Math.Clamp(who.Performance + amount, 0, 100);
                break;
            case "queue_event":
                if (e.Event != null) EventSystem.QueueSituation(ctx, e.Event);
                break;
            case "job_quit":
                if (who.Activity == Activity.Working)
                {
                    w.Log($"{who.FirstName} quit {(who.Sex == Sex.Male ? "his" : "her")} job as {CareerSystem.Article(CareerSystem.Title(ctx, who))}.", ctx.Importance(false, who), "career", who.Id);
                    CareerSystem.BecomeJobSeeker(who, ctx);
                }
                break;
            case "promote":
                CareerSystem.Promote(ctx, who);
                break;
            case "fire":
                if (who.Activity == Activity.Working)
                {
                    w.Log($"{who.FirstName} was fired.", ctx.Importance(false, who), "career", who.Id);
                    CareerSystem.BecomeJobSeeker(who, ctx);
                }
                break;
            case "study":
            {
                var prog = ctx.Content.Programme(e.Programme)
                           ?? CareerSystem.ChooseProgramme(ctx, who, e.Level ?? EducationLevel.University);
                if (prog == null || !CareerSystem.CanEnter(ctx, who, prog)) return;
                CareerSystem.StartStudies(ctx, who, prog.Id);
                w.Log(prog.Level == EducationLevel.University
                        ? $"{who.FirstName} started studying {prog.Name} at university."
                        : $"{who.FirstName} started the {prog.Name.ToLowerInvariant()}.",
                    ctx.Importance(false, who), "education", who.Id);
                break;
            }
            case "start_dating":
                if (to == null || !EventSystem.Compatible(ctx, who, to)) return;
                // Already with someone? Then this is an affair, not a new relationship.
                if (who.PartnerId is { } current && current != to.Id && who.Id == w.PlayerId)
                {
                    FamilySystem.StartAffair(ctx, who, to);
                    pending.Vars["became_affair"] = current;
                    return;
                }
                if (who.PartnerId is { } oldA) FamilySystem.BreakUp(ctx, who, w.Get(oldA));
                if (to.PartnerId is { } oldB) FamilySystem.BreakUp(ctx, to, w.Get(oldB));
                FamilySystem.StartDating(ctx, who, to);
                break;
            case "move_in":
                if (w.TryGet(who.PartnerId) is { } mp) FamilySystem.MoveIn(ctx, who, mp);
                break;
            case "marry":
                if (w.TryGet(who.PartnerId) is { } wp) FamilySystem.Marry(ctx, who, wp);
                break;
            case "breakup":
                if (w.TryGet(who.PartnerId) is { } bp) FamilySystem.BreakUp(ctx, who, bp);
                break;
            case "child":
            {
                // The baby (or adopted child) arrives next year.
                FamilySystem.Expect(ctx, who, w.TryGet(who.PartnerId));
                break;
            }
            case "friend_add":
                if (to == null || who.FriendIds.Contains(to.Id)) return;
                who.FriendIds.Add(to.Id);
                to.FriendIds.Add(who.Id);
                RelationshipSystem.Change(ctx, who.Id, to.Id, RelDim.Closeness, 45, mutual: true);
                RelationshipSystem.MarkContact(ctx, who.Id, to.Id);
                w.Log($"{who.FirstName} became friends with {to.FirstName}.", 1, "relation", who.Id, to.Id);
                break;
            case "friend_remove":
                if (to == null) return;
                who.FriendIds.Remove(to.Id);
                to.FriendIds.Remove(who.Id);
                break;
            case "will_favorite":
                who.WillFavoriteId = to?.Id;
                break;
            case "disinherit":
                if (to != null && !who.Disinherited.Contains(to.Id)) who.Disinherited.Add(to.Id);
                break;
            case "buy_home":
                if (EconomySystem.CanBuyHome(ctx, who)) EconomySystem.BuyHome(ctx, who);
                break;
            case "death":
                if (who.IsAlive) LifeSystem.Die(ctx, who, e.Cause ?? "an accident");
                break;
            case "start_affair":
                if (to != null) FamilySystem.StartAffair(ctx, who, to);
                break;
            case "reveal_secret":
            {
                var secret = SecretFrom(ctx, pending);
                if (secret is { Kind: "affair", Revealed: false })
                    SecretSystem.RevealAffair(ctx, secret, who, allowAutoBreakup: who.Id != w.PlayerId);
                break;
            }
            case "end_affair":
            {
                var secret = SecretFrom(ctx, pending)
                             ?? w.Secrets.LastOrDefault(s => s.Kind == "affair" && s.Active && s.SubjectId == who.Id);
                if (secret != null) secret.Active = false;
                break;
            }
        }
    }

    private static string EventTitle(SimContext ctx, PendingEvent pending) =>
        ctx.Content.Events.TryGetValue(pending.EventId, out var def) ? TextFormatter.Format(ctx, def.Title, pending) : "Other";

    private static Secret? SecretFrom(SimContext ctx, PendingEvent pending) =>
        pending.Vars.TryGetValue("secret", out var sid) ? ctx.World.Secrets.FirstOrDefault(s => s.Id == (int)sid) : null;
}
