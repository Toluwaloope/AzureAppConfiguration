using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Cryptography;
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
        public bool UiKeyHasValue { get; private set; }
        public int UiKeyLength { get; private set; }
        public string UiKeyPreview { get; private set; } = "(empty)";
        public string UiKeyFingerprint { get; private set; } = "(none)";
        public bool ApiKeyHasValue { get; private set; }
        public int ApiKeyLength { get; private set; }
        public string ApiKeyPreview { get; private set; } = "(empty)";
        public string ApiKeyFingerprint { get; private set; } = "(none)";
        public bool KeyFingerprintMatch { get; private set; }

        public async Task OnGetAsync()
        {
            ApiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL")
                          ?? "http://localhost:5002"; // default for local dev

            var uiKey = Environment.GetEnvironmentVariable("MY_API_KEY") ?? string.Empty;
            UiKeyHasValue = !string.IsNullOrWhiteSpace(uiKey);
            UiKeyLength = uiKey.Length;
            UiKeyPreview = GetMaskedPreview(uiKey);
            UiKeyFingerprint = GetFingerprint(uiKey);

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

                using var keyResponse = await httpClient.GetAsync($"{ApiBaseUrl}/api/my-api-key-check");
                if (!keyResponse.IsSuccessStatusCode)
                {
                    return;
                }

                await using var keyStream = await keyResponse.Content.ReadAsStreamAsync();
                using var keyDocument = await JsonDocument.ParseAsync(keyStream);
                var root = keyDocument.RootElement;
                if (root.TryGetProperty("hasValue", out var hasValueProperty))
                {
                    ApiKeyHasValue = hasValueProperty.GetBoolean();
                }

                if (root.TryGetProperty("length", out var lengthProperty) && lengthProperty.TryGetInt32(out var apiLength))
                {
                    ApiKeyLength = apiLength;
                }

                if (root.TryGetProperty("preview", out var previewProperty))
                {
                    ApiKeyPreview = previewProperty.GetString() ?? "(empty)";
                }

                if (root.TryGetProperty("fingerprint", out var fingerprintProperty))
                {
                    ApiKeyFingerprint = fingerprintProperty.GetString() ?? "(none)";
                }

                KeyFingerprintMatch = UiKeyFingerprint != "(none)" && UiKeyFingerprint == ApiKeyFingerprint;
            }
            catch
            {
                DashboardKind = "Classic";
            }
        }

        private static string GetMaskedPreview(string value)
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

        private static string GetFingerprint(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "(none)";
            }

            var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hash)[..12];
        }
    }
}
