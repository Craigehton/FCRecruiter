using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SamplePlugin;

public sealed record PlayerSearchResult(
    string CharacterName,
    string World,
    string FreeCompany
);

public interface IPlayerSearchSnapshotReader
{
    IReadOnlyList<PlayerSearchResult> Read(nint addonAddress);
}

/// <summary>
/// Patch-specific API 15 reader for the SocialList Player Search layout.
/// Fails closed when the expected component structure is unavailable.
/// </summary>
public sealed class SocialListSnapshotReader : IPlayerSearchSnapshotReader
{
    private const int ResultsComponentIndex = 8;
    private const uint CharacterNameNodeId = 8;
    private const uint FreeCompanyNodeId = 23;

    private readonly IPlayerState playerState;

    public SocialListSnapshotReader(IPlayerState playerState)
    {
        this.playerState = playerState;
    }

    public unsafe IReadOnlyList<PlayerSearchResult> Read(nint addonAddress)
    {
        var results = new List<PlayerSearchResult>();
        if (addonAddress == nint.Zero || !playerState.IsLoaded)
            return results;

        var world = playerState.HomeWorld.Value.Name.ToString();
        if (string.IsNullOrWhiteSpace(world))
            return results;

        var addon = (AtkUnitBase*)addonAddress;
        var rootManager = &addon->UldManager;

        if (rootManager->NodeListCount <= ResultsComponentIndex)
            return results;

        var resultsNode = rootManager->NodeList[ResultsComponentIndex];
        if (resultsNode == null || (ushort)resultsNode->Type < 1000)
            return results;

        var resultsComponent = resultsNode->GetAsAtkComponentNode();
        if (resultsComponent == null || resultsComponent->Component == null)
            return results;

        var rowsManager = &resultsComponent->Component->UldManager;
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var rowIndex = 0; rowIndex < rowsManager->NodeListCount; rowIndex++)
        {
            var rowNode = rowsManager->NodeList[rowIndex];
            if (rowNode == null || (ushort)rowNode->Type < 1000)
                continue;

            var rowComponent = rowNode->GetAsAtkComponentNode();
            if (rowComponent == null || rowComponent->Component == null)
                continue;

            var rowManager = &rowComponent->Component->UldManager;
            var name = ReadText(rowManager, CharacterNameNodeId);
            if (string.IsNullOrWhiteSpace(name) || !seenNames.Add(name))
                continue;

            var freeCompany = ReadText(rowManager, FreeCompanyNodeId);

            results.Add(new PlayerSearchResult(
                name.Trim(),
                world.Trim(),
                freeCompany.Trim()
            ));
        }

        return results;
    }

    private static unsafe string ReadText(AtkUldManager* manager, uint nodeId)
    {
        for (var index = 0; index < manager->NodeListCount; index++)
        {
            var node = manager->NodeList[index];
            if (node == null || node->NodeId != nodeId || node->Type != NodeType.Text)
                continue;

            return node->GetAsAtkTextNode()->NodeText.ToString();
        }

        return string.Empty;
    }
}

public sealed class PlayerSearchCapture : IDisposable
{
    public const string PlayerSearchResultsAddon = "SocialList";

    private readonly IAddonLifecycle lifecycle;
    private readonly IPlayerSearchSnapshotReader reader;
    private readonly RecruitingService recruiting;
    private readonly IPluginLog log;
    private DateTimeOffset lastCapture = DateTimeOffset.MinValue;

    public PlayerSearchCapture(
        IAddonLifecycle lifecycle,
        IPlayerSearchSnapshotReader reader,
        RecruitingService recruiting,
        IPluginLog log)
    {
        this.lifecycle = lifecycle;
        this.reader = reader;
        this.recruiting = recruiting;
        this.log = log;

        lifecycle.RegisterListener(
            AddonEvent.PostUpdate,
            PlayerSearchResultsAddon,
            OnResultsChanged
        );
    }

    private void OnResultsChanged(AddonEvent eventType, AddonArgs args)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - lastCapture < TimeSpan.FromMilliseconds(750))
            return;

        lastCapture = now;

        try
        {
            var rows = reader.Read(args.Addon.Address);
            var added = recruiting.AddCaptured(rows);
            if (added > 0)
                log.Information("Imported {Count} blank-FC Player Search rows", added);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "SocialList layout was not recognized; import skipped");
        }
    }

    public void Dispose()
    {
        lifecycle.UnregisterListener(OnResultsChanged);
    }
}
