using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>
/// Heirlooms (docs/endgame.md): things that stay in the family and collect a line of history with
/// every owner. They pass on at death, to whoever they were promised to, or to the will's favourite,
/// the partner or the eldest child. They can be sold, stolen, and sometimes found again, years later.
/// </summary>
public static class HeirloomSystem
{
    public const string InheritedEvent = "heirloom_inherited", StoryEvent = "heirloom_story", AppraisalEvent = "heirloom_appraisal",
        StolenEvent = "heirloom_stolen", FoundEvent = "heirloom_found";

    public static IEnumerable<Heirloom> OwnedBy(World w, Person p) => w.Heirlooms.Where(h => h.OwnerId == p.Id);

    public static Heirloom Create(SimContext ctx, Person owner, string kind, string how)
    {
        var w = ctx.World;
        var def = ctx.Content.Heirlooms[kind];
        var h = new Heirloom
        {
            Id = w.Heirlooms.Count + 1, Kind = kind, Name = $"{Kinship.Genitive(owner.FirstName)} {def.Name}",
            OwnerId = owner.Id, SinceYear = ctx.Year,
        };
        h.History.Add(new HeirloomEntry { Year = ctx.Year, Text = $"{owner.FirstName} {how}" });
        w.Heirlooms.Add(h);
        return h;
    }

    /// <summary>The family starts with a couple of old things, kept by the oldest.</summary>
    public static void GiveStartingHeirlooms(SimContext ctx)
    {
        var w = ctx.World;
        var oldest = w.People.Where(p => p.IsAlive && p.IsBlood && p.Age(ctx.Year) >= 40).OrderBy(p => p.BirthYear).Take(2).ToList();
        var kinds = ctx.Content.Heirlooms.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        foreach (var owner in oldest)
        {
            if (kinds.Count == 0) break;
            var kind = ctx.Rng.Pick(kinds);
            kinds.Remove(kind);
            int from = ctx.Year - ctx.Rng.Range(20, 60);
            var h = Create(ctx, owner, kind, "had it from a parent");
            h.SinceYear = from;
            h.History[0] = new HeirloomEntry { Year = from, Text = $"Came to {owner.FirstName} from {(owner.Sex == Sex.Male ? "his" : "her")} parents" };
        }
    }

    /// <summary>What it would fetch now (nominal): old things are worth more.</summary>
    public static double Value(SimContext ctx, Heirloom h)
    {
        double baseValue = ctx.Content.Heirlooms.TryGetValue(h.Kind, out var def) ? def.Value : 10_000;
        return ctx.NominalRef(baseValue * (1 + Math.Max(0, ctx.Year - h.SinceYear) / 50.0));
    }

    public static string HistoryText(Heirloom h) => string.Join(" ", h.History.Select(e => $"{e.Year}: {e.Text}."));

    /// <summary>The owner died: the heirloom goes on, or is lost if nobody is left.</summary>
    public static void OnDeath(SimContext ctx, Person dead)
    {
        var w = ctx.World;
        foreach (var h in OwnedBy(w, dead).ToList())
        {
            Person? Alive(int? id) => w.TryGet(id) is { IsAlive: true } x && !dead.Disinherited.Contains(x.Id) ? x : null;
            var heir = Alive(h.PromisedToId) ?? Alive(dead.WillFavoriteId) ?? Alive(dead.PartnerId)
                       ?? Kinship.Children(w, dead).Where(c => c.IsAlive && !dead.Disinherited.Contains(c.Id)).OrderBy(c => c.BirthYear).FirstOrDefault()
                       ?? Kinship.Siblings(w, dead).Where(c => c.IsAlive).OrderBy(c => c.BirthYear).FirstOrDefault();
            h.PromisedToId = null;
            if (heir == null)
            {
                h.OwnerId = null;
                h.GoneYear = ctx.Year;
                h.Interrupted = true;
                h.History.Add(new HeirloomEntry { Year = ctx.Year, Text = $"Lost when {dead.FirstName} died with nobody to leave it to" });
                continue;
            }
            h.OwnerId = heir.Id;
            h.History.Add(new HeirloomEntry { Year = ctx.Year, Text = $"Passed from {dead.FirstName} to {heir.FirstName}" });
            if (heir.Id == w.PlayerId) Queue(ctx, InheritedEvent, h, dead.Id);
        }
    }

    /// <summary>Now and then something happens with what the player keeps, or with what the family lost.</summary>
    public static void Update(SimContext ctx)
    {
        var w = ctx.World;
        var player = w.Player;
        if (!player.IsAlive || player.Age(ctx.Year) < 18 || w.PendingEvents.Count(e => !e.Resolved) >= 3) return;
        foreach (var h in OwnedBy(w, player).OrderBy(h => h.Id).ToList())
        {
            double roll = ctx.Rng.NextDouble();
            var child = Kinship.Children(w, player).Where(c => c.IsAlive && c.Age(ctx.Year) >= 8).OrderBy(c => c.Id).ToList();
            if (roll < 0.05 && child.Count > 0) { Queue(ctx, StoryEvent, h, ctx.Rng.Pick(child).Id); return; }
            if (roll < 0.08) { Queue(ctx, AppraisalEvent, h, null); return; }
            if (roll < 0.087) { Queue(ctx, StolenEvent, h, null); return; }
        }
        // Something the family lost turns up again, years later.
        foreach (var h in w.Heirlooms.Where(h => h.OwnerId == null && h.GoneYear is { } gone && ctx.Year - gone >= 15).OrderBy(h => h.Id))
            if (ctx.Rng.Chance(0.003)) { Queue(ctx, FoundEvent, h, null); return; }
    }

    private static void Queue(SimContext ctx, string eventId, Heirloom h, int? targetId)
    {
        var roles = targetId is { } t ? new Dictionary<string, int> { ["target"] = t } : null;
        if (EventSystem.QueueSituation(ctx, eventId, roles) is not { } pending) return;
        pending.Words["heirloom"] = h.Name;
        pending.Words["heirloom_id"] = h.Id.ToString();
        pending.Words["heirloom_history"] = HistoryText(h);
        pending.Words["heirloom_value"] = EconomySystem.Format(ctx, Value(ctx, h));
        pending.Words["heirloom_price"] = EconomySystem.Format(ctx, Value(ctx, h) * 2);
        pending.Words["heirloom_text"] = ctx.Content.Heirlooms.TryGetValue(h.Kind, out var def) ? def.Text : "";
    }

    private static Heirloom? From(World w, PendingEvent pending) =>
        pending.Words.TryGetValue("heirloom_id", out var raw) && int.TryParse(raw, out var id) ? w.Heirlooms.FirstOrDefault(h => h.Id == id) : null;

    /// <summary>The "heirloom" effect: sell, promise (to the target), keep, stolen, buy_back, or make:id for a new one.</summary>
    public static string Apply(SimContext ctx, PendingEvent pending, string? kind)
    {
        var w = ctx.World;
        var player = w.Player;
        // "make:oak_table": a new heirloom, made now.
        if (kind != null && kind.StartsWith("make:") && ctx.Content.Heirlooms.ContainsKey(kind[5..]))
        {
            Create(ctx, player, kind[5..], "had it made");
            return "";
        }
        if (From(w, pending) is not { } h) return "";
        switch (kind)
        {
            case "sell":
                if (h.OwnerId != player.Id) return "";
                return Sell(ctx, player, h);
            case "promise" when w.TryGet(pending.Roles.GetValueOrDefault("target")) is { } to:
                h.PromisedToId = to.Id;
                h.History.Add(new HeirloomEntry { Year = ctx.Year, Text = $"Promised to {to.FirstName}" });
                return "";
            case "stolen":
                h.OwnerId = null;
                h.GoneYear = ctx.Year;
                h.Interrupted = true;
                h.History.Add(new HeirloomEntry { Year = ctx.Year, Text = $"Stolen from {player.FirstName}" });
                return "";
            case "buy_back":
                double price = Value(ctx, h) * 2;
                if (player.Money < price) return "You cannot afford it. You watch someone else carry it out of the room.";
                player.Money -= price;
                EconomySystem.Record(ctx, player, $"Bought back {h.Name}", -price);
                h.OwnerId = player.Id;
                h.GoneYear = null;
                h.Sold = false;
                h.History.Add(new HeirloomEntry { Year = ctx.Year, Text = $"Found and bought back by {player.FirstName}" });
                w.Feats.Add("bought_back");
                return "";
        }
        return "";
    }

    public static string Sell(SimContext ctx, Person p, Heirloom h)
    {
        double value = Value(ctx, h);
        p.Money += value;
        EconomySystem.Record(ctx, p, $"Sold {h.Name}", value);
        h.OwnerId = null;
        h.PromisedToId = null;
        h.Sold = true;
        h.GoneYear = ctx.Year;
        h.Interrupted = true;
        h.History.Add(new HeirloomEntry { Year = ctx.Year, Text = $"Sold by {p.FirstName}" });
        return $"It goes for {EconomySystem.Format(ctx, value)}.";
    }
}
