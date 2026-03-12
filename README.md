# Azure App Configuration Demo

Sample .NET 8 solution with:
- `src/Api`: ASP.NET Core minimal API
- `src/Ui`: Razor Pages UI
- `terraform/`: Azure infrastructure (App Configuration, Key Vault, App Service plan, Linux Web Apps)
- `.github/workflows/deploy.yml`: infra + app deployment workflow

The API uses Azure App Configuration for feature flags and both API/UI consume `MY_API_KEY` through Key Vault references in App Service settings.

## Current Features

### 1) Dashboard mode toggle (`NewDashboard`)
- API endpoint `GET /api/dashboard-mode` checks feature flag `NewDashboard`.
- API returns `dashboard: "New"` or `"Classic"`.
- UI reads that value and renders one of two distinct dashboard layouts/styles.

### 2) `MY_API_KEY` verification (API + UI)
- API endpoint `GET /api/my-api-key-check` returns safe diagnostics only:
  - `hasValue`
  - `length`
  - masked `preview`
  - short SHA-256 `fingerprint`
- UI computes the same diagnostics for its own `MY_API_KEY` and compares fingerprints.
- UI page shows `Fingerprint match: True/False` for quick verification.

### 3) UI route support for `/index.html`
- Razor route convention maps `/index.html` to the `Index` page.

### 4) Sample PNG from Blob Storage (runtime config)
- Terraform creates a Storage Account and uploads `terraform/assets/sample.png` to a private blob container.
- Terraform saves these values in Azure App Configuration:
  - `Storage:ConnectionString`
  - `Storage:ContainerName`
  - `Storage:SampleBlobName`
- API endpoint `GET /api/sample-image` reads those keys from App Configuration at runtime and streams the PNG.

## API Endpoints
- `GET /api/health`
- `GET /api/feature`
- `GET /api/features`
- `GET /api/dashboard-mode`
- `GET /api/my-api-key-check`
- `GET /api/sample-image`

## Infrastructure Notes (Terraform)
- API App Service settings include:
  - `APP_CONFIG_ENDPOINT`
  - `MY_API_KEY` (Key Vault reference)
- App Configuration key-values now include storage settings used by the API at runtime:
  - `Storage:ConnectionString`
  - `Storage:ContainerName`
  - `Storage:SampleBlobName`
- UI App Service settings include:
  - `API_BASE_URL`
  - `MY_API_KEY` (Key Vault reference)

## CI/CD (GitHub Actions)
Workflow uses OIDC federated login (not `AZURE_CREDENTIALS`).

Required repository secrets:
- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

Required repository variables for Terraform backend:
- `TF_STATE_RESOURCE_GROUP`
- `TF_STATE_STORAGE_ACCOUNT`
- `TF_STATE_CONTAINER`
- `TF_STATE_KEY`

Run deployment from GitHub Actions with `workflow_dispatch` and environment input (`dev` or `prod`).

## Local Run

### API
```bash
dotnet run --project src/Api/AzureAppConfiguration.Api
```

### UI
```bash
dotnet run --project src/Ui/AzureAppConfiguration.Ui
```

Optional local environment variables:
- API: `APP_CONFIG_ENDPOINT`, `MY_API_KEY`
- UI: `API_BASE_URL`, `MY_API_KEY`
