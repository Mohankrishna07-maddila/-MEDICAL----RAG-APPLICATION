# Deployment Guide for HealthBot

This guide covers how to deploy the HealthBot (API + UI) and its dependencies.

## 1. Prerequisites (Target Server)
- **OS**: Windows Server or Ubuntu 20.04+ (Recommended)
- **.NET 8 Runtime**: [Download Here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- **Ollama**: Must be installed and running locally on the server.
- **AWS Credentials**: `~/.aws/credentials` or Environment Variables configured.

---

## 2. Preparing the Build
Run these commands in your development terminal to create optimized release files.

### 2.1 Publish API (Backend)
```powershell
dotnet publish HealthBot.Api -c Release -o ./publish/api
```

### 2.2 Publish UI (Frontend)
```powershell
dotnet publish HealthBot.Ui -c Release -o ./publish/ui
```
*Note: The UI is Blazor WASM, so it produces static files in `./publish/ui/wwwroot` that must be served by a web server.*

---

## 3. Deployment Option A: Windows (Local / IIS)

### Running Manually (Testing)
1. Copy the `./publish/api` folder to your target machine.
2. Open PowerShell and navigate to the folder.
3. Run: `dotnet HealthBot.Api.dll`
4. The API will start on `http://localhost:5030` (or configured port).

### Running in IIS (Production)
1. Install **Hosting Bundle** for .NET 8.
2. Create a new Website in IIS.
3. Point the **Physical Path** to your `./publish/api` folder.
4. Ensure App Pool is set to **No Managed Code**.
5. **Ollama**: Ensure Ollama is running as a background service or startup task.

---

## 4. Deployment Option B: Ubuntu / Linux (Nginx)

### Setup Service (Systemd)
Create a service file: `/etc/systemd/system/healthbot.service`

```ini
[Unit]
Description=HealthBot API
[Service]
WorkingDirectory=/var/www/healthbot/api
ExecStart=/usr/bin/dotnet /var/www/healthbot/api/HealthBot.Api.dll
Restart=always
# Restart service after 10 seconds if the dotnet service crashes:
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=healthbot-api
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=AWS_PROFILE=default

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl enable healthbot
sudo systemctl start healthbot
```

### Setup Ollama (Linux)
```bash
curl -fsSL https://ollama.com/install.sh | sh
ollama pull gemma3:4b
```
*Ensure Ollama is running on port 11434.*

---

## 5. Environment Variables & Security
Ensure your production `appsettings.json` or Environment Variables have:
- **AWS_ACCESS_KEY_ID**
- **AWS_SECRET_ACCESS_KEY**
- **AWS_REGION**

> **Warning**: Never commit production credentials to git. Use environment variables on the server.
