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
    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    internal static IPluginLog Log { get; private set; } = null!;

    [PluginService]
    internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;

    private const string CommandName = "/fcrecruit";
    private const string TraceCommandName = "/fctrace";

    private readonly WindowSystem windowSystem = new("FCRecruiter");
    private readonly RecruitingService recruitingService;
    private readonly RecruitingWindow recruitingWindow;
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

        CommandManager.AddHandler(
            CommandName,
            new CommandInfo(OnCommand)
            {
                HelpMessage = "Open the FC recruiting assistant."
            }
        );

        CommandManager.AddHandler(
            TraceCommandName,
            new CommandInfo(OnTraceCommand)
            {
                HelpMessage = "Toggle temporary addon refresh tracing."
            }
        );

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleRecruitingWindow;

        Log.Information("FCRecruiter loaded.");
    }

    public void Dispose()
    {
        if (tracingAddons)
            AddonLifecycle.UnregisterListener(OnAddonRefresh);

        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleRecruitingWindow;

        CommandManager.RemoveHandler(TraceCommandName);
        CommandManager.RemoveHandler(CommandName);

        windowSystem.RemoveAllWindows();
    }

    private void OnCommand(string command, string arguments)
    {
        ToggleRecruitingWindow();
    }

    private void OnTraceCommand(string command, string arguments)
    {
        tracingAddons = !tracingAddons;

        if (tracingAddons)
        {
            AddonLifecycle.RegisterListener(
                AddonEvent.PostRefresh,
                OnAddonRefresh
            );

            Log.Information("FCRecruiter addon tracing enabled.");
        }
        else
        {
            AddonLifecycle.UnregisterListener(OnAddonRefresh);
            Log.Information("FCRecruiter addon tracing disabled.");
        }
    }

    private void OnAddonRefresh(AddonEvent eventType, AddonArgs args)
    {
        Log.Information(
            "FCRecruiter refreshed addon: {AddonName}",
            args.AddonName
        );
    }

    private void ToggleRecruitingWindow()
    {
        recruitingWindow.Toggle();
    }
}
