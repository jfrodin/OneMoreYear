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
        if (scenario != null)
            options = options with
            {
                Seed = options.Seed ?? (string.IsNullOrWhiteSpace(options.SeedCode) ? scenario.Seed : Core.SeedCode.ToSeed(options.SeedCode)),
                StartYear = scenario.StartYear,
            };
        string? code = options.Seed != null ? null : Core.SeedCode.Normalize(options.SeedCode ?? "") is { Length: > 0 } typed ? typed : Core.SeedCode.Random();
        ulong seed = options.Seed ?? Core.SeedCode.ToSeed(code!);
        var world = new World
        {
            Seed = seed,
            SeedCode = code,
            ContentSettings = options.ContentSettings?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new(),
            CountryId = options.CountryId,
            StartYear = options.StartYear,
            Year = options.StartYear,
            Rng = new SimRandom(seed),
        };
        var session = new GameSession(world, content);
        StartingFamily.Create(session.Ctx);
        if (scenario != null) Scenarios.ApplyFamily(session.Ctx, scenario);
        session.World.ActionPoints = session.ActionPointsFor(session.Player);
        if (scenario != null) Scenarios.FastForward(session, scenario);
        return session;
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
    public static IReadOnlyList<ScenarioDef> AvailableScenarios(ContentDb? content = null) => (content ?? ContentDb.Embedded).Scenarios;

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
            if (p.Age(ctx.Year) == ctx.Country.AdultAge) PersonFactory.RollAdultTraits(ctx, p);
            CareerSystem.Update(ctx, p, jobLoss);
            HousingSystem.Update(ctx, p);
            EconomySystem.Update(ctx, p, market);
            p.PeakNetWorth = Math.Max(p.PeakNetWorth, EconomySystem.NetWorth(ctx, p));
            if (p.InFamily) LifeSystem.UpdateWill(ctx, p);
        }

        FamilySystem.Update(ctx);
        SecretSystem.Update(ctx);
        DarkSystem.Update(ctx);
        CrimeSystem.Update(ctx);
        RelationshipSystem.UpdateYear(ctx);
        SocialSystem.Update(ctx);

        if (Player.IsAlive)
        {
            EventSystem.GenerateRandomEvents(ctx);
            w.ActionPoints = ActionPointsFor(Player);
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
            foreach (var name in new[] { p.FullName, p.FirstName })
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
            choices.Add(new ChoiceView(i, $"{lvl.Title}{(employer != null ? $" at {employer}" : $"  ·  {occ.Name}")}  ·  {EconomySystem.FormatPay(Ctx, lvl.Salary)}",
                $"{occ.Name}. {fit} Top of this career: {occ.Levels[^1].Title}.", null, true));
        }
        int offset = pending.Options.Count;
        for (int i = 0; i < def.Choices.Count; i++)
        {
            var c = def.Choices[i];
            string? hint = c.Hint == null ? null : TextFormatter.Format(Ctx, c.Hint, pending);
            if (EventSystem.StudyProgramme(Ctx, c) is { } prog)
            {
                double need = CareerSystem.RequiredGrades(Ctx, Player, prog);
                hint ??= prog.Description + (need > 0 ? $" Needs grades {need:0} – yours are {Player.Grades:0}." : "");
            }
            if (EventSystem.StartsRomance(c) && World.TryGet(Player.PartnerId) is { } partner)
                hint = $"You're with {partner.FirstName} – this would be an affair." + (hint == null ? "" : " " + hint);
            choices.Add(new ChoiceView(offset + i, TextFormatter.Format(Ctx, c.Text, pending), hint,
                c.Chance != null ? (int)Math.Round(EventSystem.SuccessChance(Ctx, c, pending) * 100) : null,
                EventSystem.IsChoiceAvailable(Ctx, c, pending)));
        }
        var involved = pending.Roles.Values.ToList();
        return new EventView(pending.Uid, TextFormatter.Format(Ctx, def.Title, pending),
            Annotate(TextFormatter.Format(Ctx, def.Text, pending), involved),
            choices, pending.Resolved, pending.OutcomeText == null ? null : Annotate(pending.OutcomeText, involved),
            pending.Roles.TryGetValue("target", out var t) ? t : null);
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
            bool isSelf = def.Trigger == "self";
            if (def.Trigger != "action" && !isSelf || !EventSystem.Allowed(Ctx, def)) continue;
            // In prison, only prison life is possible.
            if ((player.Activity == Activity.Prison) != (def.Category == "prison")) continue;
            if (isSelf != (target == null)) continue;
            if (!EventSystem.Matches(Ctx, def.Conditions, player, player)) continue;
            if (target != null)
            {
                if (def.Target == null || !Kinship.MatchesRole(World, player, target, def.Target.Role, distances)) continue;
                if (!EventSystem.Matches(Ctx, def.Target.Conditions, target, player)) continue;
            }
            var choice = def.Choices[0];
            if (!EventSystem.Matches(Ctx, choice.Requires, player, player)) continue;

            var probe = new PendingEvent { EventId = def.Id };
            if (target != null) probe.Roles["target"] = target.Id;
            int? chance = choice.Chance != null ? (int)Math.Round(EventSystem.SuccessChance(Ctx, choice, probe) * 100) : null;
            bool used = World.ActionsThisYear.Contains(ActionKey(def.Id, targetId));
            result.Add(new ActionView(def.Id, TextFormatter.Format(Ctx, def.Title, probe),
                choice.Hint == null ? null : TextFormatter.Format(Ctx, choice.Hint, probe), chance,
                !used && World.ActionPoints > 0 && CanAdvanceOrActionsAllowed(), def.Category));
        }
        return result;
    }

    private bool CanAdvanceOrActionsAllowed() => !NeedsSuccession && !GameOver;

    private static string ActionKey(string id, int? target) => $"{id}:{target?.ToString() ?? "self"}";

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
    public PortraitView Portrait(int id)
    {
        var p = World.Get(id);
        var face = Faces.Of(World, p, Content);
        int age = p.Age(Year);
        double bmi = Appearance.WeightAt(p, age) / Math.Pow(Math.Max(0.5, Appearance.HeightAt(p, age) / 100.0), 2);
        double normal = age < 12 ? 16 : age < 18 ? 19 : 22.5;
        return new PortraitView
        {
            Id = p.Id,
            Male = p.Sex == Sex.Male,
            Age = age,
            Alive = p.IsAlive,
            HairColor = p.HairColor,
            EyeColor = p.EyeColor,
            Grey = Faces.GreyAt(face, age),
            Bald = Faces.BaldAt(face, p.Sex, age),
            Heaviness = Math.Clamp(0.5 + (bmi - normal) / 16, 0, 1),
            Mood = Math.Clamp((p.Happiness - 50) / 35, -1, 1),
            Glasses = age >= face.GlassesFromAge,
            HeightCm = Appearance.HeightAt(p, age),
            Fitness = p.Fitness,
            Face = face,
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
                EducationLevel.Secondary => "Upper secondary",
                EducationLevel.Primary => "Primary school",
                _ => "None"
            },
            Partner = partnerText,
            Traits = p.Traits.Where(Content.Traits.ContainsKey)
                .Select(t => (Content.Traits[t].Name, Content.Traits[t].Description, Content.Traits[t].Tone)).ToList(),
            Condition = p.Addiction != null && p.IsAlive ? $"Struggling with {DarkSystem.What(p.Addiction)}" : null,
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

    public CareerView Career()
    {
        var p = Player;
        var prog = Content.Programme(p.ProgrammeId);
        var occ = Content.Occupation(p.OccupationId);
        var ladder = occ == null ? new List<LadderStep>() : occ.Levels.Select((l, i) =>
        {
            var req = new List<string>();
            if (l.MinEducation > EducationLevel.None) req.Add(CareerSystem.EducationName(l.MinEducation));
            if (l.RequiresDegree is { Count: > 0 } degrees)
                req.Add(string.Join(" or ", degrees.Select(d => Content.Programme(d)?.Name ?? d)));
            return new LadderStep(l.Title, EconomySystem.FormatPay(Ctx, l.Salary),
                req.Count == 0 ? "No requirements" : "Needs " + string.Join(", ", req),
                i == p.OccupationLevel, CareerSystem.QualifiesFor(p, l));
        }).ToList();

        string? promotionNote = null;
        if (occ != null && p.OccupationLevel + 1 < occ.Levels.Count)
        {
            var next = occ.Levels[p.OccupationLevel + 1];
            promotionNote = !CareerSystem.QualifiesFor(p, next) ? $"To become {CareerSystem.Article(next.Title)} you need more education."
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
                EducationLevel.Secondary => "Upper secondary school",
                EducationLevel.Primary => "Primary school",
                _ => p.Age(Year) < 7 ? "Not in school yet" : "In school"
            },
            Programme = prog?.Name,
            ProgrammeDescription = prog?.Description,
            YearsLeft = p.Activity == Activity.Studying ? p.StudyYearsLeft : 0,
            Grades = p.Age(Year) >= 7 ? p.Grades : null,
            PartTimeJob = p.Flags.Contains(CareerSystem.PartTimeFlag),
            Degrees = p.Degrees.Select(d => Content.Programme(d)?.Name ?? d).ToList(),
            JobTitle = occ == null ? null : CareerSystem.Title(Ctx, p),
            Employer = occ == null || occ.Id == "crime" ? null : p.Employer,
            Field = occ?.Name,
            Salary = occ == null ? null : EconomySystem.FormatPay(Ctx, p.Income),
            YearsInJob = p.YearsInJob,
            Performance = occ == null ? null : p.Performance,
            PromotionChancePercent = (int)Math.Round(CareerSystem.PromotionChance(Ctx, p) * 100),
            PromotionNote = promotionNote,
            Ladder = ladder,
            CriminalRecord = p.CriminalRecord.Select(r => $"{r.Year}: {Content.Crimes.GetValueOrDefault(r.CrimeId)?.Name ?? r.CrimeId} – {r.Sentence}").ToList(),
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
        if (p.Funds >= 1) rows.Add(("Funds", EconomySystem.Format(Ctx, p.Funds)));
        if (p.Stocks >= 1) rows.Add(("Shares", EconomySystem.Format(Ctx, p.Stocks)));
        if (p.HomeValue > 0) rows.Add(("Home", EconomySystem.Format(Ctx, p.HomeValue)));
        if (p.Mortgage >= 1) rows.Add(("Mortgage", "-" + EconomySystem.Format(Ctx, p.Mortgage)));
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
            Year = Year,
            ThisYear = thisYear,
            ThisYearTotal = Signed(thisYear.Sum(l => l.Raw)),
            LastYear = lastYear,
            LastYearTotal = Signed(lastYear.Sum(l => l.Raw)),
        };
    }

    /// <summary>Everything that has happened to the player's circle so far this year.</summary>
    public IReadOnlyList<ChronicleLine> NewsThisYear() => RelevantLines(World.Chronicle.Where(e => e.Year == Year));

    // --- Chronicle ---------------------------------------------------------------------------

    /// <summary>"Single – Monica broke up with him in 1984", "Widowed – Erik died in 2001" ...</summary>
    private string SingleText(Person p)
    {
        var ex = World.TryGet(p.LastSplitWithId);
        if (ex == null || p.LastSplitYear is not { } year || Year - year > 10)
            return p.Flags.Contains("widowed") ? (p.Sex == Sex.Male ? "Widower" : "Widow") : "Single";
        if (p.Flags.Contains("widowed") && !ex.IsAlive && ex.DeathYear == year)
            return $"Widowed – {ex.FirstName} died in {year}";
        string pronoun = p.Id == Player.Id ? "you" : p.Sex == Sex.Male ? "him" : "her";
        return p.LastSplitByThem ? $"Single – {ex.FirstName} broke up with {pronoun} in {year}" : $"Single – left {ex.FirstName} in {year}";
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
        World.PlayerId = p.Id;
        World.PlayedIds.Add(p.Id);
        World.EventHistory.Clear();
        World.PendingEvents.Clear();
        World.ActionsThisYear.Clear();
        World.ActionPoints = ActionPointsFor(p);
        SocialSystem.Update(Ctx);
    }

    /// <summary>Ends the game when nobody is left to continue the family.</summary>
    public void EndGame() => World.GameOver = true;

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
