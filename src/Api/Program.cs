using Azure.Identity;
using Azure.Storage.Blobs;
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
builder.Services.Configure<NewSettings>(builder.Configuration.GetSection("Appsettings"));

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
    // Supports both:
    // 1) plain string value: AppConfiguration:Features = "[\"A\",\"B\"]"
    // 2) JSON content type value flattened as section children: AppConfiguration:Features:0, :1, ...
    var featureSection = config.GetSection("AppConfiguration:Features");
    var featureNames = featureSection
        .GetChildren()
        .Select(child => child.Value)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Cast<string>()
        .ToArray();

    if (featureNames.Length == 0)
    {
        var namesJson = config["AppConfiguration:Features"] ?? "[]";
        try
        {
            featureNames = JsonSerializer.Deserialize<string[]>(namesJson) ?? Array.Empty<string>();
        }
        catch
        {
            featureNames = Array.Empty<string>();
        }
    }

    featureNames = featureNames
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

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

app.MapGet("/api/sample-image", async (IConfiguration config) =>
{
    var connectionString = config["Storage:ConnectionString"];
    var containerName = config["Storage:ContainerName"];
    var blobName = config["Storage:SampleBlobName"];

    if (string.IsNullOrWhiteSpace(connectionString) ||
        string.IsNullOrWhiteSpace(containerName) ||
        string.IsNullOrWhiteSpace(blobName))
    {
        return Results.Problem(
            title: "Storage configuration missing",
            detail: "One or more storage settings are not available from Azure App Configuration.",
            statusCode: StatusCodes.Status500InternalServerError);
    }

    try
    {
        var containerClient = new BlobContainerClient(connectionString, containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync())
        {
            return Results.NotFound(new { message = "Sample image blob was not found." });
        }

        var response = await blobClient.DownloadStreamingAsync();
        var contentType = response.Value.Details.ContentType;
        return Results.Stream(response.Value.Content, contentType ?? "image/png");
    }
    catch
    {
        return Results.Problem(
            title: "Blob fetch failed",
            detail: "The API failed to fetch the sample image from Azure Blob Storage.",
            statusCode: StatusCodes.Status502BadGateway);
    }
}).WithName("GetSampleImage");

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
