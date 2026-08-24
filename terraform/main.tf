data "azurerm_client_config" "current" {}

# 1. Resource Group
resource "azurerm_resource_group" "rg" {
  name     = var.resource_group_name
  location = var.location
}

# 2. Azure Container Registry (ACR)
resource "azurerm_container_registry" "acr" {
  name                = "${var.app_name_prefix}acrprod01"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  sku                 = "Basic"
  admin_enabled       = true
}

# 3. Azure SQL Server
resource "azurerm_mssql_server" "sql_server" {
  name                         = "${var.app_name_prefix}-sqlserver-prod01"
  resource_group_name          = azurerm_resource_group.rg.name
  location                     = azurerm_resource_group.rg.location
  version                      = "12.0"
  administrator_login          = var.sql_admin_username
  administrator_login_password = var.sql_admin_password
  minimum_tls_version          = "1.2"
}

# 4. Azure SQL Database (Basic Tier for development/students)
resource "azurerm_mssql_database" "sql_db" {
  name      = "FintechDb"
  server_id = azurerm_mssql_server.sql_server.id
  collation            = "SQL_Latin1_General_CP1_CI_AS"
  sku_name  = "Basic"
  storage_account_type = "Local"

  lifecycle {
    prevent_destroy = false
  }
}

# 5. SQL Firewall Rule: Allow Azure Services (App Service)
resource "azurerm_mssql_firewall_rule" "allow_azure_services" {
  name             = "AllowAllWindowsAzureIps"
  server_id        = azurerm_mssql_server.sql_server.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# SQL Firewall: Allow Local Developer Machine
resource "azurerm_mssql_firewall_rule" "allow_client_ip" {
  name             = "AllowClientIP"
  server_id        = azurerm_mssql_server.sql_server.id
  start_ip_address = "197.232.247.107"
  end_ip_address   = "197.232.247.107"
}

# 5. Azure Key Vault
resource "azurerm_key_vault" "kv" {
  name                       = "kv-fintech-prod01"
  location                   = azurerm_resource_group.rg.location
  resource_group_name        = azurerm_resource_group.rg.name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 7
  purge_protection_enabled   = false
  enable_rbac_authorization  = true
}

# 6. RBAC Role: Grant Current User / Terraform Admin "Key Vault Secrets Officer"
resource "azurerm_role_assignment" "kv_admin" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

# 7. Key Vault Secrets (Double-dash notation maps to ASP.NET Core configuration paths)
resource "azurerm_key_vault_secret" "db_connection" {
  name         = "ConnectionStrings--DefaultConnection"
  value        = "Server=tcp:${azurerm_mssql_server.sql_server.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.sql_db.name};Persist Security Info=False;User ID=${var.sql_admin_username};Password=${var.sql_admin_password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  key_vault_id = azurerm_key_vault.kv.id

  depends_on = [azurerm_role_assignment.kv_admin]
}

resource "azurerm_key_vault_secret" "jwt_secret" {
  name         = "JwtSettings--Secret"
  value        = var.jwt_secret_key
  key_vault_id = azurerm_key_vault.kv.id

  depends_on = [azurerm_role_assignment.kv_admin]
}

resource "azurerm_key_vault_secret" "admin_email" {
  name         = "AdminConfig--DefaultEmail"
  value        = var.admin_seed_email
  key_vault_id = azurerm_key_vault.kv.id

  depends_on = [azurerm_role_assignment.kv_admin]
}

resource "azurerm_key_vault_secret" "admin_password" {
  name         = "AdminConfig--DefaultPassword"
  value        = var.admin_seed_password
  key_vault_id = azurerm_key_vault.kv.id

  depends_on = [azurerm_role_assignment.kv_admin]
}

# 6. Linux App Service Plan (Free/Basic tier: B1)
resource "azurerm_service_plan" "asp" {
  name                = "${var.app_name_prefix}-asp-prod"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  os_type             = "Linux"
  sku_name            = "B1"
}

# 7. Linux Web App for Containers (with System-Assigned Managed Identity)
resource "azurerm_linux_web_app" "app" {
  name                = "${var.app_name_prefix}-api-prod01"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  service_plan_id     = azurerm_service_plan.asp.id

  # This block creates the Managed Identity in Microsoft Entra ID
  identity {
    type = "SystemAssigned"
  }

  site_config {
    always_on = true
    application_stack {
      # docker_image_name   = "fintechbackend:latest"
      docker_image_name        = "${var.docker_image_name}:${var.docker_image_tag}"
      docker_registry_url = "https://${azurerm_container_registry.acr.login_server}"
      docker_registry_username = azurerm_container_registry.acr.admin_username
      docker_registry_password = azurerm_container_registry.acr.admin_password
    }
  }

  app_settings = {
    "WEBSITES_PORT"                 = "8080"
    "ASPNETCORE_ENVIRONMENT"        = "Production"
    "KEY_VAULT_NAME"         = azurerm_key_vault.kv.name
    # "ConnectionStrings__DefaultConnection" = "Server=tcp:${azurerm_mssql_server.sql_server.fully_qualified_domain_name},1433;Initial Catalog=FintechDb;Persist Security Info=False;User ID=${var.sql_admin_username};Password=${var.sql_admin_password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
    # "AdminConfig__DefaultEmail"           = var.admin_seed_email
    # "AdminConfig__DefaultPassword"           = var.admin_seed_password
    # "JwtSettings__Secret"                             = var.jwt_secret_key
    # "JwtSettings__Issuer"                          = "FintechBackend"
    # "JwtSettings__Audience"                        = "FintechFrontend"  
  }
}

# 10. RBAC Role: Grant Web App Managed Identity "Key Vault Secrets User"
resource "azurerm_role_assignment" "app_kv_secrets_user" {
  scope                = azurerm_key_vault.kv.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_web_app.app.identity[0].principal_id
}

