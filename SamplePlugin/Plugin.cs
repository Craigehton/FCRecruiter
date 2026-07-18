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

    private const string CommandName = "/fcrecruit";

    private readonly WindowSystem windowSystem = new("FCRecruiter");
    private readonly RecruitingService recruitingService;
    private readonly RecruitingWindow recruitingWindow;

    public Plugin()
    {
        var recruitingConfig = new RecruitingConfig();

        recruitingService = new RecruitingService(
            PluginInterface.GetPluginConfigDirectory().FullName,
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

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleRecruitingWindow;

        Log.Information("FCRecruiter loaded.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleRecruitingWindow;

        CommandManager.RemoveHandler(CommandName);

        windowSystem.RemoveAllWindows();
    }

    private void OnCommand(string command, string arguments)
    {
        ToggleRecruitingWindow();
    }

    private void ToggleRecruitingWindow()
    {
        recruitingWindow.Toggle();
    }
}