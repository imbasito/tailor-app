using Microsoft.Extensions.Logging;
using STailor.Maui.Services;
using STailor.UI.Rcl.Services;

namespace STailor.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddScoped<LocalApiBootstrapHttpMessageHandler>();
		builder.Services.AddScoped(sp => new HttpClient(sp.GetRequiredService<LocalApiBootstrapHttpMessageHandler>(), disposeHandler: true));
		builder.Services.AddScoped<IExternalLinkLauncher, MauiExternalLinkLauncher>();
		builder.Services.AddSingleton<IWorkspaceSettingsStore, FileWorkspaceSettingsStore>();
		builder.Services.AddScoped<IBackupRestoreService, LocalBackupRestoreService>();
		builder.Services.AddScoped<IBackupRestoreDialogService, MauiBackupRestoreDialogService>();
		builder.Services.AddScoped<WorkspaceSettingsService>();
		builder.Services.AddScoped<WhatsAppMessageComposer>();
		builder.Services.AddScoped<OrderWizardSubmissionService>();
		builder.Services.AddScoped<OrderWorklistService>();
		builder.Services.AddScoped<OrderReminderWorklistService>();
		builder.Services.AddScoped<CustomerWorkspaceService>();
		builder.Services.AddScoped<ReportingServiceClient>();
		builder.Services.AddScoped<WhatsAppDeepLinkService>();
		builder.Services.AddScoped<LegacyMigrationSubmissionService>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
