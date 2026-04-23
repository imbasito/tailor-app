using STailor.UI.Rcl.Services;

#if WINDOWS
using Windows.Storage.Pickers;
using WinRT.Interop;
#endif

namespace STailor.Maui.Services;

internal sealed class MauiBackupRestoreDialogService : IBackupRestoreDialogService
{
    public async Task<string?> PickBackupFolderAsync(string currentPath, CancellationToken cancellationToken = default)
    {
#if WINDOWS
        string? selectedPath = null;
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            picker.FileTypeFilter.Add("*");

            var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
                as Microsoft.UI.Xaml.Window;
            if (window is not null)
            {
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
            }

            var folder = await picker.PickSingleFolderAsync();
            selectedPath = folder?.Path;
        });

        return selectedPath;
#else
        await Task.CompletedTask;
        return null;
#endif
    }

    public async Task<string?> PickRestoreManifestAsync(string currentPath, CancellationToken cancellationToken = default)
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select backup-manifest.json",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.WinUI] = [".json"],
                [DevicePlatform.Android] = ["application/json"],
                [DevicePlatform.iOS] = ["public.json"],
                [DevicePlatform.MacCatalyst] = ["public.json"],
            }),
        });

        return result?.FullPath;
    }
}
