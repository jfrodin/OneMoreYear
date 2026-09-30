// Headless balancing tool: plays games automatically and prints the family chronicle and stats.
// Usage: dotnet run --project tools/OneMoreYear.SimRunner -- [seed] [years] [startYear] [--quiet]
using OneMoreYear.Simulation;

Console.OutputEncoding = System.Text.Encoding.UTF8;
if (args.FirstOrDefault(a => a.StartsWith("--load=")) is { } loadArg)
{
    Inspect.Run(loadArg["--load=".Length..], args.FirstOrDefault(a => a.StartsWith("--who="))?["--who=".Length..] ?? "");
    return;
}
ulong seed = args.Length > 0 && ulong.TryParse(args[0], out var s) ? s : 1;
int years = args.Length > 1 && int.TryParse(args[1], out var y) ? y : 120;
int startYear = args.Length > 2 && int.TryParse(args[2], out var sy) ? sy : 1950;
bool quiet = args.Contains("--quiet");

var session = GameSession.NewGame(new NewGameOptions { Seed = seed, StartYear = startYear });
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

if (args.Contains("--debug-end"))
{
    var w = session.World;
    var p = w.Player;
    Console.WriteLine($"Last player: {p.FullName} b.{p.BirthYear} d.{p.DeathYear} kids={p.ChildIds.Count} partner={p.PartnerId}");
    foreach (var x in OneMoreYear.Simulation.Systems.Kinship.Circle(w, p, includeDead: true))
        Console.WriteLine($"  {x.FullName} alive={x.IsAlive} inFamily={x.InFamily} label={OneMoreYear.Simulation.Systems.Kinship.Label(w, p, x)}");
}
