using STailor.Maui.Services;
using STailor.UI.Rcl.Services;

namespace STailor.Maui;

public partial class App : Application
{
    public App(IWorkspaceSettingsStore workspaceSettingsStore)
    {
        InitializeComponent();

        var workspaceSettings = new WorkspaceSettingsService(workspaceSettingsStore);
        _ = Task.Run(() => LocalApiBootstrapper.EnsureLocalApiAvailableAsync(
            workspaceSettings.ApiBaseUrl,
            CancellationToken.None));

        MainPage = new MainPage();
    }
}
