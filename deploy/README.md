# Deploying HarbourConnect

## A. On your home network (this PC is the server)

Other phones and laptops on the same Wi-Fi can use the site. This PC must be on.

**Install or update:** double-click `deploy\Install-LanServer.cmd` and click **Yes** on the admin prompt.
Run it again after every code change. Your bookings, accounts and uploaded photos are kept.

When it finishes it prints the address to use, for example `http://192.168.1.25:8080/`.

**If other devices can't open it:**
- The Wi-Fi must be set to **Private**: Settings → Network & internet → your network → Private.
- Guest or public Wi-Fi often blocks devices from seeing each other. Try a home router or a phone hotspot.
- The IP address can change after a restart. Run the script again to see the current one.

**Options** (run `Install-LanServer.ps1` from an admin PowerShell window):

| Command | What it does |
|---|---|
| `.\Install-LanServer.ps1 -Port 8090` | Use a different port. |
| `.\Install-LanServer.ps1 -NoDemoAccounts` | Don't create the demo logins. |
| `.\Install-LanServer.ps1 -Remove` | Take the site down. Files and data are kept. |

**Good to know:**
- The LAN site has its own database, separate from the one Visual Studio uses when you press F5.
- It uses plain `http://` because there's no certificate on a home network. Don't open the port to the internet.

## B. On the internet (Azure, free tier)

1. In the Azure Portal, create a **SQL database** using the **free offer**. Set *Allow Azure services* to **Yes**, and copy the ADO.NET connection string.
2. In Visual Studio, right-click **APDP** → **Publish** → **Azure App Service (Windows)** → **Create new** → plan **Free (F1)** → **Publish**.
3. In the App Service, open **Environment variables**:
   - **Connection strings**: `HarbourConnectDB` = your connection string, type **SQLAzure**.
   - **App settings**: `GeminiApiKey`, `OpenWeatherMapApiKey`, and optionally `SeedDemoData` = `true`.
4. To update later: **Publish** again. The data in the database is kept.

`AppSecrets.config` is never published. On Azure the same settings go in **App settings**.
