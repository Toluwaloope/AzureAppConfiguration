terraform {
  required_version = ">= 1.6.0"
  backend "azurerm" {

    resource_group_name  = "toluwaloope-demo-tfstate-rg"
    storage_account_name = "toluwaloopetfstatestg"
    container_name       = "tfstate"
    key                  = "appconfig-demo.tfstate"
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
