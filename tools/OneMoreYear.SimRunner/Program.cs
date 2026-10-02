// Headless balancing tool: plays games automatically and prints the family chronicle and stats.
// Usage: dotnet run --project tools/OneMoreYear.SimRunner -- [seed] [years] [startYear] [--quiet]
// Or: --scenarios (where each test scenario starts), --scenario=id (auto-play from a scenario)
using OneMoreYear.Simulation;

Console.OutputEncoding = System.Text.Encoding.UTF8;
// --play-from=save.json: loads a save and plays on from there, to reproduce bugs.
if (args.FirstOrDefault(a => a.StartsWith("--play-from=")) is { } playArg)
{
    var loaded = GameSession.Load(File.ReadAllText(playArg["--play-from=".Length..]));
    Console.WriteLine($"Loaded {loaded.Year}, {loaded.Player.FullName}, can advance: {loaded.CanAdvance}, needs succession: {loaded.NeedsSuccession}");
    foreach (var e in loaded.CurrentEvents()) Console.WriteLine($"  event {e.Title} resolved={e.Resolved}");
    var playBot = new AutoPlayer(1);
    for (int i = 0; i < 10 && playBot.PlayYear(loaded); i++) Console.WriteLine($"  -> {loaded.Year}");
    return;
}
if (args.FirstOrDefault(a => a.StartsWith("--load=")) is { } loadArg)
{
    Inspect.Run(loadArg["--load=".Length..], args.FirstOrDefault(a => a.StartsWith("--who="))?["--who=".Length..] ?? "");
    return;
}
if (args.FirstOrDefault(a => a.StartsWith("--find-seed=")) is { } fs) { SeedSearch.Run(fs["--find-seed=".Length..], args.FirstOrDefault(a => a.StartsWith("--count=")) is { } c ? int.Parse(c[8..]) : 60); return; }
if (args.Contains("--scenarios")) { ScenarioReport.Run(); return; }
if (args.Contains("--dating")) { DatingReport.Run(); return; }
if (args.FirstOrDefault(a => a.StartsWith("--achievements")) is { } ach) { AchievementReport.Run(ach.Contains('=') ? int.Parse(ach[15..]) : 40); return; }
if (args.FirstOrDefault(a => a.StartsWith("--coverage")) is { } cov) { CoverageReport.Run(cov.Contains('=') ? int.Parse(cov[11..]) : 60, args.FirstOrDefault(a => a.StartsWith("--country="))?["--country=".Length..] ?? "sweden"); return; }
ulong? givenSeed = args.Length > 0 && ulong.TryParse(args[0], out var s) ? s : null;
int years = args.Length > 1 && int.TryParse(args[1], out var y) ? y : 120;
int startYear = args.Length > 2 && int.TryParse(args[2], out var sy) ? sy : 1950;
bool quiet = args.Contains("--quiet");
string? scenarioId = args.FirstOrDefault(a => a.StartsWith("--scenario="))?["--scenario=".Length..];

// A scenario brings its own seed unless one is given.
string countryId = args.FirstOrDefault(a => a.StartsWith("--country="))?["--country=".Length..] ?? "sweden";
var session = GameSession.NewGame(new NewGameOptions { StartYear = startYear, ScenarioId = scenarioId, Seed = givenSeed ?? (scenarioId == null ? 1UL : null), CountryId = countryId });
ulong seed = session.World.Seed;
var bot = new AutoPlayer(seed);
var watch = System.Diagnostics.Stopwatch.StartNew();

for (int i = 0; i < years; i++)
{
    if (!quiet) Console.Error.Write($"\r{session.Year} people={session.World.People.Count} ");
    if (!bot.PlayYear(session)) break;
}
Console.Error.WriteLine();

if (!quiet)
{
    foreach (var line in session.Chronicle(minImportance: 2))
        Console.WriteLine($"{line.Year}  {line.Text}");
}

var stats = session.Stats();
Console.WriteLine();
Console.WriteLine($"Seed {seed}: {stats.Years} years, {stats.Generations} generations, {stats.Characters} characters played, " +
                  $"{stats.FamilyMembers} family members, {stats.Divorces} divorces, {stats.Affairs} affairs revealed, " +
                  $"richest {stats.LargestFortuneOwner} ({stats.LargestFortune}), people simulated {session.World.People.Count}, " +
                  $"game over: {session.GameOver}, {watch.ElapsedMilliseconds} ms");

if (args.Contains("--wealth")) WealthReport.Run(session);
if (args.Contains("--debug-end"))
{
    var w = session.World;
    var p = w.Player;
    Console.WriteLine($"Last player: {p.FullName} b.{p.BirthYear} d.{p.DeathYear} kids={p.ChildIds.Count} partner={p.PartnerId}");
    foreach (var x in OneMoreYear.Simulation.Systems.Kinship.Circle(w, p, includeDead: true))
        Console.WriteLine($"  {x.FullName} alive={x.IsAlive} inFamily={x.InFamily} label={OneMoreYear.Simulation.Systems.Kinship.Label(w, p, x)}");
}
