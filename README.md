# One More Year

A generational life and family simulator.

> "Live a life. Build a family. Leave a legacy."

See [docs/kravspec.md](docs/kravspec.md) for the requirements specification and game concept (Swedish).

## Playing

Double-click **`Play.cmd`** in the repo root. It builds the game and starts it in Godot.

Alternatively, open `game/project.godot` in Godot 4.6 (.NET edition) and press **F5**.

### Controls

| Action | Mouse / keyboard | Controller |
|---|---|---|
| Navigate | Mouse, arrow keys, Tab | D-pad / left stick |
| Select | Click, Enter / Space | A |
| Next year | **N** | **Y** |
| Switch tab | **Q** / **E** | **LB** / **RB** |

The game autosaves every year. **Continue** on the title screen picks up where you left off.

## Structure

| Folder | What |
|---|---|
| `src/OneMoreYear.Simulation` | The whole simulation as a plain C# library with no engine dependency: people, relationships, memories, secrets, careers, economy, events, succession, saving. `GameSession` is the public API. |
| `content/` | Game data (JSON): traits, countries, occupations, events and player actions. Embedded into the simulation library at build time. |
| `game/` | The Godot 4.6 (C#) presentation layer. It only shows state and forwards choices. |
| `tests/` | xUnit tests: content validation, determinism, save/load, 150-year stability runs. |
| `tools/OneMoreYear.SimRunner` | Headless tool that plays games automatically and prints the family chronicle – used for balancing. |

## Development

```sh
dotnet build                     # everything
dotnet test                      # simulation tests
dotnet run --project tools/OneMoreYear.SimRunner -- 42 150 1950    # seed, years, start year
```

UI smoke test (plays through the real UI for 1,500 steps and quits):

```sh
Godot_v4.6-stable_mono_win64_console.exe --headless --path game -- --smoke
```

### Principles

- **Deterministic**: the same seed gives the same world. The RNG state lives in the save.
- **Data-driven**: new events, traits, occupations and countries are JSON, not code.
- **Separated**: simulation and presentation never mix, so the same game state can be shown on PC, Steam Deck and later other platforms.
- **Versioned saves**: `World.SaveVersion` – migrations go in `GameSession.Load`.
- **English first**: all game text is English; localization comes later.
