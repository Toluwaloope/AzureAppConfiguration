locals {
  rg_name     = "${var.project_name}-${var.environment}-rg"
  appcfg_name = "${var.project_name}-${var.environment}-appcfg"
  kv_name     = "${var.project_name}${var.environment}kv"
  plan_name   = "${var.project_name}-${var.environment}-plan"
  api_name    = "${var.project_name}-${var.environment}-api"
  ui_name     = "${var.project_name}-${var.environment}-ui"
}

resource "random_string" "storage_suffix" {
  length  = 6
  special = false
  upper   = false
}

resource "azurerm_resource_group" "rg" {
  name     = local.rg_name
  location = var.location
}

resource "azurerm_app_configuration" "appcfg" {
  name                = local.appcfg_name
  location            = var.location
  resource_group_name = azurerm_resource_group.rg.name
  sku                 = "standard"
}

resource "azurerm_storage_account" "files" {
  name                     = substr(lower(replace("${var.project_name}${var.environment}st${random_string.storage_suffix.result}", "-", "")), 0, 24)
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = var.location
  account_tier             = "Standard"
  account_replication_type = "LRS"

  allow_nested_items_to_be_public = false
  min_tls_version                 = "TLS1_2"
}

resource "azurerm_storage_container" "samples" {
  name                  = "samples"
  storage_account_name  = azurerm_storage_account.files.name
  container_access_type = "private"
}

resource "azurerm_storage_blob" "sample_png" {
  name                   = "sample.png"
  storage_account_name   = azurerm_storage_account.files.name
  storage_container_name = azurerm_storage_container.samples.name
  type                   = "Block"
  source                 = "${path.module}/assets/sample.png"
  content_type           = "image/png"
}

resource "azurerm_app_configuration_key" "storage_connection_string" {
  configuration_store_id = azurerm_app_configuration.appcfg.id
  key                    = "Storage:ConnectionString"
  value                  = azurerm_storage_account.files.primary_connection_string

  depends_on = [azurerm_storage_account.files]
}

resource "azurerm_app_configuration_key" "storage_container_name" {
  configuration_store_id = azurerm_app_configuration.appcfg.id
  key                    = "Storage:ContainerName"
  value                  = azurerm_storage_container.samples.name
}

resource "azurerm_app_configuration_key" "storage_sample_blob_name" {
  configuration_store_id = azurerm_app_configuration.appcfg.id
  key                    = "Storage:SampleBlobName"
  value                  = azurerm_storage_blob.sample_png.name
}

/* 
# Create a sample feature flag: BetaFeature enabled
resource "azurerm_app_configuration_feature" "beta" {
  configuration_store_id = azurerm_app_configuration.appcfg.id
  name                    = "BetaFeature"
  enabled                 = true
}

# Additional feature flags
resource "azurerm_app_configuration_feature" "darkmode" {
  configuration_store_id = azurerm_app_configuration.appcfg.id
  name                    = "DarkMode"
  enabled                 = true
}

resource "azurerm_app_configuration_feature" "newdashboard" {
  configuration_store_id = azurerm_app_configuration.appcfg.id
  name                    = "NewDashboard"
  enabled                 = false
}

resource "azurerm_app_configuration_feature" "enhancedlogging" {
  configuration_store_id = azurerm_app_configuration.appcfg.id
  name                    = "EnhancedLogging"
  enabled                 = true
}

resource "azurerm_app_configuration_feature" "experimentalcheckout" {
  configuration_store_id = azurerm_app_configuration.appcfg.id
  name                    = "ExperimentalCheckout"
  enabled                 = false
}

# Sentinel key for config refresh
resource "azurerm_app_configuration_key" "sentinel" {
  configuration_store_id = azurerm_app_configuration.appcfg.id
  key                     = "AppConfiguration:Sentinel"
  value                   = "initial"
}

# Feature list stored in configuration (JSON array of names)
resource "azurerm_app_configuration_key" "feature_list" {
  configuration_store_id = azurerm_app_configuration.appcfg.id
  key                     = "AppConfiguration:Features"
  value                   = jsonencode([
    "BetaFeature",
    "DarkMode",
    "NewDashboard",
    "EnhancedLogging",
    "ExperimentalCheckout"
  ])
  label = var.environment
} */

resource "azurerm_key_vault" "kv" {
  name                       = local.kv_name
  location                   = var.location
  resource_group_name        = azurerm_resource_group.rg.name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 7
  purge_protection_enabled   = true

  network_acls {
    bypass         = "AzureServices"
    default_action = "Allow"
  }

  access_policy {
    tenant_id               = data.azurerm_client_config.current.tenant_id
    object_id               = data.azurerm_client_config.current.object_id
    key_permissions         = ["Get", "List","Set", "Delete"]
    secret_permissions      = ["Get", "List", "Set", "Delete"]
    certificate_permissions = ["Get", "List"]
  }
}

data "azurerm_client_config" "current" {}

resource "random_password" "api_key" {
  length  = 24
  special = true
}

resource "azurerm_key_vault_secret" "api_key" {
  name         = "my-api-key"
  value        = random_password.api_key.result
  key_vault_id = azurerm_key_vault.kv.id
}

resource "azurerm_service_plan" "plan" {
  name                = local.plan_name
  location            = var.location
  resource_group_name = azurerm_resource_group.rg.name
  os_type             = "Linux"
  sku_name            = "B1"
}

resource "azurerm_linux_web_app" "api" {
  name                = local.api_name
  location            = var.location
  resource_group_name = azurerm_resource_group.rg.name
  service_plan_id     = azurerm_service_plan.plan.id

  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_stack {
      dotnet_version = "8.0"
    }
    health_check_path = "/api/health"
  }

  app_settings = {
    # UI will use this base URL; also the API uses APP_CONFIG_ENDPOINT for App Configuration RBAC access
    "APP_CONFIG_ENDPOINT" = azurerm_app_configuration.appcfg.endpoint
    "MY_API_KEY"          = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.api_key.id})"
  }
}

resource "azurerm_linux_web_app" "ui" {
  name                = local.ui_name
  location            = var.location
  resource_group_name = azurerm_resource_group.rg.name
  service_plan_id     = azurerm_service_plan.plan.id

  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_stack {
      dotnet_version = "8.0"
    }
  }

  app_settings = {
    "API_BASE_URL" = "https://${azurerm_linux_web_app.api.default_hostname}"
    "MY_API_KEY"   = azurerm_key_vault_secret.api_key.value
  }
}

# Grant API web app managed identity App Configuration Data Reader role
resource "azurerm_role_assignment" "api_appcfg_reader" {
  scope                = azurerm_app_configuration.appcfg.id
  role_definition_name = "App Configuration Data Reader"
  principal_id         = azurerm_linux_web_app.api.identity[0].principal_id
}

# Grant both web apps access to Key Vault secrets via access policies
resource "azurerm_key_vault_access_policy" "api_kv" {
  key_vault_id = azurerm_key_vault.kv.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = azurerm_linux_web_app.api.identity[0].principal_id

  secret_permissions = ["Get", "List", "Set", "Delete"]
  depends_on = [ azurerm_linux_web_app.api ]
}

resource "azurerm_key_vault_access_policy" "ui_kv" {
  key_vault_id = azurerm_key_vault.kv.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = azurerm_linux_web_app.ui.identity[0].principal_id

  secret_permissions = ["Get", "List", "Set", "Delete"]

  depends_on = [ azurerm_linux_web_app.ui ]
}
