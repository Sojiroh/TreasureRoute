using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using TreasureRoute.Models;
using TreasureRoute.Services;

namespace TreasureRoute.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private const int MaxAutoRecalculateMarks = 100;

    private readonly Plugin plugin;
    private readonly ChatListener chatListener;
    private readonly RouteSolver routeSolver;
    private RoutePlan? lastPlan;

    public MainWindow(Plugin plugin, ChatListener chatListener, RouteSolver routeSolver)
        : base("Treasure Route##TreasureRouteMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(450, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        this.plugin = plugin;
        this.chatListener = chatListener;
        this.routeSolver = routeSolver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawToolbar();
        ImGui.Separator();
        DrawMarksList();
        ImGui.Separator();
        DrawRoute();
    }

    private void DrawToolbar()
    {
        var listening = chatListener.IsListening;
        var listenColor = listening ? new Vector4(0.3f, 0.8f, 0.3f, 1f) : new Vector4(0.8f, 0.3f, 0.3f, 1f);
        ImGui.TextColored(listenColor, listening ? "Listening" : "Stopped");
        ImGui.SameLine();

        if (ImGui.Button(listening ? "Stop" : "Start"))
        {
            if (listening) chatListener.Stop();
            else chatListener.Start();
        }

        ImGui.SameLine();
        if (ImGui.Button("Recalculate"))
            Recalculate();

        ImGui.SameLine();
        if (ImGui.Button("Clear marks"))
        {
            plugin.Marks.Clear();
            lastPlan = null;
        }

        ImGui.SameLine();
        if (ImGui.Button("Settings"))
            plugin.ToggleConfigUi();

        ImGui.SameLine();
        ImGui.TextDisabled($"({plugin.Marks.Count} marks)");
    }

    private void DrawMarksList()
    {
        ImGui.Text("Detected treasure marks");

        using var child = ImRaii.Child("##marks", new Vector2(0, 130 * ImGuiHelpers.GlobalScale), true);
        if (!child.Success) return;

        if (plugin.Marks.Count == 0)
        {
            ImGui.TextDisabled("No marks yet — start listening and ask the party to share their map links.");
            return;
        }

        if (ImGui.BeginTable("##marks_table", 5,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Map");
            ImGui.TableSetupColumn("Coords");
            ImGui.TableSetupColumn("Posted by");
            ImGui.TableSetupColumn("##copy", ImGuiTableColumnFlags.WidthFixed, 45 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("##remove", ImGuiTableColumnFlags.WidthFixed, 50 * ImGuiHelpers.GlobalScale);
            ImGui.TableHeadersRow();

            TreasureMark? toRemove = null;
            TreasureMark? toCopy = null;
            for (var i = 0; i < plugin.Marks.Count; i++)
            {
                var mark = plugin.Marks[i];
                ImGui.PushID(i);
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextUnformatted(mark.PlaceName);
                ImGui.TableNextColumn(); ImGui.TextUnformatted(mark.CoordinateLabel);
                ImGui.TableNextColumn(); ImGui.TextUnformatted(mark.Sender);
                ImGui.TableNextColumn();
                if (ImGui.SmallButton("Copy"))
                    toCopy = mark;
                ImGui.TableNextColumn();
                if (ImGui.SmallButton("Remove"))
                    toRemove = mark;
                ImGui.PopID();
            }
            ImGui.EndTable();

            if (toRemove != null)
            {
                plugin.Marks.Remove(toRemove);
                NotifyMarksChanged();
            }

            if (toCopy != null)
            {
                plugin.CopyMarkToClipboard(toCopy);
            }
        }
    }

    private void DrawRoute()
    {
        ImGui.Text("Suggested route");
        using var child = ImRaii.Child("##route", Vector2.Zero, true);
        if (!child.Success) return;

        if (lastPlan == null || lastPlan.Maps.Count == 0)
        {
            ImGui.TextDisabled("Press Recalculate after marks have been collected.");
            return;
        }

        ImGui.TextDisabled($"Total in-map distance: {lastPlan.TotalDistance:0.0}");
        ImGui.Spacing();

        for (var mapIdx = 0; mapIdx < lastPlan.Maps.Count; mapIdx++)
        {
            var map = lastPlan.Maps[mapIdx];
            var header = $"{mapIdx + 1}. {map.PlaceName}  ({map.OrderedMarks.Count} marks, {map.Distance:0.0} u)";
            if (ImGui.CollapsingHeader(header, ImGuiTreeNodeFlags.DefaultOpen))
            {
                using (ImRaii.PushIndent(10f))
                {
                    if (map.AetheryteName != null)
                        ImGui.Text($"Start at aetheryte: {map.AetheryteName}  ( {map.AetheryteX:0.0} , {map.AetheryteY:0.0} )");
                    else
                        ImGui.TextColored(new Vector4(0.9f, 0.7f, 0.3f, 1f), "No aetheryte found for this territory.");

                    for (var i = 0; i < map.OrderedMarks.Count; i++)
                    {
                        var mark = map.OrderedMarks[i];
                        ImGui.Text($"  {i + 1}. {mark.CoordinateLabel}");
                        ImGui.SameLine();
                        ImGui.TextDisabled($"— {mark.Sender}");
                    }
                }
            }
        }
    }

    public void Recalculate()
    {
        lastPlan = routeSolver.BuildPlan(plugin.Marks);
    }

    public void NotifyMarksChanged()
    {
        lastPlan = null;
        if (plugin.Configuration.AutoRecalculate && plugin.Marks.Count <= MaxAutoRecalculateMarks)
            Recalculate();
    }
}
