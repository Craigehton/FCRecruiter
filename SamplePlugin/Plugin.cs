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
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;

    private const string CommandName = "/fcrecruit";

    private readonly WindowSystem windowSystem = new("FCRecruiter");
    private readonly RecruitingService recruitingService;
    private readonly RecruitingWindow recruitingWindow;
    private readonly PlayerSearchCapture playerSearchCapture;

    public Plugin()
    {
        var recruitingConfig = new RecruitingConfig();

        recruitingService = new RecruitingService(
            PluginInterface.GetPluginConfigDirectory(),
            recruitingConfig
        );

        recruitingWindow = new RecruitingWindow(recruitingService);
        windowSystem.AddWindow(recruitingWindow);

        var snapshotReader = new SocialListSnapshotReader(PlayerState);
        playerSearchCapture = new PlayerSearchCapture(
            AddonLifecycle,
            snapshotReader,
            recruitingService,
            Log
        );

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the FC recruiting assistant."
        });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleRecruitingWindow;
        Log.Information("FCRecruiter loaded with passive SocialList importing.");
    }

    public void Dispose()
    {
        playerSearchCapture.Dispose();
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
