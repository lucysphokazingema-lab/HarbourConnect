#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs or updates HarbourConnect as a website on this PC's IIS, reachable by other
    devices on the same (private) network at http://<this-PC-IP>:<Port>/

.DESCRIPTION
    Run it again any time you change the code: it rebuilds, republishes and restarts the
    site. The database and uploaded photos are kept.

    What it does:
      1. Turns on the Windows IIS web server + ASP.NET 4.8 (built into Windows, free)
      2. Builds the app in Release mode and publishes it to C:\inetpub\HarbourConnect
      3. Copies AppSecrets.config (API keys) and already-uploaded boat photos
      4. Adjusts the published Web.config for plain-HTTP use on a home network
      5. Creates the "HarbourConnect" app pool + website and opens the firewall port
         (Private networks only - not public Wi-Fi)

    The database is SQL Server LocalDB running under the website's own identity, so it is
    separate from the database Visual Studio uses when you press F5.

.EXAMPLE
    .\Install-LanServer.ps1                    # install / update on port 8080
.EXAMPLE
    .\Install-LanServer.ps1 -Port 8090 -NoDemoAccounts
.EXAMPLE
    .\Install-LanServer.ps1 -Remove            # take the site down (files and data are kept)
#>
param(
    [int]    $Port     = 8080,
    [string] $SiteName = "HarbourConnect",
    [string] $SitePath = "C:\inetpub\HarbourConnect",
    [switch] $NoDemoAccounts,
    [switch] $Remove
)

$ErrorActionPreference = "Stop"
$repoRoot  = Split-Path -Parent $PSScriptRoot
$project   = Join-Path $repoRoot "APDP\APDP.csproj"
$appFolder = Join-Path $repoRoot "APDP"
$ruleName  = "HarbourConnect LAN ($Port)"

function Step($text) { Write-Host ""; Write-Host "==> $text" -ForegroundColor Cyan }

# ── Remove ───────────────────────────────────────────────────────────────────
if ($Remove) {
    Import-Module WebAdministration
    if (Test-Path "IIS:\Sites\$SiteName")    { Remove-Website -Name $SiteName;    Write-Host "Website removed." }
    if (Test-Path "IIS:\AppPools\$SiteName") { Remove-WebAppPool -Name $SiteName; Write-Host "App pool removed." }
    Get-NetFirewallRule -DisplayName "HarbourConnect LAN*" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    Write-Host "Firewall rule removed. Files in $SitePath were kept." -ForegroundColor Green
    return
}

# ── 1. IIS + ASP.NET 4.8 ─────────────────────────────────────────────────────
Step "Turning on IIS and ASP.NET 4.8 (first run can take a few minutes)"
$features = @(
    "IIS-WebServerRole", "IIS-WebServer", "IIS-CommonHttpFeatures", "IIS-StaticContent",
    "IIS-DefaultDocument", "IIS-HttpErrors", "IIS-RequestFiltering", "IIS-HttpCompressionStatic",
    "IIS-ApplicationDevelopment", "IIS-NetFxExtensibility45", "IIS-ISAPIExtensions",
    "IIS-ISAPIFilter", "IIS-ASPNET45", "NetFx4Extended-ASPNET45", "IIS-ManagementConsole"
)
$missing = $features | Where-Object { (Get-WindowsOptionalFeature -Online -FeatureName $_).State -ne "Enabled" }
if ($missing) {
    $result = Enable-WindowsOptionalFeature -Online -FeatureName $missing -All -NoRestart
    if ($result.RestartNeeded) { Write-Warning "Windows wants a restart to finish enabling IIS. Restart, then run this script again." }
} else {
    Write-Host "Already enabled."
}
Import-Module WebAdministration

# ── 2. Build + publish ───────────────────────────────────────────────────────
Step "Building and publishing the app"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild not found. Install Visual Studio with the 'ASP.NET and web development' workload." }

# Stop the running site so its files are not locked while copying
if (Test-Path "IIS:\AppPools\$SiteName") {
    if ((Get-WebAppPoolState -Name $SiteName).Value -eq "Started") { Stop-WebAppPool -Name $SiteName; Start-Sleep -Seconds 2 }
}

& $msbuild $project /t:Build /p:Configuration=Release /p:DeployOnBuild=true /p:PublishProfile=LanServer `
           "/p:publishUrl=$SitePath" /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed (see errors above)." }

# ── 3. Secrets + existing photos ─────────────────────────────────────────────
Step "Copying settings and uploaded photos"
$secrets = Join-Path $appFolder "AppSecrets.config"
if (Test-Path $secrets) {
    Copy-Item $secrets (Join-Path $SitePath "AppSecrets.config") -Force
    Write-Host "AppSecrets.config copied."
} else {
    Write-Warning "APDP\AppSecrets.config not found - chatbot/weather keys and demo accounts will be missing."
}

$photosFrom = Join-Path $appFolder "Content\BoatImages"
$photosTo   = Join-Path $SitePath  "Content\BoatImages"
New-Item -ItemType Directory -Force -Path $photosTo | Out-Null
# /XO = never overwrite a newer file already on the server (photos uploaded through the site)
robocopy $photosFrom $photosTo /E /XO /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Copying photos failed (robocopy exit $LASTEXITCODE)." }

# ── 4. Web.config for plain HTTP on a home network ───────────────────────────
Step "Adjusting Web.config for the local network"
$configPath = Join-Path $SitePath "Web.config"
[xml]$cfg = Get-Content $configPath -Raw

# The Release build may carry an Azure SQL placeholder; on the LAN use the same local
# database connection string as the project's own Web.config (SQL Server LocalDB).
[xml]$sourceCfg = Get-Content (Join-Path $appFolder "Web.config") -Raw
$localCs = $sourceCfg.SelectSingleNode("/configuration/connectionStrings/add[@name='HarbourConnectDB']").connectionString
$cs = $cfg.SelectSingleNode("/configuration/connectionStrings/add[@name='HarbourConnectDB']")
if ($cs -and $localCs) { $cs.SetAttribute("connectionString", $localCs) }

# The Release build forces HTTPS (for Azure). On a LAN there is no certificate, so:
$rewrite = $cfg.SelectSingleNode("/configuration/system.webServer/rewrite")
if ($rewrite) { [void]$rewrite.ParentNode.RemoveChild($rewrite) }     # no HTTP->HTTPS redirect (needs URL Rewrite module anyway)
$cookies = $cfg.SelectSingleNode("/configuration/system.web/httpCookies")
if ($cookies) { $cookies.SetAttribute("requireSSL", "false") }        # session cookie must work over HTTP

$cfg.Save($configPath)

$deployedSecrets = Join-Path $SitePath "AppSecrets.config"
if (Test-Path $deployedSecrets) {
    [xml]$sec = Get-Content $deployedSecrets -Raw
    $seed = $sec.SelectSingleNode("/appSettings/add[@key='SeedDemoData']")
    if (-not $seed) {
        $seed = $sec.CreateElement("add"); $seed.SetAttribute("key", "SeedDemoData")
        [void]$sec.DocumentElement.AppendChild($seed)
    }
    $seed.SetAttribute("value", $(if ($NoDemoAccounts) { "false" } else { "true" }))
    $sec.Save($deployedSecrets)
}

# ── 5. App pool, website, permissions, firewall ──────────────────────────────
Step "Configuring the IIS website"
if (-not (Test-Path "IIS:\AppPools\$SiteName")) {
    New-WebAppPool -Name $SiteName | Out-Null
}
$pool = "IIS:\AppPools\$SiteName"
Set-ItemProperty $pool -Name managedRuntimeVersion -Value "v4.0"
Set-ItemProperty $pool -Name managedPipelineMode   -Value "Integrated"
# LocalDB needs the pool's user profile loaded (it keeps its database files there)
Set-ItemProperty $pool -Name processModel.loadUserProfile       -Value $true
Set-ItemProperty $pool -Name processModel.setProfileEnvironment -Value $true
Set-ItemProperty $pool -Name processModel.idleTimeout           -Value ([TimeSpan]::FromMinutes(0))

if (-not (Test-Path "IIS:\Sites\$SiteName")) {
    New-Website -Name $SiteName -PhysicalPath $SitePath -ApplicationPool $SiteName -Port $Port -IPAddress "*" | Out-Null
} else {
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath    -Value $SitePath
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $SiteName
    Set-WebBinding -Name $SiteName -BindingInformation (Get-WebBinding -Name $SiteName | Select-Object -First 1).bindingInformation `
                   -PropertyName Port -Value $Port
}

$identity = "IIS AppPool\$SiteName"
icacls $SitePath  /grant "${identity}:(OI)(CI)RX" /T /Q | Out-Null
icacls $photosTo  /grant "${identity}:(OI)(CI)M"  /T /Q | Out-Null     # owners upload photos here

if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP `
                        -LocalPort $Port -Profile Private | Out-Null
}

Start-WebAppPool -Name $SiteName -ErrorAction SilentlyContinue
Start-Website    -Name $SiteName -ErrorAction SilentlyContinue

# ── 6. Warm up + show addresses ──────────────────────────────────────────────
Step "Starting the site (the first start creates the database - can take up to 2 minutes)"
try {
    $resp = Invoke-WebRequest "http://localhost:$Port/" -UseBasicParsing -TimeoutSec 180
    Write-Host "Site is up (HTTP $($resp.StatusCode))." -ForegroundColor Green
} catch {
    Write-Warning "The site did not answer yet: $($_.Exception.Message)"
    Write-Warning "Open http://localhost:$Port/ in a browser on this PC to see the error details."
}

$profiles = Get-NetConnectionProfile | Where-Object { $_.NetworkCategory -eq "Public" }
if ($profiles) {
    Write-Warning ("Your network '" + ($profiles.Name -join "', '") + "' is set to Public, so other devices are blocked. " +
                   "Change it to Private in Settings > Network & internet, or run: " +
                   "Set-NetConnectionProfile -Name '<network name>' -NetworkCategory Private")
}

Write-Host ""
Write-Host "HarbourConnect is running. Open it from:" -ForegroundColor Green
Write-Host "  This PC:        http://localhost:$Port/"
Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*" -and $_.PrefixOrigin -ne "WellKnown" } |
    ForEach-Object { Write-Host ("  Other devices:  http://{0}:{1}/   ({2})" -f $_.IPAddress, $Port, $_.InterfaceAlias) }
