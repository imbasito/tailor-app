using System.Diagnostics;

namespace STailor.Maui.Services;

internal static class LocalApiBootstrapper
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task EnsureLocalApiAvailableAsync(string apiBaseUrl, CancellationToken cancellationToken)
    {
        if (!TryGetLoopbackBaseUri(apiBaseUrl, out var baseUri))
        {
            return;
        }

        if (await IsApiHealthyAsync(baseUri, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await IsApiHealthyAsync(baseUri, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            TryStartLocalApiProcess(baseUri);

            for (var attempt = 0; attempt < 8; attempt++)
            {
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                if (await IsApiHealthyAsync(baseUri, cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    private static bool TryStartLocalApiProcess(Uri baseUri)
    {
        try
        {
            var apiLaunchTarget = ResolveApiLaunchTarget();
            if (apiLaunchTarget is null)
            {
                return false;
            }

            var startInfo = apiLaunchTarget.Value.IsExecutable
                ? new ProcessStartInfo
                {
                    FileName = apiLaunchTarget.Value.Path,
                    WorkingDirectory = apiLaunchTarget.Value.WorkingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
                : new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"\"{apiLaunchTarget.Value.Path}\"",
                    WorkingDirectory = apiLaunchTarget.Value.WorkingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

            startInfo.Environment["ASPNETCORE_URLS"] = baseUri.GetLeftPart(UriPartial.Authority);
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static (string Path, string WorkingDirectory, bool IsExecutable)? ResolveApiLaunchTarget()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            if (string.Equals(currentDirectory.Name, "modern", StringComparison.OrdinalIgnoreCase))
            {
                var apiProjectDirectory = Path.Combine(currentDirectory.FullName, "src", "STailor.Api");
                var executablePath = Path.Combine(apiProjectDirectory, "bin", "Debug", "net8.0", "STailor.Api.exe");
                if (File.Exists(executablePath))
                {
                    return (executablePath, Path.GetDirectoryName(executablePath)!, true);
                }

                var dllPath = Path.Combine(apiProjectDirectory, "bin", "Debug", "net8.0", "STailor.Api.dll");
                if (File.Exists(dllPath))
                {
                    return (dllPath, Path.GetDirectoryName(dllPath)!, false);
                }

                return null;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return null;
    }

    private static bool TryGetLoopbackBaseUri(string apiBaseUrl, out Uri baseUri)
    {
        baseUri = default!;
        if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(parsed.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        baseUri = parsed;
        return true;
    }

    private static async Task<bool> IsApiHealthyAsync(Uri baseUri, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(1.5),
            };

            using var response = await httpClient.GetAsync(new Uri(baseUri, "/health"), cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
