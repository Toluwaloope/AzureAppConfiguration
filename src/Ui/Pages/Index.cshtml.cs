using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace AzureAppConfiguration.Ui.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public IndexModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public string ApiBaseUrl { get; private set; } = "";
        public string DashboardKind { get; private set; } = "Classic";
        public Dictionary<string, bool> AvailableFeatures { get; private set; } = new();

        public async Task OnGetAsync()
        {
            ApiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL")
                          ?? "http://localhost:5002"; // default for local dev

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                using var response = await httpClient.GetAsync($"{ApiBaseUrl}/api/dashboard-mode");
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var document = await JsonDocument.ParseAsync(stream);
                if (document.RootElement.TryGetProperty("dashboard", out var dashboardProperty))
                {
                    var dashboard = dashboardProperty.GetString();
                    if (dashboard is "New" or "Classic")
                    {
                        DashboardKind = dashboard;
                    }
                }

                using var featureResponse = await httpClient.GetAsync($"{ApiBaseUrl}/api/features");
                if (!featureResponse.IsSuccessStatusCode)
                {
                    return;
                }

                await using var featureStream = await featureResponse.Content.ReadAsStreamAsync();
                using var featureDocument = await JsonDocument.ParseAsync(featureStream);
                if (featureDocument.RootElement.TryGetProperty("features", out var featuresElement)
                    && featuresElement.ValueKind == JsonValueKind.Object)
                {
                    AvailableFeatures = featuresElement.EnumerateObject()
                        .ToDictionary(feature => feature.Name, feature => feature.Value.GetBoolean());
                }
            }
            catch
            {
                DashboardKind = "Classic";
            }
        }
    }
}
