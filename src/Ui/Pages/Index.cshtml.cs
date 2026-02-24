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
            }
            catch
            {
                DashboardKind = "Classic";
            }
        }
    }
}
