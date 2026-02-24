variable "project_name" {
  type        = string
  default     = "appconfig-demo"
  description = "Project name for resource naming."
}

variable "location" {
  type        = string
  default     = "eastus"
  description = "Azure region."
}

variable "environment" {
  type        = string
  default     = "dev"
  description = "Environment name (dev/staging/prod)."
}
