locals {
  rg_name        = "${var.project_name}-${var.environment}-rg"
  appcfg_name    = "${var.project_name}-${var.environment}-appcfg"
  kv_name        = "${var.project_name}${var.environment}kv"
  plan_name      = "${var.project_name}-${var.environment}-plan"
  api_name       = "${var.project_name}-${var.environment}-api"
  ui_name        = "${var.project_name}-${var.environment}-ui"
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
}

resource "azurerm_key_vault" "kv" {
  name                        = local.kv_name
  location                    = var.location
  resource_group_name         = azurerm_resource_group.rg.name
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  sku_name                    = "standard"
  soft_delete_retention_days  = 7
  purge_protection_enabled    = true
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
  sku_name            = "P1v3"
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

  secret_permissions = ["Get", "List"]
}

resource "azurerm_key_vault_access_policy" "ui_kv" {
  key_vault_id = azurerm_key_vault.kv.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = azurerm_linux_web_app.ui.identity[0].principal_id

  secret_permissions = ["Get", "List"]
}
