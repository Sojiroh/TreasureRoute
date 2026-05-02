using System;
using System.Globalization;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using TreasureRoute.Models;

namespace TreasureRoute.Services;

public sealed class ChatListener : IDisposable
{
    private readonly IChatGui chatGui;
    private readonly IPluginLog log;
    private readonly Configuration configuration;

    public bool IsListening { get; private set; }
    public event Action<TreasureMark>? MarkDetected;

    public ChatListener(IChatGui chatGui, IPluginLog log, Configuration configuration)
    {
        this.chatGui = chatGui;
        this.log = log;
        this.configuration = configuration;
    }

    public void Start()
    {
        if (IsListening) return;
        chatGui.ChatMessage += OnChatMessage;
        IsListening = true;
    }

    public void Stop()
    {
        if (!IsListening) return;
        chatGui.ChatMessage -= OnChatMessage;
        IsListening = false;
    }

    public void Dispose() => Stop();

    private bool IsAcceptedChannel(XivChatType type) => type switch
    {
        XivChatType.Party => true,
        XivChatType.CrossParty => true,
        XivChatType.Alliance when configuration.ListenAlliance => true,
        XivChatType.Say when configuration.ListenSay => true,
        _ => false,
    };

    private void OnChatMessage(IHandleableChatMessage message)
    {
        try
        {
            if (!IsAcceptedChannel(message.LogKind)) return;
            if (configuration.CaptureOnlyTreasureContext && !HasTreasureContext(message.Message.TextValue)) return;

            var sender = message.Sender.TextValue;
            foreach (var payload in message.Message.Payloads)
            {
                if (payload is not MapLinkPayload mapLink) continue;

                var territory = mapLink.TerritoryType.ValueNullable;
                var map = mapLink.Map.ValueNullable;
                if (territory is null || map is null) continue;

                var mark = new TreasureMark(
                    territoryTypeId: territory.Value.RowId,
                    mapId: map.Value.RowId,
                    rawX: mapLink.RawX,
                    rawY: mapLink.RawY,
                    displayX: mapLink.XCoord,
                    displayY: mapLink.YCoord,
                    placeName: mapLink.PlaceName,
                    sender: sender,
                    postedAtUnix: DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                MarkDetected?.Invoke(mark);
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to process chat message for treasure marks.");
        }
    }

    public static bool HasTreasureContext(string text)
    {
        var value = text.ToLower(CultureInfo.InvariantCulture);
        return value.Contains("treasure")
               || value.Contains("timeworn")
               || value.Contains("map")
               || value.Contains("br'aaxskin")
               || value.Contains("loboskin")
               || value.Contains("gargantua")
               || value.Contains("ophiotauroskin")
               || value.Contains("kumbhiraskin")
               || value.Contains("gliderskin")
               || value.Contains("zonureskin")
               || value.Contains("gazelleskin")
               || value.Contains("wyvernskin")
               || value.Contains("dragonskin")
               || value.Contains("archaeoskin")
               || value.Contains("boarskin")
               || value.Contains("peisteskin");
    }
}
