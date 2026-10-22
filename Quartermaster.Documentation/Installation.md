# Installation

How to get Quartermaster running on a server from scratch. For tuning the running instance (SMTP, SAML, rate-limits, etc.) see [Configuration.md](Configuration.md).

## Prerequisites

| Component | Minimum | Notes |
|---|---|---|
| OS | Linux x64 | Production deployments are tested on Debian/Ubuntu; other targets work with a matching `-r` runtime identifier |
| MySQL | 8.0 | Or a compatible drop-in (MariaDB 10.6+ works in testing) |
| ICU | any | The distribution's `libicu` package (e.g. `libicu76` on Debian 13, `libicu74` on Ubuntu 24.04), needed for German date and number formatting |
| Reverse proxy | Caddy, nginx, Traefik | Required for TLS termination and stable client IPs |

The published app is self-contained: it brings its own .NET runtime, so the server needs no .NET installation, and there is no Node.js, Redis or Elasticsearch. Generated PDFs and (optionally) member-import CSVs live on the local filesystem.

The examples below assume the domain `quartermaster.example.de`, the install directory `/var/www/quartermaster` and Caddy as the reverse proxy.

## 1. Database

Create a database and a user with full rights on it. When the database runs on the same host, restrict the user to `localhost`:

```sql
CREATE DATABASE quartermaster CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'quartermaster'@'localhost' IDENTIFIED BY '<strong-password>';
GRANT ALL PRIVILEGES ON quartermaster.* TO 'quartermaster'@'localhost';
FLUSH PRIVILEGES;
```

Schema migrations run automatically on every app start; there is no separate migration step.

## 2. Publish the app

On a build machine with the .NET 10 SDK:

```bash
dotnet publish Quartermaster.Server/Quartermaster.Server.csproj \
  -c Release -r linux-x64 --self-contained true -o publish/quartermaster
rm publish/quartermaster/appsettings.Development.json
tar -czf quartermaster-linux-x64.tar.gz -C publish/quartermaster .
```

Use `-r linux-arm64` for ARM servers. Copy the archive to the server, e.g. with `scp`.

## 3. Service user and files

Create a system user with a real home directory. ASP.NET Core stores its data protection keys (login cookies, antiforgery tokens) under the home directory; without one, the keys are regenerated on every restart and all users are logged out.

```bash
useradd --system --create-home --home-dir /var/lib/quartermaster --shell /usr/sbin/nologin quartermaster

mkdir -p /var/www/quartermaster
tar -xzf quartermaster-linux-x64.tar.gz -C /var/www/quartermaster
mkdir -p /var/www/quartermaster/data
chown -R root:root /var/www/quartermaster
chown -R quartermaster:quartermaster /var/www/quartermaster/data
chmod +x /var/www/quartermaster/Quartermaster.Server
```

The app files stay read-only for the service user; only `data/` is writable.

## 4. Configuration file

Create `/var/www/quartermaster/appsettings.Production.json`, using `appsettings.template.json` as the starting point:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "quartermaster.example.de",
  "DatabaseSettings": {
    "ConnectionString": "server=localhost;user id=quartermaster;password=<strong-password>;database=quartermaster;pooling=true;max pool size=20;min pool size=5;"
  },
  "ForwardedHeaders": {
    "KnownProxies": ["127.0.0.1"],
    "KnownNetworks": []
  }
}
```

The file contains the database password, so restrict it to the service user:

```bash
chown root:quartermaster /var/www/quartermaster/appsettings.Production.json
chmod 640 /var/www/quartermaster/appsettings.Production.json
```

`ForwardedHeaders` **must** list every proxy IP (or CIDR network) you trust to set `X-Forwarded-For` / `X-Forwarded-Proto`. With both lists empty the headers are ignored entirely and the client IP collapses to the proxy address, so rate-limiting and audit logging will all blame the proxy. See [Configuration.md, Reverse proxy](Configuration.md#reverse-proxy) for details.

## 5. Data directories

Generated artefacts are written under the app's working directory by default:

| Path | Used for | Override |
|---|---|---|
| `./data/printouts` | PDF print-outs of notifications | `messaging.pdf.output_dir` |
| `./data/protocols` | Archived meeting protocol PDFs | `meetings.protocol.archive_dir` |
| `data/receipts` beside the executable | Receipts uploaded by logged-in users for travel expense applications, one folder per user | none |
| `data/support-signatures` beside the executable | Blank form PDFs uploaded for UU-Assistent collections, one file per collection | none |

Make sure the service user can write to these directories, or set the overrides to a location it can.

The working directory must also be the install directory: the daily administrative division import reads `DE_Base.txt` and `DE_PostCodes.txt` from it.

## 6. Bootstrap the first admin

Schema and seed data are created on first start, but the first admin user must be created manually:

```bash
cd /var/www/quartermaster
sudo -u quartermaster env ASPNETCORE_ENVIRONMENT=Production ./Quartermaster.Server init-admin
```

The command prompts for a username and a password (minimum 12 characters), runs any pending migrations, and inserts the admin user. It exits with code 0 on success. Re-running with the same username is rejected.

After this step every further configuration change happens in-app via `/Administration/Settings` while logged in as that admin.

## 7. Run the app

The app binds Kestrel to ports `5232` (HTTP) and `7213` (HTTPS) by default. Behind a reverse proxy that handles TLS, bind it to loopback with `ASPNETCORE_URLS=http://127.0.0.1:5000`.

### systemd unit (Linux)

Create `/etc/systemd/system/quartermaster.service`:

```ini
[Unit]
Description=Quartermaster
After=network.target mariadb.service
Wants=mariadb.service

[Service]
WorkingDirectory=/var/www/quartermaster
ExecStart=/var/www/quartermaster/Quartermaster.Server
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

Use `mysql.service` instead of `mariadb.service` when running MySQL.

```bash
systemctl daemon-reload
systemctl enable --now quartermaster
journalctl -u quartermaster -f
```

A healthy start logs `Application started` and, shortly after, the administrative division import (`Admin division import complete: ...`).

## 8. Reverse proxy

Quartermaster expects TLS to terminate at the proxy. The proxy must forward `X-Forwarded-For` and `X-Forwarded-Proto`, pass WebSocket upgrades through for the SignalR hubs (`/hubs/meeting`, `/hubs/assembly`), and its IP must appear in `ForwardedHeaders.KnownProxies`.

Don't add security headers (CSP, HSTS, `X-Frame-Options`) at the proxy; the app sets its own, and duplicated headers can conflict.

### Caddy

Add to `/etc/caddy/Caddyfile`:

```
quartermaster.example.de {
    encode zstd gzip
    reverse_proxy 127.0.0.1:5000
}
```

```bash
systemctl reload caddy
```

Caddy obtains and renews the certificate itself, sets `X-Forwarded-For` / `X-Forwarded-Proto` and passes WebSockets through by default; nothing else is needed.

### nginx

```nginx
server {
    listen 80;
    listen [::]:80;
    server_name quartermaster.example.de;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl;
    listen [::]:443 ssl;
    http2 on;
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

The `Upgrade` / `Connection` headers are required for the SignalR hubs. Without them collaborative meeting notes and the assembly console fall back to long-polling.

Only ports 80 and 443 need to be reachable from outside; the app listens on loopback and the database stays local.

## 9. First login

Navigate to `https://quartermaster.example.de`, log in with the admin account created in step 6, and open **Administration → Settings**. Set at minimum:

- `system.public_base_url`: the externally-reachable URL without a trailing slash (used to build links in outgoing notifications)
- `general.chaptername.display`: the umbrella organisation's display name
- `general.contact.email`: visible in error pages and notification footers
- `email.smtp.*`: without SMTP no confirmation mails go out

The public membership application, due selection and motion forms start disabled (`public_forms.*`); enable them once the instance is ready. The full configuration reference is in [Configuration.md](Configuration.md).

## Backups

Back up the database, the `data/` directory and `appsettings.Production.json`. A nightly dump, for example:

```bash
mysqldump --single-transaction quartermaster | gzip > /var/backups/quartermaster-$(date +%F).sql.gz
```

## Upgrading

The new version goes into a fresh folder next to the running one. The old folder stays untouched and serves as the rollback.

### With the deploy script

[`deploy-quartermaster.sh`](deploy-quartermaster.sh) runs all steps of the manual upgrade below in one go. Copy it to the server once and make it executable, then pass it the archive and the checksum published for the release:

```bash
sudo ./deploy-quartermaster.sh quartermaster-linux-x64.tar.gz <sha256> --backup
journalctl -u quartermaster -f
```

- It refuses to run when the checksum does not match, when `quartermaster-new` is left over from an earlier attempt, or when the archive does not have `Quartermaster.Server` at its top level.
- `--backup` writes a database dump to the home directory of the user calling `sudo` first. Use it for every release with a schema migration.
- The old folder is named after the version it contains (the highest `Changelog/V*.md` in it), for example `/var/www/quartermaster-v8.0`.
- If the service does not stay up after the start, the script prints the last log lines and the rollback command.

### Manual upgrade

1. Copy the new archive to the server and compare its checksum with the one published for the release:

   ```bash
   sha256sum quartermaster-linux-x64.tar.gz
   ```

2. If the release contains a schema migration, back up the database first (see [Backups](#backups)).
3. Unpack the archive into a new folder:

   ```bash
   mkdir -p /var/www/quartermaster-new
   tar -xzf quartermaster-linux-x64.tar.gz -C /var/www/quartermaster-new
   chown -R root:root /var/www/quartermaster-new
   chmod +x /var/www/quartermaster-new/Quartermaster.Server
   ```

4. Stop the service and carry over the configuration and the data directory. `cp -a` keeps the ownership and permissions:

   ```bash
   systemctl stop quartermaster
   cp -a /var/www/quartermaster/appsettings.Production.json /var/www/quartermaster-new/
   cp -a /var/www/quartermaster/data /var/www/quartermaster-new/
   ```

5. Swap the folders, keeping the old one under its version number:

   ```bash
   mv /var/www/quartermaster /var/www/quartermaster-v6.0
   mv /var/www/quartermaster-new /var/www/quartermaster
   ```

6. Start the service and check the log. Pending schema migrations apply automatically on startup:

   ```bash
   systemctl start quartermaster
   journalctl -u quartermaster -f
   ```

To roll back, stop the service, move the old folder back to `/var/www/quartermaster` and start it again. If a migration ran, restore the database backup as well. Remove old folders once the new version runs fine.

No separate downtime window is needed beyond the restart itself; migrations are designed to be backwards-compatible within a release line.
