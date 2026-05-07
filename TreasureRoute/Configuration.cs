using System;
using Dalamud.Configuration;

namespace TreasureRoute;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public const int CurrentVersion = 2;
    public const float MinVisitRadius = 0.05f;
    public const float MaxVisitRadius = 1.0f;
    public const float DefaultVisitRadius = 0.3f;

    public int Version { get; set; } = CurrentVersion;

    public bool ListenOnStart { get; set; } = false;
    public bool ListenAlliance { get; set; } = true;
    public bool ListenSay { get; set; } = false;
    public bool CaptureOnlyTreasureContext { get; set; } = false;
    public bool AutoRecalculate { get; set; } = true;
    public bool DedupeNearbyMarks { get; set; } = true;
    public float DedupeRadius { get; set; } = 0.5f;
    public bool AutoRemoveVisitedMarks { get; set; } = false;
    public float VisitRadius { get; set; } = DefaultVisitRadius;

    public void Migrate()
    {
        if (Version >= CurrentVersion) return;

        if (Version < 2 && VisitRadius > MaxVisitRadius)
            VisitRadius = DefaultVisitRadius;

        Version = CurrentVersion;
        Save();
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
