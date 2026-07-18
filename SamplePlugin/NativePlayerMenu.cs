using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace SamplePlugin;

/// <summary>
/// Patch-sensitive bridge that opens FFXIV's native player context menu for a
/// candidate still present in the live Player Search results. It never selects
/// a menu command or sends an invitation.
/// </summary>
public sealed class NativePlayerMenu
{
    private readonly IPlayerState playerState;
    private readonly IPluginLog log;

    public NativePlayerMenu(IPlayerState playerState, IPluginLog log)
    {
        this.playerState = playerState;
        this.log = log;
    }

    public unsafe bool TryOpen(Candidate candidate, out string message)
    {
        if (!playerState.IsLoaded)
        {
            message = "Log in before opening a player menu.";
            return false;
        }

        var infoModule = InfoModule.Instance();
        var agentModule = AgentModule.Instance();
        if (infoModule == null || agentModule == null)
        {
            message = "FFXIV's social modules are not available.";
            return false;
        }

        var search = (InfoProxySearch*)infoModule->GetInfoProxyById(InfoProxyId.PlayerSearch);
        if (search == null)
        {
            message = "Open Player Search and run a search first.";
            return false;
        }

        var homeWorldId = (ushort)playerState.HomeWorld.RowId;
        var entry = search->InfoProxyCommonList.GetEntryByName(candidate.CharacterName, homeWorldId);
        if (entry == null || entry->ContentId == 0)
        {
            message = "That player is no longer in the live Player Search results. Search again, then retry.";
            return false;
        }

        var context = (AgentContext*)agentModule->GetAgentByInternalId(AgentId.Context);
        if (context == null)
        {
            message = "FFXIV's player menu is not available.";
            return false;
        }

        try
        {
            context->ContextMenuTarget = *entry;
            context->CurrentContextMenuTarget = &context->ContextMenuTarget;
            context->TargetAccountId = entry->AccountId;
            context->TargetContentId = entry->ContentId;
            context->TargetHomeWorldId = (short)entry->HomeWorld;
            context->TargetName.SetString(candidate.CharacterName);
            context->OpenContextMenu(bindToOwner: false, closeExisting: true);

            message = "Native player menu opened. Choose Invite to Free Company manually.";
            return true;
        }
        catch (System.Exception ex)
        {
            log.Warning(ex, "Could not open native context menu for {Player}", candidate.Key);
            message = "The current game patch rejected the native player menu request.";
            return false;
        }
    }
}
