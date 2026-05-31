# Installation

How to get Quartermaster running on a server from scratch. For tuning the running instance (SMTP, SAML, rate-limits, etc.) see [Configuration.md](Configuration.md).

## Prerequisites

| Component | Minimum | Notes |
|---|---|---|
| .NET runtime | 10.0 | ASP.NET Core runtime is enough on the host; the SDK is only needed if you build on the server |
| MySQL | 8.0 | Or a compatible drop-in (MariaDB 10.6+ works in testing) |
| Reverse proxy | nginx, Caddy, Traefik, IIS | Required for TLS termination and stable client IPs |
| OS | Linux, Windows, macOS | Production deployments are tested on Linux |

The app is fully self-contained — no Node.js, no Redis, no Elasticsearch. Generated PDFs and (optionally) member-import CSVs live on the local filesystem.

## 1. Database

Create a database and a user with full rights on it:

```sql
CREATE DATABASE quartermaster CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'quartermaster'@'%' IDENTIFIED BY '<strong-password>';
GRANT ALL PRIVILEGES ON quartermaster.* TO 'quartermaster'@'%';
FLUSH PRIVILEGES;
```

Schema migrations run automatically on every app start — there is no separate migration step.

## 2. Publish the app

On a build machine:

```bash
dotnet publish Quartermaster.Server/Quartermaster.Server.csproj \
  -c Release -o /srv/quartermaster
```

The resulting directory is self-contained relative to its referenced runtime — copy it to the server (e.g. `/opt/quartermaster`).

## 3. Configuration file

Create `appsettings.Production.json` next to the published binary, using `appsettings.template.json` as the starting point:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "DatabaseSettings": {
    "ConnectionString": "server=db.internal;user id=quartermaster;password=<strong-password>;database=quartermaster;pooling=true;max pool size=20;min pool size=5;"
  },
  "ForwardedHeaders": {
    "KnownProxies": ["127.0.0.1"],
    "KnownNetworks": []
  }
}
```

`ForwardedHeaders` **must** list every proxy IP (or CIDR network) you trust to set `X-Forwarded-For` / `X-Forwarded-Proto`. With both lists empty the headers are ignored entirely and the client IP collapses to the proxy address — rate-limiting and audit logging will all blame the proxy. See [Configuration.md → Reverse proxy](Configuration.md#reverse-proxy) for details.

## 4. Data directories

Generated artefacts are written under the app's working directory by default:

| Path | Used for | Override |
|---|---|---|
| `./data/printouts` | PDF print-outs of notifications | `messaging.pdf.output_dir` |
| `./data/protocols` | Archived meeting protocol PDFs | `meetings.protocol.archive_dir` |

Make sure the service user can write to the working directory, or set the overrides to a location it can.

## 5. Bootstrap the first admin

Schema and seed data are created on first start, but the first admin user must be created manually:

```bash
cd /opt/quartermaster
ASPNETCORE_ENVIRONMENT=Production dotnet Quartermaster.Server.dll init-admin
```

The command prompts for a username and a password (minimum 12 characters), runs any pending migrations, and inserts the admin user. It exits with code 0 on success. Re-running with the same username is rejected.

After this step every further configuration change happens in-app via `/Administration/Settings` while logged in as that admin.

## 6. Run the app

For a quick test:

```bash
ASPNETCORE_ENVIRONMENT=Production dotnet Quartermaster.Server.dll
```

The app binds Kestrel to ports `5232` (HTTP) and `7213` (HTTPS) by default. Override with `ASPNETCORE_URLS=http://0.0.0.0:5000` when running behind a reverse proxy that handles TLS.

### systemd unit (Linux)

```ini
[Unit]
Description=Quartermaster
After=network.target mysql.service

[Service]
WorkingDirectory=/opt/quartermaster
ExecStart=/usr/bin/dotnet /opt/quartermaster/Quartermaster.Server.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=quartermaster
User=quartermaster
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000

[Install]
WantedBy=multi-user.target
```

```bash
systemctl daemon-reload
systemctl enable --now quartermaster
journalctl -u quartermaster -f
```

## 7. Reverse proxy

Quartermaster expects TLS to terminate at the proxy. The proxy must forward `X-Forwarded-For` and `X-Forwarded-Proto`, and its IP must appear in `ForwardedHeaders.KnownProxies`. A minimal nginx example:

```nginx
server {
    listen 443 ssl http2;
    server_name quartermaster.example.de;

    ssl_certificate     /etc/letsencrypt/live/quartermaster.example.de/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/quartermaster.example.de/privkey.pem;

    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   Upgrade           $http_upgrade;
        proxy_set_header   Connection        $connection_upgrade;
        proxy_read_timeout 300s;
    }
}
```

The `Upgrade` / `Connection` headers are required for the SignalR meeting hub (`/hubs/meeting`). Without them collaborative meeting notes fall back to long-polling.

## 8. First login

Navigate to `https://quartermaster.example.de`, log in with the admin account created in step 5, and open **Administration → Settings**. Set at minimum:

- `system.public_base_url` — the externally-reachable URL (used to build links in outgoing notifications)
- `general.chaptername.display` — the umbrella organisation's display name
- `general.contact.email` — visible in error pages and notification footers

The full configuration reference is in [Configuration.md](Configuration.md).

## Upgrading

1. Stop the service.
2. Replace the contents of `/opt/quartermaster` with the new publish output, keeping `appsettings.Production.json` and the `data/` directory in place.
3. Start the service. Pending schema migrations apply automatically on startup.

No separate downtime window is needed beyond the restart itself; migrations are designed to be backwards-compatible within a release line.
