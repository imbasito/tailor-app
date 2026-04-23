namespace STailor.UI.Rcl.Services;

public interface IWorkspaceSettingsStore
{
    WorkspaceSettingsSnapshot Load();

    void Save(WorkspaceSettingsSnapshot snapshot);
}
