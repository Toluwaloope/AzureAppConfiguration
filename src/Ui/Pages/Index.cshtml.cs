using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AzureAppConfiguration.Ui.Pages
{
    public class IndexModel : PageModel
    {
        public string ApiBaseUrl { get; private set; } = "";

        public void OnGet()
        {
            ApiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL")
                          ?? "http://localhost:5002"; // default for local dev
        }
    }
}
