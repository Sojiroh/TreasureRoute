using System;
using Dalamud.Configuration;

namespace TreasureRoute;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool ListenOnStart { get; set; } = false;
    public bool ListenAlliance { get; set; } = true;
    public bool ListenSay { get; set; } = false;
    public bool CaptureOnlyTreasureContext { get; set; } = false;
    public bool AutoRecalculate { get; set; } = true;
    public bool DedupeNearbyMarks { get; set; } = true;
    public float DedupeRadius { get; set; } = 0.5f;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
