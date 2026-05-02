using System.Collections.Generic;
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

    private const string CommandName = "/troute";

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

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Treasure Route window.",
        });

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

        CommandManager.RemoveHandler(CommandName);
    }

    public void ToggleMainUi() => mainWindow.Toggle();
    public void ToggleConfigUi() => configWindow.Toggle();

    private void OnCommand(string command, string args) => ToggleMainUi();

    private void OnMarkDetected(TreasureMark mark)
    {
        if (Configuration.DedupeNearbyMarks && MarkDeduper.IsDuplicate(Marks, mark, Configuration.DedupeRadius))
            return;

        Marks.Add(mark);
        mainWindow.NotifyMarksChanged();
    }
}
