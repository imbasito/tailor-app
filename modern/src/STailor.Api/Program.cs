using STailor.Infrastructure.DependencyInjection;
using STailor.Infrastructure.Persistence;
using STailor.Modules.Core.DependencyInjection;
using STailor.Api.Sync;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLocalization();
builder.Services.Configure<SyncWorkerOptions>(builder.Configuration.GetSection("SyncWorker"));
builder.Services.AddHostedService<SyncQueueWorker>();

var localConnectionString =
    builder.Configuration.GetConnectionString("Local") ?? "Data Source=stailor-local.db";
var centralConnectionString =
    builder.Configuration.GetConnectionString("Central")
    ?? "Host=localhost;Port=5432;Database=stailor;Username=postgres;Password=postgres";

builder.Services.AddCoreModules();
builder.Services.AddInfrastructure(localConnectionString, centralConnectionString);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var localDbContext = scope.ServiceProvider.GetRequiredService<LocalTailorDbContext>();
    localDbContext.Database.EnsureCreated();

    if (app.Environment.IsDevelopment())
    {
        await DevelopmentSampleDataSeeder.SeedAsync(localDbContext);
    }
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();

public partial class Program
{
}
