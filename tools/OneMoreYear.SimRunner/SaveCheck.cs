using OneMoreYear.Simulation;

/// <summary>
/// --savecheck=DIR[;DIR...]: loads every save in the folders (read only), plays 40 more years with the
/// bot and asks for every view the UI uses. Old saves must keep working as the game grows.
/// </summary>
static class SaveCheck
{
    public static void Run(string dirs)
    {
        int ok = 0, failed = 0;
        foreach (var dir in dirs.Split(';', StringSplitOptions.RemoveEmptyEntries))
            foreach (var file in Directory.GetFiles(dir, "*.json").Where(f => !f.EndsWith(".info.json") && !f.EndsWith("settings.json")).OrderBy(f => f))
            {
                string step = "load";
                try
                {
                    var s = GameSession.Load(File.ReadAllText(file));
                    step = "views";
                    Views(s);
                    step = "play";
                    var bot = new AutoPlayer(7);
                    for (int i = 0; i < 40 && bot.PlayYear(s); i++) { }
                    step = "views after play";
                    Views(s);
                    step = "save again";
                    GameSession.Load(s.Save());
                    ok++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine($"FAILED {Path.GetFileName(file)} at {step}: {ex.GetType().Name}: {ex.Message}");
                    Console.WriteLine("   " + ex.StackTrace?.Split('\n').FirstOrDefault(l => l.Contains("OneMoreYear"))?.Trim());
                }
            }
        Console.WriteLine($"{ok} saves fine, {failed} failed.");
    }

    private static void Views(GameSession s)
    {
        if (s.GameOver) { s.Epilogue(); return; }
        if (s.NeedsSuccession) return;
        s.Describe(s.Player.Id);
        s.Family();
        s.FamilyTree();
        s.Money();
        s.Heirlooms();
        s.FamilyTraits();
        s.Actions(null);
        s.CurrentEvents();
        s.Chronicle(1);
        s.Epilogue();
    }
}
