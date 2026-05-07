using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using TreasureRoute.Models;

namespace TreasureRoute.Services;

public sealed class ProximityTracker : IDisposable
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;
    private readonly Configuration configuration;
    private readonly Func<List<TreasureMark>> getMarks;
    private readonly Dictionary<uint, Map> maps = new();

    public event Action<TreasureMark>? MarkVisited;

    public bool IsTracking { get; private set; }

    public ProximityTracker(
        IClientState clientState,
        IObjectTable objectTable,
        IFramework framework,
        IDataManager dataManager,
        IPluginLog log,
        Configuration configuration,
        Func<List<TreasureMark>> getMarks)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.framework = framework;
        this.dataManager = dataManager;
        this.log = log;
        this.configuration = configuration;
        this.getMarks = getMarks;
    }

    public void Start()
    {
        if (IsTracking) return;
        framework.Update += OnFrameworkUpdate;
        IsTracking = true;
        log.Debug("ProximityTracker started");
    }

    public void Stop()
    {
        if (!IsTracking) return;
        framework.Update -= OnFrameworkUpdate;
        IsTracking = false;
        log.Debug("ProximityTracker stopped");
    }

    public void Dispose() => Stop();

    public static bool IsWithinVisitRadius(Vector2 playerDisplay, TreasureMark mark, float radius)
    {
        var dx = playerDisplay.X - mark.DisplayX;
        var dy = playerDisplay.Y - mark.DisplayY;
        return dx * dx + dy * dy <= radius * radius;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!configuration.AutoRemoveVisitedMarks)
            return;

        var localPlayer = objectTable.LocalPlayer;
        if (localPlayer == null)
            return;

        var currentTerritory = clientState.TerritoryType;
        var playerPos = localPlayer.Position;
        var marks = getMarks();
        TreasureMark? toRemove = null;

        foreach (var mark in marks)
        {
            if (mark.TerritoryTypeId != currentTerritory)
                continue;
            if (!TryGetMap(mark.MapId, out var map))
                continue;

            var playerDisplay = MapUtil.WorldToMap(new Vector2(playerPos.X, playerPos.Z), map);
            var radius = Math.Max(Configuration.MinVisitRadius, configuration.VisitRadius);
            if (IsWithinVisitRadius(playerDisplay, mark, radius))
            {
                toRemove = mark;
                break;
            }
        }

        if (toRemove != null)
        {
            log.Information("Mark visited and removed: {PlaceName} {Coords}",
                toRemove.PlaceName, toRemove.CoordinateLabel);
            MarkVisited?.Invoke(toRemove);
        }
    }

    private bool TryGetMap(uint mapId, out Map map)
    {
        if (maps.TryGetValue(mapId, out map))
            return true;

        var mapSheet = dataManager.GetExcelSheet<Map>();
        var row = mapSheet?.GetRow(mapId);
        if (row is null)
            return false;

        map = row.Value;
        maps[mapId] = map;
        return true;
    }
}
