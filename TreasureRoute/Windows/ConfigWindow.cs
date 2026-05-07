using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace TreasureRoute.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin)
        : base("Treasure Route — Settings###TreasureRouteConfig")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize;
        Size = new Vector2(360, 0);
        SizeCondition = ImGuiCond.FirstUseEver;
        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var listenStart = configuration.ListenOnStart;
        if (ImGui.Checkbox("Start listening on plugin load", ref listenStart))
        {
            configuration.ListenOnStart = listenStart;
            configuration.Save();
        }

        var alliance = configuration.ListenAlliance;
        if (ImGui.Checkbox("Also capture from Alliance chat", ref alliance))
        {
            configuration.ListenAlliance = alliance;
            configuration.Save();
        }

        var say = configuration.ListenSay;
        if (ImGui.Checkbox("Also capture from /say (off by default)", ref say))
        {
            configuration.ListenSay = say;
            configuration.Save();
        }

        var treasureOnly = configuration.CaptureOnlyTreasureContext;
        if (ImGui.Checkbox("Only capture treasure-map chat context", ref treasureOnly))
        {
            configuration.CaptureOnlyTreasureContext = treasureOnly;
            configuration.Save();
        }

        var auto = configuration.AutoRecalculate;
        if (ImGui.Checkbox("Auto-recalculate route on new mark", ref auto))
        {
            configuration.AutoRecalculate = auto;
            configuration.Save();
        }

        ImGui.Separator();

        var dedupe = configuration.DedupeNearbyMarks;
        if (ImGui.Checkbox("Drop duplicate / near-identical marks", ref dedupe))
        {
            configuration.DedupeNearbyMarks = dedupe;
            configuration.Save();
        }

        if (configuration.DedupeNearbyMarks)
        {
            var radius = configuration.DedupeRadius;
            if (ImGui.SliderFloat("Dedup radius (map units)", ref radius, 0.1f, 5f, "%.1f"))
            {
                configuration.DedupeRadius = radius;
                configuration.Save();
            }
        }

        ImGui.Separator();

        var autoRemove = configuration.AutoRemoveVisitedMarks;
        if (ImGui.Checkbox("Auto-remove marks when visited", ref autoRemove))
        {
            configuration.AutoRemoveVisitedMarks = autoRemove;
            configuration.Save();
        }

        if (configuration.AutoRemoveVisitedMarks)
        {
            var visitRadius = configuration.VisitRadius;
            if (ImGui.SliderFloat("Visit detection radius (map units)", ref visitRadius, 0.1f, Configuration.MaxVisitRadius, "%.1f"))
            {
                configuration.VisitRadius = visitRadius;
                configuration.Save();
            }
        }
    }
}
