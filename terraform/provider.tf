terraform {
  required_version = ">= 1.6.0"
  backend "azurerm" {
    
    resource_group_name  = "<tfstate-resource-group>"
    storage_account_name = "<tfstatestorageaccount>"
    container_name       = "<tfstate-container>"
    key                  = "appconfig-demo-dev.tfstate"
  }
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.113"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}

provider "azurerm" {
  features {}
}
