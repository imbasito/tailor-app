using STailor.UI.Rcl.Services;
using STailor.Web.Components;
using STailor.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddLocalization();
builder.Services.AddScoped(_ => new HttpClient());
builder.Services.AddScoped<IExternalLinkLauncher, BrowserExternalLinkLauncher>();
builder.Services.AddSingleton<IWorkspaceSettingsStore, FileWorkspaceSettingsStore>();
builder.Services.AddScoped<IBackupRestoreService, LocalBackupRestoreService>();
builder.Services.AddScoped<IBackupRestoreDialogService, UnavailableBackupRestoreDialogService>();
builder.Services.AddScoped<WorkspaceSettingsService>();
builder.Services.AddScoped<WhatsAppMessageComposer>();
builder.Services.AddScoped<OrderWizardSubmissionService>();
builder.Services.AddScoped<OrderWorklistService>();
builder.Services.AddScoped<OrderReminderWorklistService>();
builder.Services.AddScoped<CustomerWorkspaceService>();
builder.Services.AddScoped<ReportingServiceClient>();
builder.Services.AddScoped<WhatsAppDeepLinkService>();
builder.Services.AddScoped<LegacyMigrationSubmissionService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(STailor.UI.Rcl.Pages.Dashboard).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
