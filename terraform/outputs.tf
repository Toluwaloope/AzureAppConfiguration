output "resource_group_name" {
  value       = azurerm_resource_group.rg.name
  description = "Resource group name"
}

output "api_app_name" {
  value       = azurerm_linux_web_app.api.name
  description = "API Web App name"
}

output "ui_app_name" {
  value       = azurerm_linux_web_app.ui.name
  description = "UI Web App name"
}

output "kv_name" {
  value       = azurerm_key_vault.kv.name
  description = "Key Vault name"
}
