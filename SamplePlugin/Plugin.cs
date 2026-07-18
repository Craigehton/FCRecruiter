using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

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
    private bool tracingAddons;

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

        var key = $\"{eventType}:{name}\";
        if (!tracedEvents.Add(key))
            return;

        Log.Information(
            "FCRecruiter addon event: {EventType} -> {AddonName}",
            eventType,
            name
        );

        if (!name.Equals("SocialList", StringComparison.Ordinal))
            return;

        if (args is AddonSetupArgs setupArgs)
            LogAtkValues("PostSetup", setupArgs.AtkValueEnumerable);
        else if (args is AddonRefreshArgs refreshArgs)
            LogAtkValues("PostRefresh", refreshArgs.AtkValueEnumerable);
    }

    private static void LogAtkValues(
        string source,
        IEnumerable<Dalamud.Game.NativeWrapper.AtkValuePtr> values)
    {
        var index = 0;
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
    }

    private void ToggleRecruitingWindow() => recruitingWindow.Toggle();
}
