variable "resource_group_name" {
  type        = string
  default     = "rg-fintech-prod"
  description = "Name of the Azure Resource Group"
}

variable "location" {
  type        = string
  default     = "italynorth"
  description = "Azure region for deployment"
}

variable "app_name_prefix" {
  type        = string
  default     = "fintech"
  description = "Prefix for globally unique resource names"
}

variable "sql_admin_username" {
  type        = string
  default     = "sqladmin"
  description = "Azure SQL Server administrator username"
}

variable "sql_admin_password" {
  type        = string
  sensitive   = true
  description = "Azure SQL Server administrator password"
}


variable "jwt_secret_key" {
  type        = string
  sensitive   = true
  description = "Cryptographic signing key for JWT tokens (min 32 characters)"
}

variable "admin_seed_email" {
  type        = string
  default     = "admin@example.com"
  description = "Default administrative username for initial seeder"
}

variable "admin_seed_password" {
  type        = string
  sensitive   = true
  description = "Default administrative password for initial seeder"
}