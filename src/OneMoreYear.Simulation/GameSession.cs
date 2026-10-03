using System.Text.Json;
using OneMoreYear.Simulation.Content;
using OneMoreYear.Simulation.Core;
using OneMoreYear.Simulation.Model;
using OneMoreYear.Simulation.Systems;

namespace OneMoreYear.Simulation;

/// <summary>
/// The public API of the simulation. A presentation layer (Godot, tests, tools) creates a session,
/// reads views from it and calls its methods to act. All game rules live behind this class.
/// </summary>
public sealed class GameSession
{
    public World World { get; }
    public ContentDb Content { get; }
    public SimContext Ctx { get; }

    private GameSession(World world, ContentDb content)
    {
        World = world;
        Content = content;
        Ctx = new SimContext(world, content);
    }

    public Person Player => World.Player;
    /// <summary>What to tell a friend: the seed code (or number) and the start year.</summary>
    public string SeedCode => World.SeedCode ?? World.Seed.ToString();
    public int Year => World.Year;
    public CountryDef Country => Ctx.Country;

    public bool NeedsSuccession => !World.GameOver && !Player.IsAlive;
    public bool GameOver => World.GameOver;
    public bool HasUnresolvedEvents => World.PendingEvents.Any(e => !e.Resolved);
    public bool CanAdvance => !NeedsSuccession && !GameOver && !HasUnresolvedEvents;

    // --- Creating and saving -----------------------------------------------------------------

    public static GameSession NewGame(NewGameOptions options, ContentDb? content = null)
    {
        content ??= ContentDb.Embedded;
        var scenario = options.ScenarioId == null ? null
            : content.Scenarios.FirstOrDefault(s => s.Id == options.ScenarioId)
              ?? throw new ArgumentException($"Unknown scenario '{options.ScenarioId}'.");
        if (scenario != null && Withdrawn(scenario)) throw new ArgumentException($"Scenario '{scenario.Id}' is withdrawn.");
        if (scenario != null)
            options = options with
            {
                Seed = options.Seed ?? (string.IsNullOrWhiteSpace(options.SeedCode) ? scenario.Seed : Core.SeedCode.ToSeed(options.SeedCode)),
                StartYear = scenario.StartYear,
            };
        string? code = options.Seed != null ? null : Core.SeedCode.Normalize(options.SeedCode ?? "") is { Length: > 0 } typed ? typed : Core.SeedCode.Random();
        ulong seed = options.Seed ?? Core.SeedCode.ToSeed(code!);

        GameSession Build(ulong s)
        {
            var world = new World
            {
                Seed = s,
                SeedCode = code,
                ContentSettings = options.ContentSettings?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new(),
                CountryId = options.CountryId,
                StartYear = options.StartYear,
                Year = options.StartYear,
                Rng = new SimRandom(s),
            };
            var session = new GameSession(world, content);
            StartingFamily.Create(session.Ctx, options);
            StartChoices.Apply(session.Ctx, options.StartConditions);
            HeirloomSystem.GiveStartingHeirlooms(session.Ctx);
            if (scenario != null) Scenarios.ApplyFamily(session.Ctx, scenario);
            session.World.ActionPoints = session.ActionPointsFor(session.Player);
            session.PickWish();
            if (scenario != null) Scenarios.FastForward(session, scenario);
            return session;
        }

        // A test scenario tries seeds in a fixed order until the family is what it promises, so it
        // survives balancing changes and always gives the same result.
        var result = Build(seed);
        for (int attempt = 1; scenario != null && attempt < 80 && !Scenarios.Meets(result, scenario); attempt++)
            result = Build(seed + (ulong)attempt * 7919UL);
        return result;
    }

    /// <summary>Changes how a dark theme is handled from now on (content settings).</summary>
    public void SetContentLevel(string category, ContentLevel level)
    {
        if (level == ContentLevel.On) World.ContentSettings.Remove(category);
        else World.ContentSettings[category] = level;
        // Anything already waiting about that theme goes away.
        World.PendingEvents.RemoveAll(e => !e.Resolved && Content.Events.TryGetValue(e.EventId, out var d) && !EventSystem.Allowed(Ctx, d));
    }

    public ContentLevel ContentLevelOf(string category) => Ctx.Level(category);

    /// <summary>The test scenarios that can be started from the title screen.</summary>
    public static IReadOnlyList<ScenarioDef> AvailableScenarios(ContentDb? content = null) =>
        (content ?? ContentDb.Embedded).Scenarios.Where(s => !Withdrawn(s)).ToList();

    // "The family secret" is built on abuse of a child, a theme withdrawn for now.
    private static bool Withdrawn(ScenarioDef s) =>
        s.Storyline == "hidden_father" && ContentCategories.Withdrawn.Contains(ContentCategories.SexualAbuse);

    public string Save() => JsonSerializer.Serialize(World, ContentDb.JsonOptions);

    public static GameSession Load(string json, ContentDb? content = null)
    {
        var world = JsonSerializer.Deserialize<World>(json, ContentDb.JsonOptions)
                    ?? throw new InvalidDataException("Tom sparfil.");
        if (world.SaveVersion > World.CurrentSaveVersion)
            throw new InvalidDataException("The save file comes from a newer version of the game.");
        var session = new GameSession(world, content ?? ContentDb.Embedded);
        // Older save versions are migrated here, one version step at a time.
        if (world.SaveVersion < 2)
        {
            // 0.12: homes got a market value and a mortgage (they used to count as half the price).
            foreach (var p in world.People.Where(p => p.IsAlive && p.OwnsHome && p.HomeValue <= 0))
            {
                if (world.TryGet(p.PartnerId) is { HomeValue: > 0 }) continue;
                double price = HousingSystem.HomePrice(session.Ctx, p);
                EconomySystem.GiveHome(p, price, price * 0.5);
            }
        }
        // 0.29: the player's simple funds and shares become real holdings.
        if (world.Player.IsAlive) InvestmentSystem.ConvertSimple(session.Ctx, world.Player);
        world.SaveVersion = World.CurrentSaveVersion;
        return session;
    }

    // --- The yearly loop ---------------------------------------------------------------------

    public YearReport AdvanceYear()
    {
        if (!CanAdvance) throw new InvalidOperationException("Cannot continue: there are unanswered events or no living player.");
        var w = World;
        var ctx = Ctx;
        int logStart = w.Chronicle.Count;

        WishSystem.EndOfYear(ctx);
        w.Year++;
        w.PendingEvents.Clear();
        w.ActionsThisYear.Clear();
        w.Ledger.RemoveAll(l => l.Year < w.Year - 1);

        double jobLoss = 0;
        foreach (var h in Country.HistoricalEvents.Where(h => h.Year == w.Year))
        {
            w.Log(h.Text, 3, "world"); // history is front-page news
            jobLoss += h.JobLossChance;
        }

        // The markets move for everyone; big moves make the news.
        var market = Market.For(ctx, w.Year);
        if (Math.Abs(market.Stocks - market.Inflation) >= 0.18)
            w.Log(market.Stocks > market.Inflation ? $"A boom year: the stock market rose {Math.Round(market.Stocks * 100)} percent." : $"The stock market fell {Math.Round(-market.Stocks * 100)} percent.", 2, "world");
        int count = w.People.Count;
        for (int i = 0; i < count; i++)
        {
            var p = w.People[i];
            if (!p.IsAlive) continue;
            LifeSystem.UpdateHealth(ctx, p);
            if (LifeSystem.CheckDeath(ctx, p)) continue;
            UpbringingSystem.Update(ctx, p);
            SkillSystem.Update(ctx, p);
            SchoolSystem.Update(ctx, p);
            if (p.Age(ctx.Year) == ctx.Country.AdultAge)
            {
                PersonFactory.RollAdultTraits(ctx, p);
                UpbringingSystem.BecomeAdult(ctx, p);
            }
            CareerSystem.Update(ctx, p, jobLoss);
            FameSystem.Update(ctx, p);
            HousingSystem.Update(ctx, p);
            HomeProjectSystem.Update(ctx, p);
            EconomySystem.Update(ctx, p, market);
            p.PeakNetWorth = Math.Max(p.PeakNetWorth, EconomySystem.NetWorth(ctx, p));
            double refWorth = ctx.Real(EconomySystem.NetWorth(ctx, p)) / ctx.Country.ContentMoneyScale;
            p.PeakRefWorth = Math.Max(p.PeakRefWorth, refWorth);
            p.LowRefWorth = Math.Min(p.LowRefWorth, refWorth);
            if (p.InFamily) LifeSystem.UpdateWill(ctx, p);
        }

        FamilySystem.Update(ctx);
        SecretSystem.Update(ctx);
        DarkSystem.Update(ctx);
        AilmentSystem.Update(ctx);
        Hardship.Update(ctx);
        CrimeSystem.Update(ctx);
        EmigrationSystem.Update(ctx);
        BusinessSystem.Update(ctx);
        RentalSystem.Update(ctx);
        RelationshipSystem.UpdateYear(ctx);
        SocialSystem.Update(ctx);

        if (Player.IsAlive)
        {
            DreamSystem.Update(ctx);
            LegacySystem.Update(ctx);
            ReputationSystem.Update(ctx);
            HeirloomSystem.Update(ctx);
            PetSystem.Update(ctx);
            EventSystem.GenerateRandomEvents(ctx);
            w.ActionPoints = ActionPointsFor(Player);
            PickWish();
        }
        else
        {
            w.PendingEvents.Clear();
        }

        var news = RelevantLines(w.Chronicle.Skip(logStart));
        return new YearReport(w.Year, Player.Age(w.Year), news, !Player.IsAlive);
    }

    private int ActionPointsFor(Person p) => p.Age(World.Year) switch { < 4 => 0, < 13 => 2, _ => 3 };

    private List<ChronicleLine> RelevantLines(IEnumerable<LogEntry> entries)
    {
        var circle = Kinship.Circle(World, Player, includeDead: true).Select(p => p.Id).ToHashSet();
        circle.Add(Player.Id);
        return entries
            .Where(e => e.Category == "world" || e.PersonIds.Any(circle.Contains))
            .Select(ToLine)
            .ToList();
    }

    /// <summary>
    /// The family news as stories, one per person, the biggest first: what happened to the same
    /// person is told together and in order, with "she" or "he" after the first sentence.
    /// </summary>
    public IReadOnlyList<NewsStory> Stories(IReadOnlyList<ChronicleLine> news)
    {
        var family = news.Where(l => l.Category != "world").ToList();
        var groups = new List<(int Person, List<ChronicleLine> Lines)>();
        foreach (var line in family)
        {
            int person = line.PersonIds.FirstOrDefault();
            var group = person > 0 ? groups.FirstOrDefault(g => g.Person == person) : default;
            if (group.Lines == null) groups.Add((person, new List<ChronicleLine> { line }));
            else group.Lines.Add(line);
        }
        static int Rank(ChronicleLine l) => (YearReport.IsFrontPage(l) ? 100 : 0) + l.Importance;
        return groups.Select(g =>
        {
            var top = g.Lines.OrderByDescending(Rank).First();
            var sentences = g.Lines.Select((l, i) => i == 0 ? l.Text : Pronoun(g.Person, l.Text)).ToList();
            var rest = g.Lines.Where(l => l != top).Select(l => Pronoun(g.Person, l.Text));
            return new NewsStory(g.Person, top.Text, string.Join(" ", rest), string.Join(" ", sentences),
                g.Lines.Max(Rank), g.Lines.Any(YearReport.IsFrontPage));
        }).OrderByDescending(s => s.Importance).ToList();
    }

    /// <summary>"Anna (your sister) got a job." told again about Anna: "She got a job."</summary>
    private string Pronoun(int personId, string text)
    {
        if (World.TryGet(personId) is not { } p || p.Id == Player.Id) return text;
        var match = System.Text.RegularExpressions.Regex.Match(text,
            $@"^(?:{System.Text.RegularExpressions.Regex.Escape(p.FullName)}|{System.Text.RegularExpressions.Regex.Escape(p.FirstName)})(?: \([^)]*\))?(?=[ ,])");
        if (!match.Success || text[match.Length..].StartsWith("'")) return text;
        return (p.Sex == Sex.Male ? "He" : "She") + text[match.Length..];
    }

    private ChronicleLine ToLine(LogEntry e) => new(e.Year, Annotate(e.Text, e.PersonIds), e.Importance, e.Category, e.PersonIds);

    /// <summary>
    /// Adds how each mentioned person relates to the current player: "Anna got married" becomes
    /// "Anna (your sister) got married". Computed when shown, so it stays right after a generation change.
    /// </summary>
    public string Annotate(string text, IEnumerable<int> personIds)
    {
        var player = Player;
        // All insertions are found in the original text and applied afterwards, so an added
        // "(Oskar's ex)" can never itself be annotated.
        var inserts = new List<(int Index, string Text)>();
        foreach (var id in personIds.Distinct())
        {
            if (id == player.Id || World.TryGet(id) is not { } p) continue;
            string label = Kinship.Label(World, player, p);
            if (label is "acquaintance" or "you") continue;
            string relation = Kinship.WithYour(label);
            if (text.Contains(relation, StringComparison.OrdinalIgnoreCase)) continue;
            // "Agneta (Oskar's ex)" adds nothing when Oskar is already in the sentence.
            if (Kinship.IsOwnerLabel(label) && text.Contains(label.Split('\'')[0])) continue;
            // The full name now, the name at birth (before a marriage), then the first name alone.
            foreach (var name in new[] { p.FullName, $"{p.FirstName} {p.BirthLastName}", p.FirstName })
            {
                // Whole word, and not already followed by a parenthesis.
                var match = System.Text.RegularExpressions.Regex.Match(text, $@"\b{System.Text.RegularExpressions.Regex.Escape(name)}\b(?!\s*\()(?!')");
                if (!match.Success) continue;
                int at = match.Index + match.Length;
                if (inserts.All(i => i.Index != at)) inserts.Add((at, $" ({relation})"));
                break;
            }
        }
        foreach (var (index, insert) in inserts.OrderByDescending(i => i.Index))
            text = text.Insert(index, insert);
        return text;
    }

    // --- Events ------------------------------------------------------------------------------

    public IReadOnlyList<EventView> CurrentEvents() => World.PendingEvents.Select(DescribeEvent).ToList();

    public EventView DescribeEvent(PendingEvent pending)
    {
        var def = Content.Events[pending.EventId];
        var choices = new List<ChoiceView>();

        // Generated choices first (job offers), then the event's own.
        for (int i = 0; i < pending.Options.Count; i++)
        {
            if (def.DynamicChoices == "dreams")
            {
                var (dreamName, dreamText) = DreamSystem.Describe(Ctx, pending.Options[i]);
                choices.Add(new ChoiceView(i, dreamName, dreamText, null, true));
                continue;
            }
            if (def.DynamicChoices == "baby_names")
            {
                choices.Add(new ChoiceView(i, pending.Options[i], i == 0 ? "The name you had in mind." : null, null, true));
                continue;
            }
            if (def.DynamicChoices == "cities" && Country.Cities.FirstOrDefault(c => c.Id == pending.Options[i]) is { } city)
            {
                string size = city.Size switch { "village" => "A small village", "town" => "A town", _ => "A big city" };
                choices.Add(new ChoiceView(i, city.Name, $"{size}. Homes cost about {EconomySystem.Format(Ctx, Ctx.Nominal(Country.HomePrice) * city.PriceFactor)}.", null, true));
                continue;
            }
            if (CareerSystem.ParseOffer(Ctx, pending.Options[i]) is not { } offer) continue;
            var (occ, level, employer) = offer;
            var lvl = occ.Levels[level];
            string fit = CareerSystem.FitsDegree(Ctx, Player, occ) ? "Uses your education." : "Doesn't use your education.";
            choices.Add(new ChoiceView(i, $"{lvl.Title}{(employer != null ? $" at {employer}" : $"  ·  {occ.Name}")}  ·  {EconomySystem.FormatPay(Ctx, CareerSystem.Salary(Ctx, lvl))}",
                $"{occ.Name}. {fit} Top of this career: {occ.Levels[^1].Title}.", null, true));
        }
        int offset = pending.Options.Count;
        for (int i = 0; i < def.Choices.Count; i++)
        {
            var c = def.Choices[i];
            // A programme this country does not have is not a choice at all.
            if (EventSystem.StudyProgramme(Ctx, c) is { Countries.Count: > 0 } foreign && !foreign.Countries.Contains(Country.Id)) continue;
            // Trait choices only exist for people with the trait.
            if (c.Trait != null && !Player.HasTrait(c.Trait)) continue;
            string? tag = c.Trait != null && Content.Traits.TryGetValue(c.Trait, out var traitDef) ? traitDef.Name : null;
            string? hint = c.Hint == null ? null : TextFormatter.Format(Ctx, c.Hint, pending);
            if (EventSystem.StudyProgramme(Ctx, c) is { } prog)
            {
                // What it is, and if it is out of reach, exactly why.
                hint ??= prog.Description + (CareerSystem.WhyNot(Ctx, Player, prog) is { } why ? " " + why : "");
            }
            if (EventSystem.StartsRomance(c) && World.TryGet(Player.PartnerId) is { } partner)
                hint = $"You're with {partner.FirstName}, so this would be an affair." + (hint == null ? "" : " " + hint);
            choices.Add(new ChoiceView(offset + i, TextFormatter.Format(Ctx, c.Text, pending), hint,
                c.Chance != null ? (int)Math.Round(EventSystem.SuccessChance(Ctx, c, pending) * 100) : null,
                EventSystem.IsChoiceAvailable(Ctx, c, pending), tag, EventSystem.ChanceFactors(Ctx, c, pending)));
        }
        var involved = pending.Roles.Values.ToList();
        return new EventView(pending.Uid, TextFormatter.Format(Ctx, def.Title, pending),
            Annotate(TextFormatter.Format(Ctx, def.Text, pending), involved),
            choices, pending.Resolved, pending.OutcomeText == null ? null : Annotate(pending.OutcomeText, involved),
            pending.Roles.TryGetValue("target", out var t) ? t : null,
            EventSystem.Insights(Ctx, def, pending));
    }

    public string Choose(int eventUid, int choiceIndex)
    {
        var pending = World.PendingEvents.First(e => e.Uid == eventUid);
        if (pending.Resolved) throw new InvalidOperationException("The event has already been answered.");
        var def = Content.Events[pending.EventId];
        int own = choiceIndex - pending.Options.Count;
        if (own >= 0 && !EventSystem.IsChoiceAvailable(Ctx, def.Choices[own], pending))
            throw new InvalidOperationException("That choice is not available.");
        return EventSystem.Resolve(Ctx, pending, choiceIndex);
    }

    // --- Actions -----------------------------------------------------------------------------

    /// <summary>Actions the player can take towards a person, or on their own life if <paramref name="targetId"/> is null.</summary>
    public IReadOnlyList<ActionView> Actions(int? targetId)
    {
        var player = Player;
        var result = new List<ActionView>();
        if (!player.IsAlive) return result;
        var target = World.TryGet(targetId);
        if (target is { IsAlive: false }) return result;
        var distances = Kinship.Distances(World, player, 3);

        foreach (var def in Content.Events.Values.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            if (!Possible(def, player, target, distances)) continue;
            var choice = def.Choices[0];
            var probe = new PendingEvent { EventId = def.Id };
            if (target != null) probe.Roles["target"] = target.Id;
            if (target?.Addiction != null) probe.Words["habit"] = DarkSystem.What(target.Addiction);
            int? chance = choice.Chance != null ? (int)Math.Round(EventSystem.SuccessChance(Ctx, choice, probe) * 100) : null;
            bool used = World.ActionsThisYear.Contains(ActionKey(def.Id, targetId));
            string? hint = choice.Hint == null ? null : TextFormatter.Format(Ctx, choice.Hint, probe);
            result.Add(new ActionView(def.Id, TextFormatter.Format(Ctx, def.Title, probe), hint, chance,
                !used && World.ActionPoints > 0 && CanAdvanceOrActionsAllowed(), def.Category));
        }
        return result;
    }

    /// <summary>Can the player take this action, towards the target or alone (ignoring action points)?</summary>
    private bool Possible(EventDef def, Person player, Person? target, Dictionary<int, int> distances)
    {
        bool isSelf = def.Trigger == "self";
        if (def.Trigger != "action" && !isSelf || !EventSystem.Allowed(Ctx, def)) return false;
        // In prison, only prison life is possible.
        if ((player.Activity == Activity.Prison) != (def.Category == "prison")) return false;
        if (isSelf != (target == null)) return false;
        if (!EventSystem.Matches(Ctx, def.Conditions, player, player)) return false;
        if (target != null)
        {
            if (def.Target == null || !Kinship.MatchesRole(World, player, target, def.Target.Role, distances)) return false;
            if (!EventSystem.Matches(Ctx, def.Target.Conditions, target, player)) return false;
        }
        return EventSystem.Matches(Ctx, def.Choices[0].Requires, player, player);
    }

    private bool CanAdvanceOrActionsAllowed() => !NeedsSuccession && !GameOver;

    private static string ActionKey(string id, int? target) => $"{id}:{target?.ToString() ?? "self"}";

    /// <summary>The player's wish for this year (null if none), and whether it has come true.</summary>
    public (string Text, bool Kept)? Wish() => WishSystem.Describe(Ctx) is { } text ? (text, WishSystem.Kept(Ctx)) : null;

    private void PickWish()
    {
        var distances = Kinship.Distances(World, Player, 3);
        WishSystem.Pick(Ctx, (id, target) => Content.Events.TryGetValue(id, out var def) && Possible(def, Player, World.TryGet(target), distances));
    }

    public string PerformAction(string actionId, int? targetId)
    {
        var action = Actions(targetId).FirstOrDefault(a => a.Id == actionId);
        if (action is not { Enabled: true }) throw new InvalidOperationException("That action is not available.");
        var def = Content.Events[actionId];
        var pending = EventSystem.CreatePending(Ctx, def, Player, Kinship.Distances(World, Player, 3), targetId)
                      ?? throw new InvalidOperationException("The action could not be performed.");
        World.ActionPoints--;
        World.ActionsThisYear.Add(ActionKey(actionId, targetId));
        string intro = string.IsNullOrWhiteSpace(def.Text) ? "" : TextFormatter.Format(Ctx, def.Text, pending) + " ";
        return (intro + EventSystem.Resolve(Ctx, pending, 0)).Trim();
    }

    // --- People ------------------------------------------------------------------------------

    /// <summary>The player's family and friends, closest first.</summary>
    public IReadOnlyList<PersonView> Family(bool includeDead = false)
    {
        var dist = Kinship.Distances(World, Player, 3);
        return Kinship.Circle(World, Player, includeDead)
            .OrderBy(p => SortKey(p, dist))
            .ThenBy(p => p.BirthYear)
            .Select(p => Describe(p.Id))
            .ToList();
    }

    private int SortKey(Person p, Dictionary<int, int> dist)
    {
        var pl = Player;
        if (pl.PartnerId == p.Id) return 0;
        if (pl.ChildIds.Contains(p.Id)) return 1;
        if (pl.ParentIds.Contains(p.Id)) return 2;
        if (Kinship.Siblings(World, pl).Any(s => s.Id == p.Id)) return 3;
        if (pl.FriendIds.Contains(p.Id)) return 6;
        if (pl.ExPartnerIds.Contains(p.Id)) return 7;
        if (SocialSystem.Find(pl, p.Id) is { } a) return a.Current ? 8 : 9;
        return 3 + dist.GetValueOrDefault(p.Id, 5);
    }

    /// <summary>What someone looks like right now (or when they died).</summary>
    public PortraitView Portrait(int id) => PortraitAt(World.Get(id), World.Get(id).Age(Year), World.Get(id).IsAlive, (World.Get(id).Happiness - 50) / 35);

    /// <summary>
    /// A life in photographs (The Sims' memories, docs/sims-inspiration.md): the birth, the happiest
    /// memories, the children's births and a last photograph, each with the face of that age.
    /// </summary>
    public IReadOnlyList<AlbumPhoto> Album(int id)
    {
        var p = World.Get(id);
        int last = p.DeathYear ?? Year;
        var photos = new List<(int Year, string Caption, double Weight)> { (p.BirthYear, "Born", 1000) };
        foreach (var m in p.Memories.Where(m => m.Impact >= 12 && m.Year > p.BirthYear && m.Year <= last))
            photos.Add((m.Year, TextFormatter.Capitalize(m.Text), m.Impact));
        foreach (var child in Kinship.Children(World, p).Where(c => c.BirthYear <= last))
            if (!photos.Any(x => x.Year == child.BirthYear && x.Caption.Contains(child.FirstName)))
                photos.Add((child.BirthYear, $"The year {child.FirstName} was born", 40));
        if (p.DeathYear is { } died && died - p.BirthYear >= 2) photos.Add((died - 1, "The last photograph", 999));
        var chosen = photos.GroupBy(x => (x.Year, x.Caption)).Select(g => g.First())
            .OrderByDescending(x => x.Weight).Take(30).OrderBy(x => x.Year).ThenByDescending(x => x.Weight).ToList();
        return chosen.Select(x =>
        {
            int age = Math.Max(0, x.Year - p.BirthYear);
            // Most people smile for the camera.
            return new AlbumPhoto(x.Year, age, x.Caption, PortraitAt(p, age, true, x.Caption == "The last photograph" ? 0.2 : 0.6));
        }).ToList();
    }

    /// <summary>What a person wears in a picture at this age: work clothes while working, else everyday clothes.</summary>
    private string Outfit(Person p, int age)
    {
        if (age < 2) return "baby";
        bool now = p.Age(p.DeathYear ?? Year) == age;
        if (!now) return age >= 66 ? "cardigan" : "casual";
        if (p.Activity == Activity.Retired || age >= 70) return "cardigan";
        if (p.Activity != Activity.Working) return "casual";
        return p.OccupationId switch
        {
            "medicine" or "healthcare" => "scrubs",
            "police" or "military" or "aviation" => "uniform",
            "law" or "finance" or "politics" or "business" or "real_estate" => "suit",
            "construction" or "industry" or "agriculture" or "transport" or "cleaning" => "work",
            "public" or "academia" or "engineering" or "science" or "media" or "it" or "education" => "smart",
            _ => "casual",
        };
    }

    /// <summary>What someone looked like at an age (for the album: alive in the photo, mood as given).</summary>
    private PortraitView PortraitAt(Person p, int age, bool alive, double mood)
    {
        var face = Faces.Of(World, p, Content);
        double bmi = Appearance.WeightAt(p, age) / Math.Pow(Math.Max(0.5, Appearance.HeightAt(p, age) / 100.0), 2);
        double normal = age < 12 ? 16 : age < 18 ? 19 : 22.5;
        return new PortraitView
        {
            Id = p.Id,
            Male = p.Sex == Sex.Male,
            Age = age,
            Alive = alive,
            HairColor = p.HairColor,
            EyeColor = p.EyeColor,
            Grey = Faces.GreyAt(face, age),
            Bald = Faces.BaldAt(face, p.Sex, age),
            Heaviness = Math.Clamp(0.5 + (bmi - normal) / 16, 0, 1),
            Mood = Math.Clamp(mood, -1, 1),
            Glasses = age >= face.GlassesFromAge,
            HeightCm = Appearance.HeightAt(p, age),
            Fitness = p.Fitness,
            Face = face,
            Year = p.BirthYear + age,
            Outfit = Outfit(p, age),
        };
    }

    public PersonView Describe(int id)
    {
        var p = World.Get(id);
        var player = Player;
        int age = p.Age(Year);
        var partner = World.TryGet(p.PartnerId);
        string partnerText = partner == null || !p.IsAlive ? "" : p.PartnerStatus switch
        {
            PartnerStatus.Married => $"Married to {partner.FullName}",
            PartnerStatus.Cohabiting => $"Living with {partner.FullName}",
            _ => $"Dating {partner.FullName}"
        };
        if (p.IsAlive && partner == null) partnerText = SingleText(p);

        // Close family as links, so you can jump between them.
        var links = new List<PersonLink>();
        void Link(Person? x) { if (x != null && links.All(l => l.Id != x.Id)) links.Add(new PersonLink(x.Id, TextFormatter.Capitalize(Kinship.Label(World, p, x)), x.FullName, x.IsAlive)); }
        Link(partner);
        foreach (var x in Kinship.Parents(World, p)) Link(x);
        foreach (var x in Kinship.Siblings(World, p).OrderBy(x => x.BirthYear)) Link(x);
        foreach (var x in Kinship.Children(World, p).OrderBy(x => x.BirthYear)) Link(x);
        foreach (var x in p.ExPartnerIds.Select(World.Get).TakeLast(3)) Link(x);

        var memories = p.Memories
            .Where(m => p.Id == player.Id || m.AboutId == player.Id || Math.Abs(m.Impact) >= 30)
            .OrderByDescending(m => m.Year)
            .Take(12)
            .Select(m => new MemoryView(m.Year, Annotate(m.Text, new[] { m.AboutId, m.MentionId }.OfType<int>()), m.Impact * m.Strength, World.TryGet(m.AboutId)?.FirstName))
            .ToList();

        return new PersonView
        {
            Id = p.Id,
            Name = p.FullName,
            FirstName = p.FirstName,
            IsMale = p.Sex == Sex.Male,
            Age = age,
            Alive = p.IsAlive,
            BirthYear = p.BirthYear,
            DeathYear = p.DeathYear,
            CauseOfDeath = p.CauseOfDeath,
            RoleLabel = p.Id == player.Id ? "You" : TextFormatter.Capitalize(Kinship.Label(World, player, p)),
            Occupation = p.IsAlive ? CareerSystem.ActivityText(Ctx, p) : $"Died {p.DeathYear}",
            Education = p.Education switch
            {
                EducationLevel.University => "University",
                EducationLevel.Secondary => TextFormatter.Capitalize(Country.SecondarySchool),
                EducationLevel.Primary => "Primary school",
                _ => "None"
            },
            Partner = partnerText,
            Hobby = SkillSystem.Describe(Ctx, p),
            Fame = FameSystem.Label(Ctx, p),
            Pets = p.Id == Player.Id ? PetSystem.InHome(World, p).Select(x => PetSystem.Describe(Ctx, x)).ToList() : Array.Empty<string>(),
            Dream = DreamSystem.Of(Ctx, p) is { } dream ? p.DreamState switch
            {
                DreamState.Fulfilled => $"Lived the dream: {dream.Name.ToLowerInvariant()}",
                DreamState.Failed => $"A dream that never came true: {dream.Name.ToLowerInvariant()}",
                _ => $"Dream: {dream.Name.ToLowerInvariant()}" + (p.DreamFromId is { } from ? $", for {World.Get(from).FirstName}" : ""),
            } : null,
            Traits = p.Traits.Where(Content.Traits.ContainsKey)
                .Select(t => (Content.Traits[t].Name, Content.Traits[t].Description, Content.Traits[t].Tone)).ToList(),
            Condition = !p.IsAlive ? null
                : string.Join("  ·  ", new[] { p.Addiction != null ? $"Struggling with {DarkSystem.What(p.Addiction)}" : null, AilmentSystem.Describe(Ctx, p) }
                    .Where(x => x != null)) is { Length: > 0 } c ? c : null,
            Health = p.Health,
            HealthLabel = p.Health switch { >= 80 => "Excellent", >= 60 => "Good", >= 40 => "Fair", >= 20 => "Poor", _ => "Critical" },
            Happiness = p.Happiness,
            Money = EconomySystem.Format(Ctx, p.Money),
            Income = p.Income > 0 ? EconomySystem.FormatPay(Ctx, p.Income) : "–",
            OwnsHome = p.OwnsHome,
            TowardsPlayer = p.Id == player.Id ? null : RelView(World.FindRel(p.Id, player.Id)),
            FromPlayer = p.Id == player.Id ? null : RelView(World.FindRel(player.Id, p.Id)),
            Memories = memories,
            Generation = p.Generation,
            Smarts = p.Smarts,
            Looks = p.Looks,
            Fitness = p.Fitness,
            Grades = p.Grades,
            AppearanceText = Appearance.Describe(p, Year),
            Home = HousingSystem.Describe(Ctx, p),
            Links = links,
        };
    }

    private static RelationView RelView(Relationship? r)
    {
        r ??= new Relationship();
        return new RelationView(r.Closeness, r.Respect, r.Trust, r.Attraction, r.Fear, r.Envy, r.Bitterness, r.Opinion,
            RelationshipSystem.OpinionLabel(r.Opinion));
    }

    // --- School & work, money ---------------------------------------------------------------

    /// <summary>How hard the player works or studies: -1, 0 or 1. Free; it shows next year.</summary>
    public void SetEffort(int effort) => Player.Effort = Math.Clamp(effort, -1, 1);

    /// <summary>"Heading towards about 68: working hard +12, ambition +15, smarts +3. Luck moves it too."</summary>
    private static string FactorNote(IReadOnlyList<(string Label, double Points)> factors, string? tail)
    {
        double target = Math.Clamp(50 + factors.Sum(f => f.Points), 0, 100);
        string parts = factors.Count == 0 ? "nothing special pulls it either way"
            : string.Join(", ", factors.Select(f => $"{f.Label.ToLowerInvariant()} {(f.Points >= 0 ? "+" : "")}{Math.Round(f.Points)}"));
        return $"Heading towards about {target:0}: {parts}. Luck moves it too, a little every year." + (tail == null ? "" : " " + tail);
    }

    public CareerView Career()
    {
        var p = Player;
        var prog = Content.Programme(p.ProgrammeId);
        var occ = Content.Occupation(p.OccupationId);
        var ladder = occ == null ? new List<LadderStep>() : occ.Levels.Select((l, i) =>
        {
            var req = new List<string>();
            if (l.MinEducation > EducationLevel.None) req.Add(CareerSystem.EducationName(Ctx, l.MinEducation));
            if (l.RequiresDegree is { Count: > 0 } degrees)
                req.Add(string.Join(" or ", degrees.Select(d => Content.Programme(d)?.NameIn(Country.Id) ?? d)));
            return new LadderStep(l.Title, EconomySystem.FormatPay(Ctx, CareerSystem.Salary(Ctx, l)),
                req.Count == 0 ? "No requirements" : "Needs " + string.Join(", ", req),
                i == p.OccupationLevel, CareerSystem.QualifiesFor(p, l));
        }).ToList();

        string? promotionNote = null;
        if (occ != null && p.OccupationLevel + 1 < occ.Levels.Count)
        {
            var next = occ.Levels[p.OccupationLevel + 1];
            string? skillName = next.Skill != null ? Content.Hobbies.GetValueOrDefault(next.Skill)?.Name.ToLowerInvariant() : null;
            promotionNote = next.Skill != null && SkillSystem.Level(p, next.Skill) < next.MinSkill
                    ? $"To become {CareerSystem.Article(next.Title)} you need {skillName} {next.MinSkill}. Yours is {SkillSystem.Level(p, next.Skill)}; every year at work is practice."
                : !CareerSystem.QualifiesFor(p, next) ? $"To become {CareerSystem.Article(next.Title)} you need more education."
                : p.YearsInJob < 2 ? "Promotions come after at least two years in the role."
                : "Your chance depends on your performance.";
        }
        else if (occ != null) promotionNote = "You are at the top of this career.";

        return new CareerView
        {
            Status = CareerSystem.ActivityText(Ctx, p),
            EducationLevel = p.Education switch
            {
                EducationLevel.University => "University degree",
                EducationLevel.Secondary => TextFormatter.Capitalize(Country.SecondarySchool),
                EducationLevel.Primary => "Primary school",
                _ => p.Age(Year) < 7 ? "Not in school yet" : "In school"
            },
            Programme = prog?.Name,
            ProgrammeDescription = prog?.Description,
            YearsLeft = p.Activity == Activity.Studying ? p.StudyYearsLeft : 0,
            Grades = p.Age(Year) >= 7 ? p.Grades : null,
            PartTimeJob = p.Flags.Contains(CareerSystem.PartTimeFlag),
            Degrees = p.Degrees.Select(d => Content.Programme(d)?.NameIn(Country.Id) ?? d).ToList(),
            JobTitle = occ == null ? null : CareerSystem.Title(Ctx, p),
            Employer = occ == null || occ.Id == "crime" ? null : p.Employer,
            Field = occ?.Name,
            Salary = occ == null ? null : EconomySystem.FormatPay(Ctx, p.Income),
            YearsInJob = p.YearsInJob,
            Performance = occ == null ? null : p.Performance,
            PromotionChancePercent = (int)Math.Round(CareerSystem.PromotionChance(Ctx, p) * 100),
            PromotionNote = promotionNote,
            Ladder = ladder,
            CriminalRecord = p.CriminalRecord.Select(r => $"{r.Year}: {Content.Crimes.GetValueOrDefault(r.CrimeId)?.Name ?? r.CrimeId}, {r.Sentence}").ToList(),
            Effort = p.Effort,
            GradesFromAge = Country.GradesFromAge,
            CanChooseEffort = p.Activity is Activity.Working or Activity.Studying || p.Activity == Activity.School && p.Age(Year) >= 10,
            PerformanceNote = p.Activity == Activity.Working ? FactorNote(CareerSystem.PerformanceFactors(Ctx, p), "Over 50 helps a promotion; under 30 you risk losing the job.") : null,
            GradesNote = p.Activity is Activity.School or Activity.Studying ? FactorNote(CareerSystem.GradeFactors(Ctx, p), null) : null,
            SchoolNote = p.Activity == Activity.School ? SchoolSystem.Describe(p) : null,
        };
    }

    private string? HomeText(Person p)
    {
        if (!p.OwnsHome) return null;
        if (p.HomeValue <= 0)
            return World.TryGet(p.PartnerId) is { } partner ? $"You live in {Kinship.Genitive(partner.FirstName)} home." : null;
        return p.Mortgage > 0
            ? $"Your home is worth about {EconomySystem.Format(Ctx, p.HomeValue)}; {EconomySystem.Format(Ctx, p.Mortgage)} of the loan is left."
            : $"Your home is worth about {EconomySystem.Format(Ctx, p.HomeValue)}, and it is paid off.";
    }

    private List<(string, string)> Assets(Person p)
    {
        var rows = new List<(string, string)>();
        if (EconomySystem.Investments(p) >= 1) rows.Add(("Investments", EconomySystem.Format(Ctx, EconomySystem.Investments(p))));
        if (p.HomeValue > 0) rows.Add(("Home", EconomySystem.Format(Ctx, p.HomeValue)));
        if (p.Mortgage >= 1) rows.Add(("Mortgage", "-" + EconomySystem.Format(Ctx, p.Mortgage)));
        if (p.CottageValue >= 1) rows.Add(("Summer cottage", EconomySystem.Format(Ctx, p.CottageValue)));
        if (RentalSystem.OwnsRental(World, p)) rows.Add(("Homes you let, after loans", EconomySystem.Format(Ctx, RentalSystem.Equity(Ctx, p))));
        return rows;
    }

    private string MarketNote()
    {
        var m = Market.For(Ctx, Year);
        string Pct(double v) => $"{(v >= 0 ? "+" : "")}{Math.Round(v * 100)} %";
        return $"This year: stock market {Pct(m.Stocks)}, home prices {Pct(m.Housing)}, inflation {Pct(m.Inflation)}. " +
               $"The bank pays {Pct(m.Bank)} on savings; mortgages cost {Pct(m.MortgageRate)}.";
    }

    public MoneyView Money()
    {
        var p = Player;
        List<LedgerView> Lines(int year) => World.Ledger.Where(l => l.Year == year)
            .GroupBy(l => l.Label)
            .Select(g => new LedgerView(g.Key, Signed(g.Sum(l => l.Amount)), g.Sum(l => l.Amount)))
            .OrderByDescending(l => l.Raw)
            .ToList();
        string Signed(double v) => (v > 0 ? "+" : "") + EconomySystem.Format(Ctx, v);
        var thisYear = Lines(Year);
        var lastYear = Lines(Year - 1);
        return new MoneyView
        {
            Money = EconomySystem.Format(Ctx, p.Money),
            InDebt = p.Money < 0,
            NetWorth = EconomySystem.Format(Ctx, EconomySystem.NetWorth(Ctx, p)),
            Home = HomeText(p),
            Assets = Assets(p),
            MarketNote = MarketNote(),
            YearlyIncome = EconomySystem.FormatPay(Ctx, EconomySystem.GrossIncome(Ctx, p)) + " before tax",
            SaveRatePercent = (int)Math.Round(EconomySystem.SaveRate(Ctx, p) * 100),
            TaxPercent = (int)Math.Round(Country.TaxRate * 100),
            WelfareShare = Country.WelfareShare switch { >= 0.45 and <= 0.55 => "half", >= 0.2 and <= 0.3 => "a quarter", var w => $"{w * 100:0}%" },
            Year = Year,
            ThisYear = thisYear,
            ThisYearTotal = Signed(thisYear.Sum(l => l.Raw)),
            LastYear = lastYear,
            LastYearTotal = Signed(lastYear.Sum(l => l.Raw)),
            Holdings = p.Holdings.Select(HoldingView).OfType<HoldingView>().ToList(),
            InvestedTotal = EconomySystem.Format(Ctx, EconomySystem.Investments(p)),
            CannotInvest = CannotInvest(),
            HomeDescription = HousingSystem.DescribeForPlayer(Ctx, p),
            HousingPerMonth = HousingPerMonth(p),
            HasMortgage = p.Mortgage >= 1,
            Cottage = p.CottageValue > 0 ? EconomySystem.Format(Ctx, p.CottageValue) : null,
            CanMove = CanMove(p),
        };
    }

    // --- Investments and homes (free: they do not use up the year's time) ----------------------

    private static int Percent(double v) => (int)Math.Round(v * 100);

    private HoldingView? HoldingView(Holding h)
    {
        if (InvestmentSystem.Asset(Ctx, h.AssetId) is not { } a) return null;
        double change = h.Invested > 0 ? h.Value / h.Invested - 1 : 0;
        return new HoldingView(a.Id, a.Name, a.Kind, InvestmentSystem.Risk(a), EconomySystem.Format(Ctx, h.Invested),
            EconomySystem.Format(Ctx, h.Value), Percent(change), Percent(h.LastReturn),
            h.LastDividend >= 1 ? EconomySystem.Format(Ctx, h.LastDividend) : null, h.SinceYear);
    }

    private string? CannotInvest()
    {
        if (!CanAdvanceOrActionsAllowed() || !Player.IsAlive) return "Not now.";
        if (Player.Age(Year) < InvestmentSystem.MinAge) return $"You can start investing at {InvestmentSystem.MinAge}, with your parents' help.";
        if (Player.Activity == Activity.Prison) return "Not from prison.";
        return Player.Money < 1 ? "You have no savings to invest." : null;
    }

    /// <summary>The funds and companies on offer this year, with how they have done.</summary>
    public IReadOnlyList<AssetView> InvestmentOptions() =>
        InvestmentSystem.AvailableAssets(Ctx).Select(a => new AssetView(a.Id, a.Name, a.Kind, a.Description, InvestmentSystem.Risk(a),
            InvestmentSystem.Trailing(Ctx, a, 1) is { } one ? Percent(one) : null,
            InvestmentSystem.Trailing(Ctx, a, 5) is { } five ? Percent(five) : null,
            Percent(a.Dividend))).ToList();

    /// <summary>The player's savings in nominal money, for the amount slider.</summary>
    public double Savings => Math.Max(0, Player.Money);

    public string BuyInvestment(string assetId, double nominalAmount)
    {
        if (CannotInvest() is { } reason) return reason;
        return InvestmentSystem.Buy(Ctx, Player, assetId, nominalAmount);
    }

    public string SellInvestment(string assetId, double share)
    {
        if (!CanAdvanceOrActionsAllowed()) return "Not now.";
        var name = InvestmentSystem.Asset(Ctx, assetId)?.Name ?? "it";
        double amount = InvestmentSystem.Sell(Ctx, Player, assetId, share);
        return amount < 1 ? "There was nothing to sell." : $"You sold {name} for {EconomySystem.Format(Ctx, amount)}.";
    }

    private bool CanMove(Person p) =>
        CanAdvanceOrActionsAllowed() && p.IsAlive && p.Age(Year) >= Country.AdultAge && p.Activity != Activity.Prison
        && !p.Flags.Contains(HousingSystem.CareHomeFlag);

    private string? HousingPerMonth(Person p)
    {
        if (p.LivesWithParents || p.Flags.Contains(HousingSystem.CareHomeFlag)) return null;
        var type = HousingSystem.HomeTypeOf(Ctx, p) ?? Country.HomeTypes.FirstOrDefault(t => t.Id == (p.SharesFlat ? "room" : "two_room"));
        if (type == null) return null;
        double cost = p.OwnsHome ? HousingSystem.MonthlyOwnerCost(Ctx, p, type) : HousingSystem.MonthlyRent(Ctx, p, type);
        return EconomySystem.Format(Ctx, cost);
    }

    /// <summary>Every kind of home in the player's city: rent, price and whether the household can buy it.</summary>
    public IReadOnlyList<HomeOptionView> HomeOptions()
    {
        var p = Player;
        return Country.HomeTypes.Where(t => t.MinYear <= Year).Select(t => new HomeOptionView(t.Id, t.Name, t.Sleeps,
            p.HomeType == t.Id && !p.LivesWithParents,
            t.RentFactor > 0 ? EconomySystem.Format(Ctx, HousingSystem.MonthlyRent(Ctx, p, t)) : null,
            t.PriceFactor > 0 ? EconomySystem.Format(Ctx, HousingSystem.HomePrice(Ctx, p, t.Id)) : null,
            t.PriceFactor > 0 ? EconomySystem.Format(Ctx, HousingSystem.MonthlyOwnerCost(Ctx, p, t)) : null,
            t.PriceFactor > 0 && HousingSystem.CannotBuy(Ctx, p, t) == null,
            t.PriceFactor > 0 ? HousingSystem.CannotBuy(Ctx, p, t) : null)).ToList();
    }

    public string ChooseHome(string typeId, bool buy)
    {
        var p = Player;
        if (!CanMove(p)) return "You cannot move right now.";
        if (Country.HomeTypes.FirstOrDefault(t => t.Id == typeId) is not { } type) return "That home does not exist.";
        return buy ? HousingSystem.Buy(Ctx, p, type) : HousingSystem.Rent(Ctx, p, type);
    }

    public string RepayMortgage(double share)
    {
        if (!CanAdvanceOrActionsAllowed()) return "Not now.";
        double paid = EconomySystem.RepayMortgage(Ctx, Player, share);
        return paid < 1 ? "You have no savings to pay with." : $"You paid {EconomySystem.Format(Ctx, paid)} off the loan. {EconomySystem.Format(Ctx, Player.Mortgage)} is left.";
    }

    public string SellCottage()
    {
        if (!CanAdvanceOrActionsAllowed()) return "Not now.";
        double amount = EconomySystem.SellCottage(Ctx, Player);
        return amount < 1 ? "You have no cottage." : $"The cottage is sold for {EconomySystem.Format(Ctx, amount)}.";
    }

    // --- Decades ---------------------------------------------------------------------------------

    /// <summary>The decades a life can begin in, for the title screen: (1970, "The 1970s: the welfare state").</summary>
    public static IReadOnlyList<(int Year, string Title)> StartDecades(string countryId = "sweden", ContentDb? content = null)
    {
        var country = (content ?? ContentDb.Embedded).Countries[countryId];
        return country.Decades.Where(d => d.Year >= country.MinStartYear && d.Year <= country.MaxStartYear)
            .Select(d => (d.Year, $"The {d.Year}s: {d.Name}")).ToList();
    }

    /// <summary>True in the first year of a decade, when the chapter page is shown.</summary>
    public bool IsNewDecade => Year % 10 == 0 && Year > World.StartYear;

    /// <summary>The chapter page for the decade that has just begun (or any decade, for the screenshot tour).</summary>
    public ChapterView Chapter(int? decade = null)
    {
        int year = decade ?? Year / 10 * 10;
        var def = Country.Decades.LastOrDefault(d => d.Year <= year);
        var p = Player;
        int since = year - 10;
        var family = World.People.Where(x => x.InFamily).ToList();
        int born = family.Count(x => x.BirthYear >= since && x.BirthYear < year);
        int died = family.Count(x => x.DeathYear is { } d && d >= since && d < year);
        int weddings = World.Chronicle.Count(l => l.Year >= since && l.Year < year && l.Category == "love" && l.Text.Contains("got married")
                                                  && l.PersonIds.Any(id => World.TryGet(id) is { InFamily: true }));
        int alive = family.Count(x => x.IsAlive);
        string Count(int n, string one, string many) => n == 1 ? $"one {one}" : $"{n} {many}";
        var lines = new List<string>();
        if (born + died + weddings > 0)
            lines.Add($"Since {since}: " + string.Join(", ", new[]
            {
                born > 0 ? Count(born, "child born", "children born") : null,
                weddings > 0 ? Count(weddings, "wedding", "weddings") : null,
                died > 0 ? Count(died, "funeral", "funerals") : null,
            }.OfType<string>()) + ".");
        lines.Add($"The family is {alive} people now.");
        string playerLine = p.IsAlive ? $"{p.FirstName} is {p.Age(year)}." : "";
        return new ChapterView(year, $"The {year}s", def?.Name ?? "", def?.Text ?? "", playerLine, lines);
    }

    /// <summary>This year's lines, with what happened to the player told to them: "You inherited …".</summary>
    public IReadOnlyList<ChronicleLine> NewsThisYear() =>
        RelevantLines(World.Chronicle.Where(e => e.Year == Year)).Select(l => l with { Text = ToYou(l.Text) }).ToList();

    /// <summary>"Marie inherited …" → "You inherited …", "Marie was released" → "You were released".</summary>
    internal string ToYou(string text)
    {
        foreach (var name in new[] { Player.FullName, Player.FirstName })
        {
            if (!text.StartsWith(name + " ")) continue;
            var rest = text[(name.Length + 1)..];
            foreach (var (from, to) in new[] { ("was ", "were "), ("has ", "have "), ("is ", "are "), ("doesn't ", "don't ") })
                if (rest.StartsWith(from)) { rest = to + rest[from.Length..]; break; }
            return "You " + rest;
        }
        return text;
    }

    // --- Chronicle ---------------------------------------------------------------------------

    /// <summary>"Single – Monica broke up with him in 1984", "Widowed – Erik died in 2001" ...</summary>
    private string SingleText(Person p)
    {
        var ex = World.TryGet(p.LastSplitWithId);
        if (ex == null || p.LastSplitYear is not { } year || Year - year > 10)
            return p.Flags.Contains("widowed") ? (p.Sex == Sex.Male ? "Widower" : "Widow") : "Single";
        if (p.Flags.Contains("widowed") && !ex.IsAlive && ex.DeathYear == year)
            return $"Widowed when {ex.FirstName} died in {year}";
        string pronoun = p.Id == Player.Id ? "you" : p.Sex == Sex.Male ? "him" : "her";
        return p.LastSplitByThem ? $"Single since {ex.FirstName} broke up with {pronoun} in {year}" : $"Single since leaving {ex.FirstName} in {year}";
    }

    public IReadOnlyList<ChronicleLine> Chronicle(int minImportance = 1, int? personId = null) =>
        World.Chronicle
            .Where(e => e.Importance >= minImportance && (personId == null || e.PersonIds.Contains(personId.Value)))
            .Select(ToLine)
            .ToList();

    // --- Generations -------------------------------------------------------------------------

    public IReadOnlyList<HeirCandidate> HeirCandidates()
    {
        var dead = Player;
        // Search the whole family (closest first) so the story goes on as long as the bloodline lives.
        var dist = Kinship.Distances(World, dead, 16);
        int Priority(Person p)
        {
            if (dead.ChildIds.Contains(p.Id)) return 0;
            if (Kinship.Grandchildren(World, dead).Any(g => g.Id == p.Id)) return 1;
            // Blood relatives carry the family on; people who married in come last.
            if (Kinship.Siblings(World, dead).Any(s => s.Id == p.Id)) return 2;
            if (p.IsBlood) return 3 + dist[p.Id];
            if (dead.PartnerId == p.Id) return 20;
            return 21 + dist[p.Id];
        }
        return dist.Keys.Select(World.Get)
            .Where(p => p.IsAlive && p.InFamily)
            .OrderBy(Priority).ThenByDescending(p => p.Age(Year))
            .Select(p => new HeirCandidate(p.Id, p.FullName, TextFormatter.Capitalize(Kinship.Label(World, dead, p)),
                p.Age(Year), EconomySystem.Format(Ctx, p.Money), CareerSystem.ActivityText(Ctx, p)))
            .ToList();
    }

    public LifeSummary SummarizeLife(int personId)
    {
        var p = World.Get(personId);
        var lines = World.Chronicle.Where(e => e.PersonIds.Contains(personId) && e.Importance >= 2).Select(ToLine).ToList();
        int grandchildren = Kinship.Grandchildren(World, p).Count();
        int partners = p.ExPartnerIds.Count + (p.PartnerId != null ? 1 : 0);
        string job = CareerSystem.Title(Ctx, p);
        return new LifeSummary(p.FullName, p.BirthYear, p.DeathYear ?? Year, p.Age(Year), p.CauseOfDeath ?? "",
            p.ChildIds.Count, grandchildren, partners, string.IsNullOrEmpty(job) ? CareerSystem.ActivityText(Ctx, p) : job,
            EconomySystem.Format(Ctx, p.PeakNetWorth), lines);
    }

    public void ChooseHeir(int personId)
    {
        if (!NeedsSuccession) throw new InvalidOperationException("No succession is needed right now.");
        if (HeirCandidates().All(c => c.Id != personId)) throw new InvalidOperationException("That person cannot take over.");
        var old = Player;
        var heir = World.Get(personId);
        string relation = Kinship.Label(World, old, heir);
        SwitchPlayer(heir);
        World.Log($"The story continues with {heir.FullName}, {Kinship.Genitive(old.FirstName)} {relation}, aged {heir.Age(Year)}.", 3, "succession", heir.Id, old.Id);
    }

    /// <summary>Scenarios: the player becomes someone else while the current player lives on (as an NPC).</summary>
    internal void TakeOver(Person other)
    {
        var old = Player;
        World.PlayedIds.Remove(old.Id);
        SwitchPlayer(other);
        World.Log($"The story follows {other.FullName}, aged {other.Age(Year)}.", 3, "succession", other.Id);
    }

    private void SwitchPlayer(Person p)
    {
        // The next player lives in another country: the story moves there.
        if (p.Abroad is { } country) EmigrationSystem.Reframe(Ctx, country, EmigrationSystem.Travellers(Ctx, p));
        World.PlayerId = p.Id;
        World.PlayedIds.Add(p.Id);
        InvestmentSystem.ConvertSimple(Ctx, p);
        World.EventHistory.Clear();
        World.PendingEvents.Clear();
        World.ActionsThisYear.Clear();
        World.ActionPoints = ActionPointsFor(p);
        SocialSystem.Update(Ctx);
        DreamSystem.Offer(Ctx, p);
        PickWish();
    }

    /// <summary>Ends the game when nobody is left to continue the family.</summary>
    public void EndGame() => World.GameOver = true;

    /// <summary>
    /// Achievements this family has earned that the player did not already have (the UI keeps the
    /// list across families and calls this after each year, a succession and the end of the game).
    /// </summary>
    public IReadOnlyList<AchievementDef> NewAchievements(IReadOnlySet<string> alreadyUnlocked) =>
        AchievementSystem.NewlyEarned(Ctx, alreadyUnlocked);

    /// <summary>The businesses the player runs (BusinessSystem).</summary>
    public IReadOnlyList<BusinessView> Businesses() => BusinessSystem.OwnedBy(World, Player).Select(b => new BusinessView(b.Id, b.Name,
        Content.BusinessKinds.GetValueOrDefault(b.Kind)?.Name ?? b.Kind, b.FoundedYear, EconomySystem.Format(Ctx, Ctx.NominalRef(Math.Max(0, b.Value))),
        EconomySystem.Format(Ctx, Ctx.NominalRef(b.LastProfit)), b.LastProfit >= 0, b.Owners.Count)).ToList();

    /// <summary>Sells one of the player's businesses. Returns what happened.</summary>
    public string SellBusiness(int id) => World.Businesses.FirstOrDefault(b => b.Id == id && b.IsOpen && b.OwnerId == Player.Id) is { } b ? BusinessSystem.Sell(Ctx, b, Player) : "";

    /// <summary>What the player could do to their own home (HomeProjectSystem); empty when they do not own it.</summary>
    public IReadOnlyList<HomeProjectView> HomeProjects()
    {
        if (!Player.IsAlive || HomeProjectSystem.Holder(World, Player) is not { } holder) return Array.Empty<HomeProjectView>();
        return HomeProjectSystem.Fitting(Ctx, Player).Select(d =>
        {
            double cost = HomeProjectSystem.Cost(Ctx, Player, d);
            bool done = HomeProjectSystem.IsDone(Ctx, holder, d);
            return new HomeProjectView(d.Id, d.Name, d.Text, EconomySystem.Format(Ctx, cost), Player.Money >= cost, done ? holder.HomeProjects[d.Id] : null);
        }).ToList();
    }

    public string DoHomeProject(string id) => HomeProjectSystem.Do(Ctx, Player, id);

    /// <summary>Homes the player lets out (RentalSystem).</summary>
    public IReadOnlyList<RentalView> Rentals() => RentalSystem.OwnedBy(World, Player).Select(r => new RentalView(r.Id,
        TextFormatter.Capitalize(r.Name), r.BoughtYear, r.Owners.Count, EconomySystem.Format(Ctx, Ctx.NominalRef(r.Value)),
        r.Loan >= 1 ? EconomySystem.Format(Ctx, Ctx.NominalRef(r.Loan)) : null,
        EconomySystem.Format(Ctx, Ctx.NominalRef(Math.Abs(r.LastNet))), r.LastNet >= 0, r.LastNet != 0)).ToList();

    /// <summary>What the player could buy to let in their city: price, cash needed, and whether there is enough.</summary>
    public IReadOnlyList<RentalOptionView> RentalOptions()
    {
        if (!Player.IsAlive || Player.Age(Year) < 18 || Player.Activity == Activity.Prison) return Array.Empty<RentalOptionView>();
        return RentalSystem.Options(Ctx, Player).Select(o =>
        {
            double cash = Ctx.NominalRef(RentalSystem.CashNeeded(Ctx, o.Price));
            return new RentalOptionView(o.Type.Id, o.Type.Name, EconomySystem.Format(Ctx, Ctx.NominalRef(o.Price)), EconomySystem.Format(Ctx, cash), Player.Money >= cash);
        }).ToList();
    }

    public string BuyRental(string typeId)
    {
        RentalSystem.Buy(Ctx, Player, typeId, out var message);
        return message;
    }

    public string SellRental(int id) => World.Rentals.FirstOrDefault(r => r.Id == id && r.IsHeld && r.OwnerId == Player.Id) is { } r ? RentalSystem.Sell(Ctx, r, Player) : "";

    /// <summary>The player's children under eighteen and how each is raised (UpbringingSystem).</summary>
    public IReadOnlyList<UpbringingView> Upbringing() => Kinship.Children(World, Player)
        .Where(c => c.IsAlive && c.Age(Year) < Country.AdultAge && c.Abroad == Player.Abroad).OrderBy(c => c.BirthYear)
        .Select(c => new UpbringingView(c.Id, c.FirstName, c.Age(Year), UpbringingSystem.StyleFor(Ctx, c), c.Upbringing != null, UpbringingSystem.Shaping(c))).ToList();

    /// <summary>Chooses how one of the player's children is raised from now on.</summary>
    public void SetUpbringing(int childId, string style)
    {
        if (!UpbringingSystem.Styles.Contains(style) || World.TryGet(childId) is not { } child || !child.ParentIds.Contains(Player.Id)) return;
        child.Upbringing = style;
    }

    /// <summary>What the player keeps of the family's things (HeirloomSystem).</summary>
    public IReadOnlyList<HeirloomView> Heirlooms() => HeirloomSystem.OwnedBy(World, Player).Select(h => new HeirloomView(h.Id, h.Name,
        Content.Heirlooms.TryGetValue(h.Kind, out var def) ? def.Text : "", HeirloomSystem.HistoryText(h),
        EconomySystem.Format(Ctx, HeirloomSystem.Value(Ctx, h)), World.TryGet(h.PromisedToId)?.FirstName)).ToList();

    /// <summary>Sells an heirloom out of the family. Returns what happened.</summary>
    public string SellHeirloom(int id) => World.Heirlooms.FirstOrDefault(h => h.Id == id && h.OwnerId == Player.Id) is { } h ? HeirloomSystem.Sell(Ctx, Player, h) : "";

    /// <summary>What the family is known for now (ReputationSystem): "A family of readers", "Close knit" ...</summary>
    public IReadOnlyList<FamilyTraitDef> FamilyTraits() => ReputationSystem.Current(Ctx);

    /// <summary>All achievements, in the order they are listed.</summary>
    public static IReadOnlyList<AchievementDef> AllAchievements(ContentDb? content = null) => (content ?? ContentDb.Embedded).Achievements;

    /// <summary>The last page of the family, in words (EpilogueSystem).</summary>
    public EpilogueView Epilogue() => EpilogueSystem.Write(Ctx);

    public FamilyStats Stats()
    {
        var fam = World.People.Where(p => p.InFamily || p.IsBlood).ToList();
        var richest = World.People.Where(p => p.InFamily).OrderByDescending(p => p.PeakNetWorth).FirstOrDefault();
        var blood = fam.Where(p => p.IsBlood).ToList();
        return new FamilyStats(
            blood.Count == 0 ? 0 : blood.Max(p => p.Generation) - blood.Min(p => p.Generation) + 1,
            fam.Count,
            World.PlayedIds.Count,
            EconomySystem.Format(Ctx, richest?.PeakNetWorth ?? 0),
            richest?.FullName ?? "",
            World.Chronicle.Count(e => e.Text.Contains(" divorced.")),
            World.Secrets.Count(s => s.Kind == "affair" && s.Revealed),
            Year - World.StartYear);
    }

    /// <summary>The family tree from the founders down. Founder couples share one root.</summary>
    /// <summary>The family around one person, for the graphical family tree.</summary>
    public FamilyFocusView FamilyFocus(int id)
    {
        var w = World;
        var p = w.Get(id);
        TreePerson Card(Person x, bool half = false) => new(
            x.Id, x.FullName, x.DeathYear is { } d ? $"{x.BirthYear}–{d}" : $"b. {x.BirthYear}", x.IsAlive,
            Kinship.Label(w, Player, x), x.Id == Player.Id, w.PlayedIds.Contains(x.Id), half);
        IReadOnlyList<TreePerson> Cards(IEnumerable<Person> people) => people.OrderBy(x => x.BirthYear).ThenBy(x => x.Id).Select(x => Card(x)).ToList();

        var parents = Kinship.Parents(w, p).OrderBy(x => x.Sex == Sex.Male ? 0 : 1).ToList();
        var siblings = Kinship.Siblings(w, p).Append(p).OrderBy(x => x.BirthYear).ThenBy(x => x.Id)
            .Select(x => Card(x, x.Id != p.Id && Kinship.IsHalfSibling(p, x))).ToList();
        var children = Kinship.Children(w, p).ToList();
        return new FamilyFocusView(
            Card(p),
            parents.Select(x => Card(x)).ToList(),
            parents.ToDictionary(x => x.Id, x => Cards(Kinship.Parents(w, x).OrderBy(g => g.Sex == Sex.Male ? 0 : 1))),
            siblings,
            w.TryGet(p.PartnerId) is { } partner ? Card(partner) : null,
            Cards(children),
            children.ToDictionary(c => c.Id, c => Cards(Kinship.Children(w, c))));
    }

    public IReadOnlyList<TreeNode> FamilyTree()
    {
        var founders = World.People.Where(p => p.IsBlood && p.ParentIds.Count == 0).ToList();
        var roots = new List<TreeNode>();
        var used = new HashSet<int>();
        var shown = new HashSet<int>();
        foreach (var f in founders)
        {
            if (used.Contains(f.Id)) continue;
            used.Add(f.Id);
            foreach (var partnerId in f.ExPartnerIds.Append(f.PartnerId ?? 0))
                if (founders.Any(x => x.Id == partnerId)) used.Add(partnerId);
            roots.Add(BuildNode(f, 0, shown));
        }
        return roots;
    }

    private TreeNode BuildNode(Person p, int depth, HashSet<int> shown)
    {
        string Years(Person x) => x.IsAlive ? $"b. {x.BirthYear}" : $"{x.BirthYear}–{x.DeathYear}";
        // Someone who descends from two founder couples (e.g. you) is shown in full only once.
        if (!shown.Add(p.Id))
            return new TreeNode(p.Id, $"{p.FullName} ({Years(p)})", p.IsAlive, p.Id == World.PlayerId, World.PlayedIds.Contains(p.Id),
                Array.Empty<string>(), Array.Empty<TreeNode>(), IsReference: true);
        var partners = p.ExPartnerIds.Select(World.Get)
            .Concat(World.TryGet(p.PartnerId) is { } cur && !p.ExPartnerIds.Contains(cur.Id) ? new[] { cur } : Array.Empty<Person>())
            .Where(x => x.ChildIds.Intersect(p.ChildIds).Any() || x.Id == p.PartnerId || p.Flags.Contains($"married_to_{x.Id}"))
            .Select(x => $"{x.FullName} ({Years(x)})")
            .ToList();
        var children = depth > 12 ? new List<TreeNode>() :
            p.ChildIds.Select(World.Get).OrderBy(c => c.BirthYear).Select(c => BuildNode(c, depth + 1, shown)).ToList();
        return new TreeNode(p.Id, $"{p.FullName} ({Years(p)})", p.IsAlive, p.Id == World.PlayerId, World.PlayedIds.Contains(p.Id),
            partners, children);
    }
}
