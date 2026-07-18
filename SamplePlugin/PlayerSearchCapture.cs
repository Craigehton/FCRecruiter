using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;

namespace SamplePlugin;

public sealed record PlayerSearchResult(string CharacterName, string World, string FreeCompany);

/// <summary>
/// Patch-specific adapter. Read only the rows currently displayed by Player Search.
/// Return an empty list if any expected node/type/count is missing.
/// </summary>
public interface IPlayerSearchSnapshotReader
{
    IReadOnlyList<PlayerSearchResult> Read(nint addonAddress);
}

public sealed class PlayerSearchCapture : IDisposable
{
    // Confirm this with /xldev after each game patch; do not guess silently.
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
        lifecycle.RegisterListener(AddonEvent.PostRefresh, PlayerSearchResultsAddon, OnResultsChanged);
        lifecycle.RegisterListener(AddonEvent.PostUpdate, PlayerSearchResultsAddon, OnResultsChanged);
    }

    private void OnResultsChanged(AddonEvent eventType, AddonArgs args)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - lastCapture < TimeSpan.FromMilliseconds(750)) return;
        lastCapture = now;

        try
        {
            var rows = reader.Read(args.Addon);
            var added = recruiting.AddCaptured(rows);
            if (added > 0) log.Information("Captured {Count} blank-FC Player Search rows", added);
        }
        catch (Exception ex)
        {
            // Fail closed: a patch/layout mismatch should collect nobody.
            log.Warning(ex, "Player Search layout was not recognized; capture skipped");
        }
    }

    public void Dispose() => lifecycle.UnregisterListener(OnResultsChanged);
}
