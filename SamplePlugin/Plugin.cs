using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SamplePlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;

    private const string CommandName = "/fcrecruit";
    private const string TraceCommandName = "/fctrace";

    private readonly WindowSystem windowSystem = new("FCRecruiter");
    private readonly RecruitingService recruitingService;
    private readonly RecruitingWindow recruitingWindow;
    private readonly HashSet<string> tracedEvents = new(StringComparer.Ordinal);
    private readonly HashSet<string> tracedNodeTexts = new(StringComparer.Ordinal);
    private bool tracingAddons;
    private bool socialListValuesLogged;

    public Plugin()
    {
        var recruitingConfig = new RecruitingConfig();
        recruitingService = new RecruitingService(
            PluginInterface.GetPluginConfigDirectory(),
            recruitingConfig
        );

        recruitingWindow = new RecruitingWindow(recruitingService);
        windowSystem.AddWindow(recruitingWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the FC recruiting assistant."
        });

        CommandManager.AddHandler(TraceCommandName, new CommandInfo(OnTraceCommand)
        {
            HelpMessage = "Toggle temporary Player Search addon tracing."
        });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleRecruitingWindow;
        Log.Information("FCRecruiter loaded.");
    }

    public void Dispose()
    {
        AddonLifecycle.UnregisterListener(OnAddonEvent);
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleRecruitingWindow;
        CommandManager.RemoveHandler(TraceCommandName);
        CommandManager.RemoveHandler(CommandName);
        windowSystem.RemoveAllWindows();
    }

    private void OnCommand(string command, string arguments) => ToggleRecruitingWindow();

    private void OnTraceCommand(string command, string arguments)
    {
        tracingAddons = !tracingAddons;

        if (tracingAddons)
        {
            tracedEvents.Clear();
            tracedNodeTexts.Clear();
            socialListValuesLogged = false;
            AddonLifecycle.RegisterListener(AddonEvent.PostSetup, OnAddonEvent);
            AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, OnAddonEvent);
            AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, OnAddonEvent);
            Log.Information("FCRecruiter Player Search tracing enabled.");
        }
        else
        {
            AddonLifecycle.UnregisterListener(OnAddonEvent);
            Log.Information("FCRecruiter Player Search tracing disabled.");
        }
    }

    private void OnAddonEvent(AddonEvent eventType, AddonArgs args)
    {
        var name = args.AddonName;
        if (!name.Contains("Search", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("Social", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("Pc", StringComparison.OrdinalIgnoreCase))
            return;

        var key = $"{eventType}:{name}";
        if (tracedEvents.Add(key))
        {
            Log.Information(
                "FCRecruiter addon event: {EventType} -> {AddonName}",
                eventType,
                name
            );
        }

        if (!name.Equals("SocialList", StringComparison.Ordinal))
            return;

        if (eventType == AddonEvent.PostUpdate)
        {
            if (!socialListValuesLogged)
                socialListValuesLogged = LogAtkValues("PostUpdate", args.Addon.AtkValues);

            LogTextNodes(args.Addon.Address);
        }
        else if (args is AddonSetupArgs setupArgs)
            socialListValuesLogged |= LogAtkValues("PostSetup", setupArgs.AtkValueEnumerable);
        else if (args is AddonRefreshArgs refreshArgs)
            socialListValuesLogged |= LogAtkValues("PostRefresh", refreshArgs.AtkValueEnumerable);
    }

    private static bool LogAtkValues(
        string source,
        IEnumerable<Dalamud.Game.NativeWrapper.AtkValuePtr> values)
    {
        var index = 0;
        var foundText = false;
        foreach (var value in values)
        {
            object? boxed;
            try
            {
                boxed = value.GetValue();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Could not read SocialList AtkValue {Index}", index);
                index++;
                continue;
            }

            if (boxed is string text && !string.IsNullOrWhiteSpace(text))
            {
                foundText = true;
                Log.Information(
                    "FCRecruiter SocialList {Source} value[{Index}] ({Type}) = {Text}",
                    source,
                    index,
                    value.ValueType,
                    text
                );
            }

            index++;
        }

        return foundText;
    }

    private unsafe void LogTextNodes(nint addonAddress)
    {
        if (addonAddress == nint.Zero)
            return;

        var addon = (AtkUnitBase*)addonAddress;
        var visitedManagers = new HashSet<nint>();
        LogUldManager(&addon->UldManager, "root", 0, visitedManagers);
    }

    private unsafe void LogUldManager(
        AtkUldManager* manager,
        string path,
        int depth,
        HashSet<nint> visitedManagers)
    {
        if (manager == null || depth > 8)
            return;

        if (!visitedManagers.Add((nint)manager))
            return;

        for (var index = 0; index < manager->NodeListCount; index++)
        {
            var node = manager->NodeList[index];
            if (node == null)
                continue;

            var nodePath = $"{path}/{index}";

            if (node->Type == NodeType.Text)
            {
                var textNode = node->GetAsAtkTextNode();
                var text = textNode->NodeText.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var key = $"{(nint)node:X}:{text}";
                    if (tracedNodeTexts.Add(key))
                    {
                        Log.Information(
                            "FCRecruiter SocialList path={Path} id={NodeId} text={Text}",
                            nodePath,
                            node->NodeId,
                            text
                        );
                    }
                }
            }

            if ((ushort)node->Type < 1000)
                continue;

            var componentNode = node->GetAsAtkComponentNode();
            if (componentNode == null || componentNode->Component == null)
                continue;

            LogUldManager(
                &componentNode->Component->UldManager,
                nodePath,
                depth + 1,
                visitedManagers
            );
        }
    }

    private void ToggleRecruitingWindow() => recruitingWindow.Toggle();
}
