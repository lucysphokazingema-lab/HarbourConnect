<#
.SYNOPSIS
    Deploys HarbourConnect to Azure on the FREE tiers, or updates an existing deployment.

.DESCRIPTION
    First run (creates everything):
      - Resource group, Azure SQL server + database on the free offer
      - Free (F1) Windows App Service plan + web app (.NET Framework 4.8)
      - Connection string, API keys (read from APDP\AppSecrets.config) and your own TNPA admin
      - Builds the current code, uploads it and opens the site

    Later runs (same command): only rebuilds and uploads the code.
    The database and uploaded photos are kept.

    Requires the Azure CLI:  winget install -e --id Microsoft.AzureCLI
    The names it creates are remembered in deploy\azure.local.json (git-ignored).

.EXAMPLE
    .\Deploy-Azure.ps1
.EXAMPLE
    .\Deploy-Azure.ps1 -Location westeurope      # if your student subscription blocks South Africa North
#>
param(
    [string] $Location = "southafricanorth",
    [string] $AppName  = ""          # default: harbourconnect-<random>  (becomes <name>.azurewebsites.net)
)

$ErrorActionPreference = "Stop"
$repoRoot   = Split-Path -Parent $PSScriptRoot
$appFolder  = Join-Path $repoRoot "APDP"
$stateFile  = Join-Path $PSScriptRoot "azure.local.json"

function Step($text) { Write-Host ""; Write-Host "==> $text" -ForegroundColor Cyan }

# Runs an az command and stops with a readable message if it fails.
# (Named Invoke-Az: PowerShell names are case-insensitive, so "Az" would call itself.)
function Invoke-Az {
    $ErrorActionPreference = "Continue"     # az writes warnings to stderr; judge by exit code only
    $out = & az @args 2>&1
    if ($LASTEXITCODE -ne 0) {
        $msg = ($out | Out-String).Trim()
        if ($msg -match "RequestDisallowedByAzure|RequestDisallowedByPolicy|disallowed by policy") {
            throw "Azure for Students does not allow region '$Location'. Run again with another region, e.g.  .\Deploy-Azure.ps1 -Location westeurope   (or eastus, uksouth, northeurope).`n$msg"
        }
        throw "az $($args -join ' ') failed:`n$msg"
    }
    return $out
}

function New-Password {
    # Letters + digits only (meets Azure SQL complexity, safe on any command line)
    $chars = [char[]]"ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789"
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $bytes = New-Object byte[] 28; $rng.GetBytes($bytes)
    $p = -join ($bytes | ForEach-Object { $chars[$_ % $chars.Length] })
    return "Hc9" + $p     # guarantees upper, lower and digit
}

function Read-AppSecret($key) {
    $file = Join-Path $appFolder "AppSecrets.config"
    if (-not (Test-Path $file)) { return "" }
    [xml]$x = Get-Content $file -Raw
    $node = $x.SelectSingleNode("/appSettings/add[@key='$key']")
    if ($node) { return $node.value } else { return "" }
}

# ── 0. Azure CLI + sign-in ───────────────────────────────────────────────────
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    $cli = "${env:ProgramFiles}\Microsoft SDKs\Azure\CLI2\wbin"
    if (Test-Path "$cli\az.cmd") { $env:Path += ";$cli" }
    else { throw "Azure CLI not found. Install it with:  winget install -e --id Microsoft.AzureCLI   then open a new PowerShell window." }
}

Step "Checking your Azure sign-in"
$account = $null
try { $account = (Invoke-Az account show --only-show-errors) | ConvertFrom-Json } catch { }
if (-not $account) {
    # Use the normal browser sign-in (the Windows sign-in pop-up is easily hidden or cancelled)
    & az config set core.enable_broker_on_windows=false --only-show-errors | Out-Null
    Write-Host "A browser tab will open - sign in with your student account, then come back to this window."
    & az login --only-show-errors      # not hidden: it may ask you to pick a subscription
    $account = (Invoke-Az account show) | ConvertFrom-Json
}
Write-Host ("Signed in as {0}  |  subscription: {1}" -f $account.user.name, $account.name) -ForegroundColor Green

# ── Remembered names (so later runs update the same site) ────────────────────
if (Test-Path $stateFile) {
    $state = Get-Content $stateFile -Raw | ConvertFrom-Json
} else {
    $suffix = -join ((48..57) + (97..122) | Get-Random -Count 5 | ForEach-Object { [char]$_ })
    if (-not $AppName) { $AppName = "harbourconnect-$suffix" }
    $state = [pscustomobject]@{
        ResourceGroup = "harbourconnect-rg"
        Location      = $Location
        SqlServer     = "$AppName-sql"
        Database      = "HarbourConnectDB"
        Plan          = "harbourconnect-free-plan"
        WebApp        = $AppName
    }
}
# Lets you retry in another region after "region not allowed" (only matters before the site exists)
if ($PSBoundParameters.ContainsKey("Location")) { $state.Location = $Location }
$rg = $state.ResourceGroup
# Remember the names straight away so a run that fails half-way reuses them next time.
$state | ConvertTo-Json | Set-Content $stateFile -Encoding UTF8

function Test-Az { try { Invoke-Az @args --only-show-errors | Out-Null; return $true } catch { return $false } }
$exists = Test-Az webapp show -g $rg -n $state.WebApp

if (-not $exists) {
    Write-Host ""
    Write-Host "Creating a new deployment:  https://$($state.WebApp).azurewebsites.net  (region: $($state.Location))" -ForegroundColor Yellow

    # Your own TNPA admin (asked here so the password never leaves this window)
    $adminEmail = Read-Host "Email for your TNPA admin account"
    $sec1 = Read-Host "Password for that admin (min 8 chars, upper + lower + number)" -AsSecureString
    $sec2 = Read-Host "Type the password again" -AsSecureString
    $p1 = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec1))
    $p2 = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec2))
    if ($p1 -ne $p2) { throw "The passwords don't match. Run the script again." }
    if ($p1.Length -lt 8 -or $p1 -cnotmatch "[A-Z]" -or $p1 -cnotmatch "[a-z]" -or $p1 -notmatch "\d") {
        throw "Password too weak: use at least 8 characters with upper case, lower case and a number."
    }

    # New subscriptions must switch on each Azure service once before using it
    Step "Enabling the database and website services on your subscription"
    foreach ($ns in "Microsoft.Sql", "Microsoft.Web") {
        Invoke-Az provider register --namespace $ns --wait --only-show-errors | Out-Null
    }

    # ── 1. Resource group ────────────────────────────────────────────────────
    Step "Creating resource group '$rg'"
    Invoke-Az group create -n $rg -l $state.Location --only-show-errors | Out-Null

    # ── 2. SQL server + free database ────────────────────────────────────────
    Step "Creating the database server (takes a few minutes)"
    $sqlUser = "hcadmin"
    $sqlPass = New-Password
    if (Test-Az sql server show -g $rg -n $state.SqlServer) {
        # Left over from an earlier run that stopped part-way: give it the new password
        Invoke-Az sql server update -g $rg -n $state.SqlServer -p $sqlPass --only-show-errors | Out-Null
    } else {
        Invoke-Az sql server create -g $rg -n $state.SqlServer -l $state.Location -u $sqlUser -p $sqlPass --only-show-errors | Out-Null
    }
    Invoke-Az sql server firewall-rule create -g $rg -s $state.SqlServer -n AllowAzureServices `
       --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 --only-show-errors | Out-Null

    Step "Creating the database on the free offer"
    $freeDb = (Test-Az sql db show -g $rg -s $state.SqlServer -n $state.Database) -or
              (Test-Az sql db create -g $rg -s $state.SqlServer -n $state.Database `
                       -e GeneralPurpose -f Gen5 -c 2 --compute-model Serverless `
                       --use-free-limit --free-limit-exhaustion-behavior AutoPause)
    if (-not $freeDb) {
        Write-Warning "The free database offer isn't available on this subscription - using Basic (about US`$5/month from your credit)."
        Invoke-Az sql db create -g $rg -s $state.SqlServer -n $state.Database --edition Basic --only-show-errors | Out-Null
    }

    # ── 3. Free web app ──────────────────────────────────────────────────────
    Step "Creating the free (F1) website"
    if (-not (Test-Az appservice plan show -g $rg -n $state.Plan)) {
        Invoke-Az appservice plan create -g $rg -n $state.Plan -l $state.Location --sku F1 --is-linux false --only-show-errors | Out-Null
    }
    Invoke-Az webapp create -g $rg -p $state.Plan -n $state.WebApp --runtime "ASPNET:V4.8" --only-show-errors | Out-Null
    # The free tier runs 32-bit and has no Always On; force HTTPS.
    Invoke-Az webapp config set -g $rg -n $state.WebApp --use-32bit-worker-process true --only-show-errors | Out-Null
    Invoke-Az webapp update     -g $rg -n $state.WebApp --https-only true --only-show-errors | Out-Null

    # ── 4. Settings ──────────────────────────────────────────────────────────
    Step "Saving the connection string, API keys and admin account"
    $cs = "Server=tcp:$($state.SqlServer).database.windows.net,1433;Initial Catalog=$($state.Database);" +
          "User ID=$sqlUser;Password=$sqlPass;MultipleActiveResultSets=True;Encrypt=True;" +
          "TrustServerCertificate=False;Connection Timeout=60;"
    Invoke-Az webapp config connection-string set -g $rg -n $state.WebApp -t SQLAzure `
       --settings "HarbourConnectDB=$cs" --only-show-errors | Out-Null

    $settings = @(
        @{ name = "GeminiApiKey";          value = (Read-AppSecret "GeminiApiKey") },
        @{ name = "OpenWeatherMapApiKey";  value = (Read-AppSecret "OpenWeatherMapApiKey") },
        @{ name = "SeedDemoData";          value = "false" },
        @{ name = "InitialAdminEmail";     value = $adminEmail },
        @{ name = "InitialAdminPassword";  value = $p1 },
        @{ name = "SiteBaseUrl";           value = "https://$($state.WebApp).azurewebsites.net" }
    ) | ForEach-Object { [pscustomobject]@{ name = $_.name; value = $_.value; slotSetting = $false } }
    $settingsFile = Join-Path $env:TEMP "harbourconnect-settings.json"
    ConvertTo-Json @($settings) | Set-Content $settingsFile -Encoding UTF8
    try {
        Invoke-Az webapp config appsettings set -g $rg -n $state.WebApp --settings "@$settingsFile" --only-show-errors | Out-Null
    } finally {
        Remove-Item $settingsFile -ErrorAction SilentlyContinue      # it contained the admin password
    }

} else {
    Write-Host "Updating existing site https://$($state.WebApp).azurewebsites.net" -ForegroundColor Yellow
}

# ── 5. Build and upload the code ─────────────────────────────────────────────
Step "Building the current code"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild not found - install Visual Studio with the 'ASP.NET and web development' workload." }

$publishDir = Join-Path $env:TEMP "harbourconnect-publish"
$zipPath    = Join-Path $env:TEMP "harbourconnect-deploy.zip"
Remove-Item $publishDir, $zipPath -Recurse -Force -ErrorAction SilentlyContinue
& $msbuild (Join-Path $appFolder "APDP.csproj") /t:Build /p:Configuration=Release /p:DeployOnBuild=true `
    /p:PublishProfile=LanServer "/p:publishUrl=$publishDir" /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed (see errors above)." }
Add-Type -AssemblyName System.IO.Compression.FileSystem      # zip with "/" paths, as Azure expects
[System.IO.Compression.ZipFile]::CreateFromDirectory($publishDir, $zipPath)

Step "Uploading to Azure"
# --clean false keeps photos that users uploaded to the live site
Invoke-Az webapp deploy -g $rg -n $state.WebApp --src-path $zipPath --type zip --clean false --only-show-errors | Out-Null

# ── 6. Warm up ───────────────────────────────────────────────────────────────
$url = "https://$($state.WebApp).azurewebsites.net/"
Step "Starting the site (the first start creates the database tables - can take a few minutes)"
$ok = $false
for ($i = 1; $i -le 12 -and -not $ok; $i++) {
    try {
        $r = Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 120
        $ok = $r.StatusCode -eq 200
    } catch {
        Write-Host "  still starting ($i/12)..."
        Start-Sleep -Seconds 15
    }
}

Write-Host ""
if ($ok) {
    Write-Host "HarbourConnect is live:  $url" -ForegroundColor Green
    if (-not $exists) {
        Write-Host "Log in at ${url}Tnpa/Login with the admin email and password you just entered."
        Write-Host "Afterwards you can delete InitialAdminEmail / InitialAdminPassword in the Azure Portal"
        Write-Host "(App Service > Settings > Environment variables) - the account stays."
    }
} else {
    Write-Warning "The site didn't answer yet. Open $url in a browser in a minute or two."
    Write-Warning "If it shows an error, see Azure Portal > your App Service > Log stream."
}
