# TreasureRoute

TreasureRoute is a Dalamud plugin that listens for shared treasure map links in party chat, collects them for the current session, and suggests an efficient per-map route starting from the closest aetheryte.

## Usage

- Open the main window with `/troute` or `/treasureroute`.
- Start or stop chat capture from the main window.
- Ask party members to post their treasure map links; detected marks appear in the list.
- Press **Recalculate** to build a suggested route, or enable auto-recalculate in settings.
- Marks are runtime-only and are cleared when the plugin unloads.

## Commands

- `/troute` — open or close the main window.
- `/troute help` — show command help in chat.
- `/troute start` / `/troute stop` — start or stop chat capture.
- `/troute clear` — clear collected session marks.
- `/troute recalc` — recalculate the current route.
- `/troute settings` — open the settings window.

## Settings

- Start listening on plugin load (default: off).
- Capture Alliance chat in addition to Party/Cross-party.
- Capture `/say` (default: off).
- Only capture messages that look treasure-map related (default: off; enable it if your party includes treasure keywords in map-link messages).
- Auto-recalculate when a new mark is captured.
- Drop duplicate or near-identical marks and configure the fallback radius.

## Privacy

TreasureRoute stores configuration only. Captured marks, including the sender name shown in the UI, live only in memory for the current plugin session and are not saved to the plugin configuration.

TreasureRoute does not communicate with any backend service.

## Build

Prerequisites:

- XIVLauncher/Dalamud installed and run at least once.
- .NET 10 SDK.
- Dalamud dev files in the default XIVLauncher dev path, or `DALAMUD_HOME` pointing to your Dalamud dev directory.

Build with:

```bash
dotnet build TreasureRoute.sln -c Debug
```

Release artifacts are produced under:

```text
TreasureRoute/bin/x64/Release/TreasureRoute/
```

## Development plugin path

In-game, open `/xlsettings` → **Experimental** and add the built plugin DLL as a Dev Plugin Location, for example:

```text
/home/sojiroh/Proyectos/TreasureRoute/TreasureRoute/bin/x64/Debug/TreasureRoute.dll
```

Then open `/xlplugins` → **Dev Tools** → **Installed Dev Plugins** and enable TreasureRoute.

## Custom repository publishing

TreasureRoute publishes its ZIP from this repository, but the Dalamud custom repository metadata is hosted in a separate repository:

```text
https://github.com/Sojiroh/SojirohPlugins
```

After a release has run, users can add this custom repository URL in Dalamud:

```text
https://sojiroh.github.io/SojirohPlugins/pluginmaster.json
```

In Dalamud, add it through `/xlsettings` → **Experimental** → **Custom Plugin Repositories**.

### Release flow

Before tagging, update `TreasureRoute/TreasureRoute.csproj` `<Version>` if needed. The tag must match that version exactly with a leading `v`. Then create and push a tag:

```bash
git tag v0.1.0.1
git push origin v0.1.0.1
```

The `Release TreasureRoute` workflow will:

1. build the solution,
2. run tests,
3. use the DalamudPackager ZIP,
4. create or update the GitHub Release,
5. check out `Sojiroh/SojirohPlugins`,
6. update `entries/TreasureRoute.json`,
7. regenerate `pluginmaster.json`,
8. commit and push the metadata update.

The workflow requires a `CUSTOM_REPO_TOKEN` repository secret with write access to `Sojiroh/SojirohPlugins`.

### Multiple plugins

To publish more plugins from the same custom repository URL, add one JSON file per plugin under `SojirohPlugins/entries/` with a unique `InternalName`, then regenerate the pluginmaster in that repository:

```bash
node scripts/generate-pluginmaster.mjs
```

Dalamud reads a single JSON array, so one `pluginmaster.json` can list TreasureRoute plus any future plugins.

## Links

- Source: <https://github.com/Sojiroh/TreasureRoute>
- Dalamud plugin development docs: <https://dalamud.dev/category/plugin-development/>
