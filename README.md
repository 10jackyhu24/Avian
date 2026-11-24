# Avian

Clone, build, and deploy the default shared-world multiplayer setup on Windows. Run the commands below in PowerShell.

## Prerequisites

- Git, Git LFS, Python 3.11+, `kind`, and `kubectl` available in your terminal.
- Docker Desktop running with Linux containers and Docker Compose.
- Unity **6000.3.15f1**, including Linux Dedicated Server Build Support.
- A public IP or domain pointing to the host. Allow inbound TCP **80** (certificate issuance/renewal) and **8443** (HTTPS/WSS) through your firewall/router. Keep host port 80 free for Caddy; deployment does not configure your firewall/router.

## 1. Clone the project

```powershell
git clone https://github.com/10jackyhu24/Avian.git
cd Avian
```

## 2. Configure the host and client

Copy the templates without overwriting existing local settings:

```powershell
if (!(Test-Path backend/.env)) { Copy-Item backend/.env.example backend/.env }
if (!(Test-Path network-config.json)) { Copy-Item network-config.example.json network-config.json }
```

Edit `backend/.env` and replace these values:

```env
DB_USER=avian
DB_PASSWORD=YOUR_STRONG_PASSWORD
DB_NAME=avian_db
AVIAN_API_HOST=YOUR_PUBLIC_IP_OR_DOMAIN
```

Keep the remaining example defaults, including `ROOM_MODE=static` and port `8443`. If reusing an existing database, preserve its credentials.

Edit `network-config.json` to use the same host:

```json
{ "apiBaseUrl": "https://YOUR_PUBLIC_IP_OR_DOMAIN:8443" }
```

## 3. Build in Unity

Open the cloned project through Unity Hub and wait for asset import. Select **Avian > Build server and Windows client**.

The menu builds the correct scenes and copies the client config. Outputs:

- Server: `Builds/LinuxServer/AvianServer.x86_64`
- Client: `Builds/WindowsClient/Avian.exe`

## 4. Deploy

With Docker Desktop running, execute from the project root:

```powershell
python compose.py --all
```

This creates/reuses the Kind cluster, generates internal TLS certificates, installs the game TLS Secret, deploys the built game server, and starts the backend services. It preserves existing database volumes.

## 5. Verify and connect

Replace the host below with your configured public IP or domain:

```powershell
curl.exe https://YOUR_PUBLIC_IP_OR_DOMAIN:8443/health
python export_client_config.py --output Builds/WindowsClient/network-config.json
```

The health endpoint should return `ok`; the exporter checks Matchmaker and the game WebSocket path before writing the client config.

Run `Builds/WindowsClient/Avian.exe`, or copy the **entire** `Builds/WindowsClient` folder to another computer. Use different accounts, choose **Match**, select characters, and confirm both players enter the world. For Editor testing, open `Assets/Scenes/SampleScene.unity`.

Keep `backend/.env` and `backend/.tls/` private and out of Git/client builds. Internal certificates expire after 90 days; see [TLS renewal instructions](backend/tls/README.md).
