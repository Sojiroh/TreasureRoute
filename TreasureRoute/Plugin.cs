using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using TreasureRoute.Models;
using TreasureRoute.Services;
using TreasureRoute.Windows;

namespace TreasureRoute;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private static readonly string[] CommandNames = ["/troute", "/treasureroute"];

    public Configuration Configuration { get; }
    public List<TreasureMark> Marks { get; } = new();
    public WindowSystem WindowSystem { get; } = new("TreasureRoute");

    private readonly ChatListener chatListener;
    private readonly AetheryteRepository aetheryteRepository;
    private readonly RouteSolver routeSolver;
    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        aetheryteRepository = new AetheryteRepository(DataManager, Log);
        routeSolver = new RouteSolver(aetheryteRepository);
        chatListener = new ChatListener(ChatGui, Log, Configuration);
        chatListener.MarkDetected += OnMarkDetected;

        mainWindow = new MainWindow(this, chatListener, routeSolver);
        configWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(mainWindow);
        WindowSystem.AddWindow(configWindow);

        foreach (var commandName in CommandNames)
        {
            CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Treasure Route. Subcommands: help, start, stop, clear, recalc, settings.",
            });
        }

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        if (Configuration.ListenOnStart)
            chatListener.Start();

        Log.Information("TreasureRoute loaded.");
    }

    public void Dispose()
    {
        chatListener.MarkDetected -= OnMarkDetected;
        chatListener.Dispose();

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        WindowSystem.RemoveAllWindows();
        mainWindow.Dispose();
        configWindow.Dispose();

        foreach (var commandName in CommandNames)
            CommandManager.RemoveHandler(commandName);
    }

    public void ToggleMainUi() => mainWindow.Toggle();
    public void ToggleConfigUi() => configWindow.Toggle();

    private void OnCommand(string command, string args)
    {
        var parts = args.Split(' ', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
        var subcommand = parts.FirstOrDefault()?.ToLowerInvariant();

        switch (subcommand)
        {
            case null:
                ToggleMainUi();
                break;
            case "help":
            case "?":
                PrintHelp();
                break;
            case "start":
                chatListener.Start();
                ChatGui.Print("TreasureRoute: capture started.");
                break;
            case "stop":
                chatListener.Stop();
                ChatGui.Print("TreasureRoute: capture stopped.");
                break;
            case "clear":
                Marks.Clear();
                mainWindow.NotifyMarksChanged();
                ChatGui.Print("TreasureRoute: cleared collected marks.");
                break;
            case "recalc":
            case "recalculate":
                mainWindow.Recalculate();
                ChatGui.Print("TreasureRoute: route recalculated.");
                break;
            case "settings":
            case "config":
                ToggleConfigUi();
                break;
            default:
                ChatGui.PrintError($"TreasureRoute: unknown command '{subcommand}'. Use /troute help.");
                break;
        }
    }

    private static void PrintHelp()
    {
        ChatGui.Print("TreasureRoute commands:");
        ChatGui.Print("/troute - open or close the main window");
        ChatGui.Print("/troute start|stop - toggle chat capture");
        ChatGui.Print("/troute clear - clear collected session marks");
        ChatGui.Print("/troute recalc - recalculate the current route");
        ChatGui.Print("/troute settings - open settings");
    }

    private void OnMarkDetected(TreasureMark mark)
    {
        if (Configuration.DedupeNearbyMarks && MarkDeduper.IsDuplicate(Marks, mark, Configuration.DedupeRadius))
            return;

        Marks.Add(mark);
        mainWindow.NotifyMarksChanged();
    }
}
