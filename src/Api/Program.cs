using Azure.Identity;
using Microsoft.FeatureManagement;
using System.Security.Cryptography;
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

var app = builder.Build();
// Middleware triggers refresh if cache expired or sentinel changed
app.UseAzureAppConfiguration();

app.MapGet("/api/feature", async (IFeatureManagerSnapshot featureManager) =>
{
    var enabled = await featureManager.IsEnabledAsync("BetaFeature");
    return Results.Json(new { feature = "BetaFeature", enabled });
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
    return Results.Json(new { features = statuses });
}).WithName("GetFeaturesStatus");

app.MapGet("/api/dashboard-mode", async (IFeatureManagerSnapshot featureManager) =>
{
    var isNewDashboardEnabled = await featureManager.IsEnabledAsync("NewDashboard");
    return Results.Json(new
    {
        feature = "NewDashboard",
        enabled = isNewDashboardEnabled,
        dashboard = isNewDashboardEnabled ? "New" : "Classic"
    });
}).WithName("GetDashboardMode");

app.MapGet("/api/my-api-key-check", () =>
{
    var apiKey = Environment.GetEnvironmentVariable("MY_API_KEY") ?? string.Empty;
    var hasValue = !string.IsNullOrWhiteSpace(apiKey);
    return Results.Json(new
    {
        hasValue,
        length = apiKey.Length,
        preview = GetMaskedPreview(apiKey),
        fingerprint = GetFingerprint(apiKey)
    });
}).WithName("GetMyApiKeyCheck");

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();

static string GetMaskedPreview(string value)
{
    if (string.IsNullOrEmpty(value))
    {
        return "(empty)";
    }

    if (value.Length <= 8)
    {
        return "****";
    }

    return $"{value[..4]}...{value[^4..]}";
}

static string GetFingerprint(string value)
{
    if (string.IsNullOrEmpty(value))
    {
        return "(none)";
    }

    var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
    return Convert.ToHexString(hash)[..12];
}
