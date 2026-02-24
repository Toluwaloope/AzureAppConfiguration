using Azure.Identity;
using Microsoft.FeatureManagement;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Read App Configuration endpoint from environment (preferred) or config
var appConfigEndpoint = Environment.GetEnvironmentVariable("APP_CONFIG_ENDPOINT")
                      ?? builder.Configuration["AppConfiguration:Endpoint"];

if (!string.IsNullOrWhiteSpace(appConfigEndpoint))
{
    builder.Configuration.AddAzureAppConfiguration(options =>
    {
        options.Connect(new Uri(appConfigEndpoint), new DefaultAzureCredential())
               .ConfigureRefresh(refreshOptions =>
               {
                   // Sentinel key to trigger full refresh when updated
                   refreshOptions.Register("AppConfiguration:Sentinel", refreshAll: true);
                   // Cache the configuration for a short period to reduce calls
                   refreshOptions.SetCacheExpiration(TimeSpan.FromSeconds(30));
               })
               .UseFeatureFlags(ff =>
               {
                   // Cache feature flags briefly to reflect changes faster
                   ff.CacheExpirationInterval = TimeSpan.FromSeconds(5);
               });
    });
}

// Required to enable automatic refresh on requests when using Azure App Configuration
builder.Services.AddAzureAppConfiguration();
builder.Services.AddFeatureManagement();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
// Middleware triggers refresh if cache expired or sentinel changed
app.UseAzureAppConfiguration();

app.MapGet("/api/feature", async (IFeatureManagerSnapshot featureManager) =>
{
    var enabled = await featureManager.IsEnabledAsync("BetaFeature");
    var result = Results.Json(new { feature = "BetaFeature", enabled });
    // Optional client-side caching of the response for a short duration
    result.EnableBuffering();
    result.Headers["Cache-Control"] = "public, max-age=30";
    return result;
}).WithName("GetFeatureStatus");

app.MapGet("/api/features", async (IFeatureManagerSnapshot featureManager, IConfiguration config) =>
{
    // Read list of feature names from App Configuration (JSON array)
    var namesJson = config["AppConfiguration:Features"] ?? "[]";
    string[] featureNames;
    try
    {
        featureNames = JsonSerializer.Deserialize<string[]>(namesJson) ?? Array.Empty<string>();
    }
    catch
    {
        featureNames = Array.Empty<string>();
    }
    var statuses = new Dictionary<string, bool>();
    foreach (var name in featureNames)
        statuses[name] = await featureManager.IsEnabledAsync(name);
    var result = Results.Json(new { features = statuses });
    result.EnableBuffering();
    result.Headers["Cache-Control"] = "public, max-age=5";
    return result;
}).WithName("GetFeaturesStatus");

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();
