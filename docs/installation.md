# Overseer Installation Guide

This guide covers the different ways to install and run Overseer for monitoring your 3D printers.

## Table of Contents

- [Requirements](#requirements)
- [Installation Methods](#installation-methods)
  - [Docker (Recommended)](#docker-recommended)
  - [Linux with systemd](#linux-with-systemd)
  - [Manual Installation](#manual-installation)
- [Configuration](#configuration)
- [Updating](#updating)
- [Troubleshooting](#troubleshooting)

---

## Requirements

### Supported Platforms

- **Docker**: Any platform that supports Docker (Linux, Windows, macOS)
- **Native**: Linux with systemd (Ubuntu, Debian, Raspberry Pi OS, etc.)
- **Manual**: Any platform that supports .Net 10+

### Hardware Requirements

- Minimum 1GB RAM (2GB recommended for AI monitoring features)
- 500MB disk space for the application
- Additional storage for logs and database

### Network Requirements

- Port 9000 (default) for the web interface
- Network access to your 3D printers

---

## Installation Methods

### Docker (Recommended)

Docker is the easiest way to run Overseer and works on any platform that supports Docker/Podman.

#### Quick Start

```bash
docker run -d \
  --name overseer \
  -p 9000:9000 \
  -v overseer-data:/app/data \
  --restart unless-stopped \
  ghcr.io/overseerapp/overseer:latest
```

#### Docker Compose

Create a `docker-compose.yml` file:

```yaml
version: '3.8'

services:
  overseer:
    image: ghcr.io/overseerapp/overseer:latest
    container_name: overseer
    ports:
      - '9000:9000'
    volumes:
      - overseer-data:/app/data
    restart: unless-stopped
    environment:
      - ASPNETCORE_ENVIRONMENT=Production

volumes:
  overseer-data:
```

Then run:

```bash
docker-compose up -d
```

#### Docker with Custom Port

```bash
docker run -d \
  --name overseer \
  -p 8080:9000 \
  -v overseer-data:/app/data \
  ghcr.io/overseerapp/overseer:latest
```

#### Accessing Printer Network

If your printers are on the host network, you may need to use host networking:

```bash
docker run -d \
  --name overseer \
  --network host \
  -v overseer-data:/app/data \
  ghcr.io/overseerapp/overseer:latest
```

#### Using Podman

Podman is a drop-in replacement for Docker. The same commands work:

```bash
podman run -d \
   --name overseer \
   -p 9000:9000 \
   -v overseer-data:/app/data \
   --restart unless-stopped \
   ghcr.io/overseerapp/overseer:latest
```

---

### Linux with systemd

This method installs Overseer as a native systemd service on Linux systems.

#### Automated Installation

Download and run the installer script:

```bash
# Download the installer
wget https://github.com/overseerapp/overseer/releases/latest/download/overseer.sh

# Make it executable
chmod +x overseer.sh

# Run the installer (requires root)
sudo ./overseer.sh install
```

The installer will:

1. Install .NET runtime and dependencies
2. Download and extract Overseer
3. Create a systemd service
4. Start the service automatically
5. Optionally configure Nginx as a reverse proxy

#### Install a Specific Version

```bash
sudo ./overseer.sh install 2.0.0
```

#### Service Management

```bash
# Check status
sudo systemctl status overseer

# Stop the service
sudo systemctl stop overseer

# Start the service
sudo systemctl start overseer

# Restart the service
sudo systemctl restart overseer

# View logs
sudo journalctl -u overseer -f
```

#### Installation Directories

- **Application**: `/path/to/overseer/` (where you ran the installer)
- **Service file**: `/lib/systemd/system/overseer.service`
- **Logs**: `/var/log/overseer.log`

---

### Manual Installation

For advanced users who want full control over the installation.

#### Prerequisites

1. Install .NET 10.0 ASP.NET Core Runtime:

   ```bash
   # Ubuntu/Debian
   wget https://dot.net/v1/dotnet-install.sh
   chmod +x dotnet-install.sh
   ./dotnet-install.sh --runtime aspnetcore --version latest
   ```

2. Install OpenCV dependencies (for AI monitoring):
   ```bash
   sudo apt-get install -y libopencv-dev libgdiplus
   ```

#### Installation Steps

1. Download the latest release:

   ```bash
   wget https://github.com/overseerapp/overseer/releases/latest/download/overseer_server_VERSION.zip
   ```

2. Extract to your desired location:

   ```bash
   mkdir -p /opt/overseer
   unzip overseer_server_VERSION.zip -d /opt/overseer
   ```

3. Run the application:
   ```bash
   cd /opt/overseer
   dotnet Overseer.Server.dll
   ```

#### Creating a systemd Service Manually

Create `/lib/systemd/system/overseer.service`:

```ini
[Unit]
Description=Overseer Daemon

[Service]
WorkingDirectory=/opt/overseer
ExecStart=/path/to/.dotnet/dotnet /opt/overseer/Overseer.Server.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=overseer
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

Enable and start:

```bash
sudo systemctl daemon-reload
sudo systemctl enable overseer
sudo systemctl start overseer
```

---

## Configuration

### Accessing the Web Interface

After installation, access Overseer at:

- **Default**: `http://localhost:9000`
- **Network**: `http://<your-server-ip>:9000`

### First-Time Setup

1. Open the web interface
2. Create an administrator account
3. Add your first printer from the Machines page

### Reverse Proxy with Nginx

If you want to access Overseer on port 80 or with SSL, configure Nginx:

```nginx
server {
    listen 80;
    server_name overseer.local;

    location / {
        proxy_pass http://localhost:9000;
        proxy_http_version 1.1;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Host $host;
    }

    # WebSocket support for real-time updates
    location /push {
        proxy_pass http://localhost:9000/push;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Host $host;
    }
}
```

---

## Updating

### Docker

```bash
# Pull the latest image
docker pull ghcr.io/overseerapp/overseer:latest

# Restart the container
docker-compose down
docker-compose up -d

# Or without compose
docker stop overseer
docker rm overseer
docker run -d --name overseer -p 9000:9000 -v overseer-data:/app/data ghcr.io/overseerapp/overseer:latest
```

### Linux with systemd (Auto-Update)

Overseer supports automatic updates from the web interface:

1. Go to **Settings** > **About**
2. If an update is available, click **Install Update**
3. The application will automatically download, install, and restart

### Linux with systemd (Manual Update)

```bash
# Download the update script or new version
wget https://github.com/overseerapp/overseer/releases/download/VERSION/overseer_server_VERSION.zip

# Stop the service
sudo systemctl stop overseer

# Backup current installation (optional)
cp -r /path/to/overseer /path/to/overseer_backup

# Extract new version
unzip -o overseer_server_VERSION.zip -d /path/to/overseer

# Start the service
sudo systemctl start overseer
```

---

## Troubleshooting

### Application Won't Start

1. Check the logs:

   ```bash
   # systemd
   sudo journalctl -u overseer -n 50

   # Docker
   docker logs overseer
   ```

2. Verify .NET is installed:

   ```bash
   dotnet --list-runtimes
   ```

3. Check port availability:
   ```bash
   sudo netstat -tlnp | grep 9000
   ```

### Can't Connect to Printers

1. Verify network connectivity from the Overseer host:

   ```bash
   ping <printer-ip>
   curl http://<printer-ip>/api/version
   ```

2. For Docker, ensure proper network configuration (see [Accessing Printer Network](#accessing-printer-network))

### Database Issues

The database is stored in the application directory or Docker volume. To reset:

```bash
# systemd (Overseer stores files under the user profile, most likely root for the system service)
sudo systemctl stop overseer
sudo rm /root/overseer/*.db
sudo systemctl start overseer

# Docker
docker stop overseer
docker volume rm overseer-data
docker start overseer
```

### Permission Issues

Ensure the application has write access to its directory:

```bash
sudo chown -R $USER:$USER /path/to/overseer
```

---

## Support

- **Issues**: [GitHub Issues](https://github.com/overseerapp/overseer/issues)
- **Documentation**: [docs/](https://github.com/overseerapp/overseer/tree/main/docs)
