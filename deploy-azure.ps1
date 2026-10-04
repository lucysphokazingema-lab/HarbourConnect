###############################################################################
#  HarbourConnect – Azure Deployment Script
#  ASP.NET MVC 5 / .NET Framework 4.7.2
#
#  PREREQUISITES (do these once before running):
#    1. Install Azure CLI  →  https://aka.ms/installazurecliwindows
#    2. Run: az login
#    3. Fill in the variables in the CONFIG section below.
#
#  WHAT THIS SCRIPT DOES:
#    1. Creates a Resource Group
#    2. Creates an Azure SQL Server + Database
#    3. Creates an App Service Plan + Web App (Windows, .NET 4.8)
#    4. Configures the connection string + all app settings
#    5. Opens the SQL Server firewall to Azure services
#    6. Deploys the pre-built ZIP package
###############################################################################

# ─────────────────────────────────────────────────────────────────────────────
# CONFIG  –  Edit these values before running
# ─────────────────────────────────────────────────────────────────────────────

# Azure location (run `az account list-locations -o table` to see all options)
$LOCATION          = "southafricanorth"   # Johannesburg – closest to ZA

# Resource naming (must be globally unique for SQL server and Web App)
$RESOURCE_GROUP    = "harbourconnect-rg"
$SQL_SERVER_NAME   = "harbourconnect-sql"          # must be globally unique
$SQL_DB_NAME       = "HarbourConnectDB"
$SQL_ADMIN_USER    = "hcadmin"
$SQL_ADMIN_PASS    = "Change_Me_123!"              # change this!
$APP_SERVICE_PLAN  = "harbourconnect-plan"
$WEB_APP_NAME      = "harbourconnect-app"          # becomes <name>.azurewebsites.net

# App settings – API keys and email config
$OPENWEATHER_KEY   = ""                            # https://openweathermap.org/api
$GEMINI_KEY        = ""                            # https://aistudio.google.com/app/apikey
$EMAIL_MODE        = ""                            # "Graph", "Smtp", or "" to disable
$EMAIL_FROM        = ""                            # e.g. noreply@yourdomain.co.za
$SITE_BASE_URL     = "https://$WEB_APP_NAME.azurewebsites.net"

# Microsoft Graph (only needed if EMAIL_MODE = "Graph")
$GRAPH_TENANT_ID   = ""
$GRAPH_CLIENT_ID   = ""
$GRAPH_CLIENT_SECRET = ""

# SMTP (only needed if EMAIL_MODE = "Smtp")
$SMTP_USER         = ""
$SMTP_PASSWORD     = ""

# Initial TNPA admin account – created on first startup, then remove these settings
$INITIAL_ADMIN_EMAIL    = "admin@yourorganisation.co.za"    # change this!
$INITIAL_ADMIN_PASSWORD = "Admin@SecurePass1!"              # change this!

# Path to the deployment ZIP (relative to this script)
$ZIP_PATH = Join-Path $PSScriptRoot "apdp_deploy.zip"

# ─────────────────────────────────────────────────────────────────────────────
# VALIDATION
# ─────────────────────────────────────────────────────────────────────────────

Write-Host "`n=== HarbourConnect Azure Deployment ===" -ForegroundColor Cyan

if (-not (Test-Path $ZIP_PATH)) {
    Write-Error "Deploy ZIP not found at: $ZIP_PATH"
    Write-Error "Make sure apdp_deploy.zip is in the same folder as this script."
    exit 1
}

# Check az CLI is installed
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI not found. Install it from: https://aka.ms/installazurecliwindows"
    exit 1
}

# Check logged in
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "Not logged in to Azure. Running 'az login'..." -ForegroundColor Yellow
    az login
}
Write-Host "Logged in as: $($account.user.name) | Subscription: $($account.name)" -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# STEP 1 – Resource Group
# ─────────────────────────────────────────────────────────────────────────────

Write-Host "`n[1/6] Creating resource group '$RESOURCE_GROUP'..." -ForegroundColor Cyan
az group create --name $RESOURCE_GROUP --location $LOCATION --output none
Write-Host "      Done." -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# STEP 2 – Azure SQL Server + Database
# ─────────────────────────────────────────────────────────────────────────────

Write-Host "`n[2/6] Creating SQL Server '$SQL_SERVER_NAME'..." -ForegroundColor Cyan
az sql server create `
    --resource-group $RESOURCE_GROUP `
    --name $SQL_SERVER_NAME `
    --location $LOCATION `
    --admin-user $SQL_ADMIN_USER `
    --admin-password $SQL_ADMIN_PASS `
    --output none

Write-Host "      Creating database '$SQL_DB_NAME' (Basic tier)..." -ForegroundColor Cyan
az sql db create `
    --resource-group $RESOURCE_GROUP `
    --server $SQL_SERVER_NAME `
    --name $SQL_DB_NAME `
    --edition Basic `
    --output none

Write-Host "      Opening firewall for Azure services..." -ForegroundColor Cyan
az sql server firewall-rule create `
    --resource-group $RESOURCE_GROUP `
    --server $SQL_SERVER_NAME `
    --name "AllowAzureServices" `
    --start-ip-address 0.0.0.0 `
    --end-ip-address 0.0.0.0 `
    --output none

Write-Host "      Done." -ForegroundColor Green

# Build the connection string
$CONNECTION_STRING = "Server=tcp:$SQL_SERVER_NAME.database.windows.net,1433;Initial Catalog=$SQL_DB_NAME;Persist Security Info=False;User ID=$SQL_ADMIN_USER;Password=$SQL_ADMIN_PASS;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

# ─────────────────────────────────────────────────────────────────────────────
# STEP 3 – App Service Plan + Web App
# ─────────────────────────────────────────────────────────────────────────────

Write-Host "`n[3/6] Creating App Service Plan '$APP_SERVICE_PLAN' (B1)..." -ForegroundColor Cyan
az appservice plan create `
    --resource-group $RESOURCE_GROUP `
    --name $APP_SERVICE_PLAN `
    --location $LOCATION `
    --sku B1 `
    --is-windows `
    --output none

Write-Host "      Creating Web App '$WEB_APP_NAME'..." -ForegroundColor Cyan
az webapp create `
    --resource-group $RESOURCE_GROUP `
    --plan $APP_SERVICE_PLAN `
    --name $WEB_APP_NAME `
    --runtime "ASPNET:V4.8" `
    --output none

Write-Host "      Done." -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# STEP 4 – Connection String + App Settings
# ─────────────────────────────────────────────────────────────────────────────

Write-Host "`n[4/6] Configuring connection string and app settings..." -ForegroundColor Cyan

# Connection string
az webapp config connection-string set `
    --resource-group $RESOURCE_GROUP `
    --name $WEB_APP_NAME `
    --connection-string-type SQLAzure `
    --settings "HarbourConnectDB=$CONNECTION_STRING" `
    --output none

# App settings
az webapp config appsettings set `
    --resource-group $RESOURCE_GROUP `
    --name $WEB_APP_NAME `
    --output none `
    --settings `
        "OpenWeatherMapApiKey=$OPENWEATHER_KEY" `
        "OpenWeatherMapBaseUrl=https://api.openweathermap.org/data/2.5/weather" `
        "GeminiApiKey=$GEMINI_KEY" `
        "SeedDemoData=false" `
        "EmailMode=$EMAIL_MODE" `
        "EmailFrom=$EMAIL_FROM" `
        "EmailFromName=HarbourConnect" `
        "SiteBaseUrl=$SITE_BASE_URL" `
        "GraphTenantId=$GRAPH_TENANT_ID" `
        "GraphClientId=$GRAPH_CLIENT_ID" `
        "GraphClientSecret=$GRAPH_CLIENT_SECRET" `
        "SmtpHost=smtp.office365.com" `
        "SmtpPort=587" `
        "SmtpUser=$SMTP_USER" `
        "SmtpPassword=$SMTP_PASSWORD" `
        "InitialAdminEmail=$INITIAL_ADMIN_EMAIL" `
        "InitialAdminPassword=$INITIAL_ADMIN_PASSWORD"

Write-Host "      Done." -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# STEP 5 – Always-On + 64-bit (required for ASP.NET MVC on B1+)
# ─────────────────────────────────────────────────────────────────────────────

Write-Host "`n[5/6] Configuring runtime settings..." -ForegroundColor Cyan
az webapp config set `
    --resource-group $RESOURCE_GROUP `
    --name $WEB_APP_NAME `
    --always-on true `
    --use-32bit-worker-process false `
    --net-framework-version v4.0 `
    --output none

Write-Host "      Done." -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# STEP 6 – Deploy the ZIP
# ─────────────────────────────────────────────────────────────────────────────

Write-Host "`n[6/6] Deploying ZIP package to Azure..." -ForegroundColor Cyan
az webapp deploy `
    --resource-group $RESOURCE_GROUP `
    --name $WEB_APP_NAME `
    --src-path $ZIP_PATH `
    --type zip `
    --output none

Write-Host "      Done." -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# DONE
# ─────────────────────────────────────────────────────────────────────────────

$appUrl = "https://$WEB_APP_NAME.azurewebsites.net"
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "  Deployment complete!" -ForegroundColor Green
Write-Host "  App URL : $appUrl" -ForegroundColor White
Write-Host "  Portal  : https://portal.azure.com" -ForegroundColor White
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "NEXT STEPS:" -ForegroundColor Yellow
Write-Host "  1. Open $appUrl and log in with your InitialAdminEmail/Password"
Write-Host "  2. Once logged in, remove InitialAdminEmail and InitialAdminPassword"
Write-Host "     from App Service > Configuration > Application settings"
Write-Host "  3. Add your OpenWeatherMapApiKey and GeminiApiKey if not set above"
Write-Host ""
