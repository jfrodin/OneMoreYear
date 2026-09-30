using OneMoreYear.Simulation.Model;

namespace OneMoreYear.Simulation.Systems;

/// <summary>Family relationship queries and kinship labels ("grandmother", "nephew", ...).</summary>
public static class Kinship
{
    public static IEnumerable<Person> Parents(World w, Person p) => p.ParentIds.Select(w.Get);

    public static IEnumerable<Person> Children(World w, Person p) => p.ChildIds.Select(w.Get);

    public static IEnumerable<Person> Siblings(World w, Person p) =>
        p.ParentIds.SelectMany(pid => w.Get(pid).ChildIds).Where(id => id != p.Id).Distinct().Select(w.Get);

    public static bool IsHalfSibling(Person a, Person b) =>
        a.ParentIds.Count == 2 && b.ParentIds.Count == 2 && a.ParentIds.Intersect(b.ParentIds).Count() == 1;

    public static IEnumerable<Person> Grandparents(World w, Person p) => Parents(w, p).SelectMany(x => Parents(w, x));

    public static IEnumerable<Person> Grandchildren(World w, Person p) => Children(w, p).SelectMany(x => Children(w, x));

    /// <summary>
    /// Breadth-first distance over parent/child/partner links. Returns id → number of steps
    /// (the start person is excluded).
    /// </summary>
    public static Dictionary<int, int> Distances(World w, Person start, int maxDepth)
    {
        var dist = new Dictionary<int, int> { [start.Id] = 0 };
        var queue = new Queue<int>();
        queue.Enqueue(start.Id);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            int d = dist[id];
            if (d >= maxDepth) continue;
            var p = w.Get(id);
            foreach (var n in Neighbours(p))
            {
                if (dist.ContainsKey(n)) continue;
                dist[n] = d + 1;
                queue.Enqueue(n);
            }
        }
        dist.Remove(start.Id);
        return dist;
    }

    private static IEnumerable<int> Neighbours(Person p)
    {
        foreach (var id in p.ParentIds) yield return id;
        foreach (var id in p.ChildIds) yield return id;
        if (p.PartnerId is { } partner) yield return partner;
    }

    /// <summary>
    /// The people who matter to <paramref name="p"/>: family within three steps, partner, friends
    /// and ex-partners. Ordered by id for determinism.
    /// </summary>
    public static List<Person> Circle(World w, Person p, bool includeDead = false)
    {
        var ids = new SortedSet<int>(Distances(w, p, 3).Keys);
        foreach (var f in p.FriendIds) ids.Add(f);
        foreach (var e in p.ExPartnerIds) ids.Add(e);
        ids.Remove(p.Id);
        return ids.Select(w.Get).Where(x => includeDead || x.IsAlive).ToList();
    }

    public static bool MatchesRole(World w, Person player, Person other, string role, Dictionary<int, int>? distances = null)
    {
        if (other.Id == player.Id) return false;
        switch (role)
        {
            case "partner": return player.PartnerId == other.Id;
            case "parent": return player.ParentIds.Contains(other.Id);
            case "child": return player.ChildIds.Contains(other.Id);
            case "sibling": return Siblings(w, player).Any(s => s.Id == other.Id);
            case "grandparent": return Grandparents(w, player).Any(s => s.Id == other.Id);
            case "grandchild": return Grandchildren(w, player).Any(s => s.Id == other.Id);
            case "friend": return player.FriendIds.Contains(other.Id);
            case "ex": return player.ExPartnerIds.Contains(other.Id);
            case "relative":
            {
                distances ??= Distances(w, player, 3);
                return distances.ContainsKey(other.Id) && player.PartnerId != other.Id;
            }
            case "family":
            {
                distances ??= Distances(w, player, 3);
                return distances.ContainsKey(other.Id);
            }
            case "known":
            {
                distances ??= Distances(w, player, 3);
                return distances.ContainsKey(other.Id) || player.FriendIds.Contains(other.Id) || player.ExPartnerIds.Contains(other.Id);
            }
            default: return false;
        }
    }

    // --- Labels -------------------------------------------------------------------------------

    /// <summary>"your brother", "your granddaughter", "your friend" ...</summary>
    public static string Possessive(World w, Person viewer, Person other) => "your " + Label(w, viewer, other);

    /// <summary>"Anna's brother", "Eric's grandson" ...</summary>
    public static string ThirdPerson(World w, Person viewer, Person other) => $"{Genitive(viewer.FirstName)} {Label(w, viewer, other)}";

    public static string Genitive(string name) => name.EndsWith('s') ? name + "'" : name + "'s";

    /// <summary>How <paramref name="other"/> relates to <paramref name="viewer"/> ("mother", "cousin", ...).</summary>
    public static string Label(World w, Person viewer, Person other)
    {
        bool male = other.Sex == Sex.Male;
        string G(string m, string f) => male ? m : f;
        if (other.Id == viewer.Id) return "you";

        if (viewer.PartnerId == other.Id)
        {
            return viewer.PartnerStatus switch
            {
                PartnerStatus.Married => G("husband", "wife"),
                PartnerStatus.Cohabiting => "partner",
                _ => G("boyfriend", "girlfriend")
            };
        }
        if (viewer.ParentIds.Contains(other.Id)) return G("father", "mother");
        if (viewer.ChildIds.Contains(other.Id)) return G("son", "daughter");

        if (Siblings(w, viewer).Any(s => s.Id == other.Id))
            return (IsHalfSibling(viewer, other) ? "half-" : "") + G("brother", "sister");

        foreach (var parent in Parents(w, viewer))
        {
            if (parent.ParentIds.Contains(other.Id)) return G("grandfather", "grandmother");
            foreach (var gp in Parents(w, parent))
                if (gp.ParentIds.Contains(other.Id)) return G("great-grandfather", "great-grandmother");
        }

        foreach (var child in Children(w, viewer))
        {
            if (child.ChildIds.Contains(other.Id)) return G("grandson", "granddaughter");
            foreach (var gc in Children(w, child))
                if (gc.ChildIds.Contains(other.Id)) return G("great-grandson", "great-granddaughter");
            if (child.PartnerId == other.Id) return G("son-in-law", "daughter-in-law");
        }

        if (viewer.ExPartnerIds.Contains(other.Id))
        {
            bool wereMarried = other.Flags.Contains($"married_to_{viewer.Id}");
            if (!other.IsAlive && viewer.Flags.Contains("widowed") && wereMarried) return G("late husband", "late wife");
            return wereMarried ? G("ex-husband", "ex-wife") : "ex";
        }

        foreach (var parent in Parents(w, viewer))
        {
            if (parent.PartnerId == other.Id) return G("stepfather", "stepmother");
            foreach (var aunt in Siblings(w, parent))
            {
                if (aunt.Id == other.Id) return G("uncle", "aunt");
                if (aunt.ChildIds.Contains(other.Id)) return "cousin";
            }
        }

        foreach (var sib in Siblings(w, viewer))
        {
            if (sib.ChildIds.Contains(other.Id)) return G("nephew", "niece");
            if (sib.PartnerId == other.Id) return G("brother-in-law", "sister-in-law");
        }

        if (viewer.PartnerId is { } pid)
        {
            var partner = w.Get(pid);
            if (partner.ParentIds.Contains(other.Id)) return G("father-in-law", "mother-in-law");
            if (partner.ChildIds.Contains(other.Id)) return G("stepson", "stepdaughter");
            if (Siblings(w, partner).Any(s => s.Id == other.Id)) return G("brother-in-law", "sister-in-law");
        }

        if (viewer.FriendIds.Contains(other.Id)) return "friend";
        if (Distances(w, viewer, 4).ContainsKey(other.Id)) return "relative";
        return "acquaintance";
    }
}
