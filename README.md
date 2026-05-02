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

## Links

- Source: <https://github.com/Sojiroh/TreasureRoute>
- Dalamud plugin development docs: <https://dalamud.dev/category/plugin-development/>
