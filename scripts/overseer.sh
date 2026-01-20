#!/bin/bash

# Overseer Install/Update Script
# This unified script handles both fresh installations and updates
# Usage:
#   Install: ./overseer.sh install [version]
#   Update:  ./overseer.sh update <version> [overseer_directory] [dotnet_path]

set -e

# =============================================================================
# Configuration
# =============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEFAULT_VERSION='2.0.0-alpha.1'
OVERSEER_EXECUTABLE='Overseer.Server.dll'
SERVICE_NAME='overseer'
SERVICE_PATH='/lib/systemd/system/overseer.service'
GITHUB_RELEASES_URL='https://github.com/michaelfdeberry/overseer/releases/download'
LOG_FILE='/var/log/overseer.log'

# =============================================================================
# Logging Functions
# =============================================================================

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

log_error() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] ERROR: $1" | tee -a "$LOG_FILE" >&2
}

# =============================================================================
# Dependency Installation Functions
# =============================================================================

update_system_packages() {
    log "Updating system packages..."
    
    # Wait for any existing apt processes to complete
    log "Waiting for any existing apt processes to complete..."
    while fuser /var/lib/apt/lists/lock 2>/dev/null; do
        sleep 2
    done
    
    # Clear APT cache to resolve potential corruption
    log "Clearing APT cache..."
    apt-get clean || true
    rm -rf /var/lib/apt/lists/* || true
    
    # Retry apt-get update with exponential backoff
    local max_retries=3
    local retry=0
    
    while [ $retry -lt $max_retries ]; do
        log "Attempting apt-get update (attempt $((retry+1))/$max_retries)..."
        if apt-get update; then
            log "System packages updated successfully"
            return 0
        fi
        
        retry=$((retry+1))
        if [ $retry -lt $max_retries ]; then
            local wait_time=$((2 ** retry))
            log "apt-get update failed, retrying in ${wait_time}s..."
            sleep $wait_time
        fi
    done
    
    log_error "Failed to update system packages after $max_retries attempts"
    return 1
}

install_dependencies() {
    local dotnetPath=${1:-${PWD}'/.dotnet'}
    local dotnetExecPath=${dotnetPath}'/dotnet'

    # Always install .NET runtime dependencies
    log "Installing .NET runtime dependencies..."
    apt-get install -y wget apt-transport-https software-properties-common unzip
    apt-get install -y libc6 libgcc-s1 libgssapi-krb5-2 libstdc++6 zlib1g
    
    # Install ICU libraries (try multiple versions for compatibility)
    apt-get install -y libicu-dev || apt-get install -y libicu72 || apt-get install -y libicu70 || apt-get install -y libicu67
    
    # Install SSL libraries (try multiple versions for compatibility)
    apt-get install -y libssl-dev || apt-get install -y libssl3 || apt-get install -y libssl1.1
         
    # Install additional libraries that may be needed by OpenCV
    apt-get install -y \
        libgtk2.0-dev \
        pkg-config \
        libavcodec-dev \
        libavformat-dev \
        libswscale-dev

    # Always install/update .NET to ensure latest version is available
    log "Installing/Updating .NET runtime..."
    wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh 
    chmod +x ./dotnet-install.sh 
    ./dotnet-install.sh --version latest --runtime aspnetcore --install-dir ${dotnetPath}
    rm -f ./dotnet-install.sh
    
    log "Installing/Updating OpenCV dependencies..."
    apt-get install -y \
        libopencv-dev \
        libgdiplus \
        libc6-dev \
        libgtk2.0-dev \
        pkg-config \
        libavcodec-dev \
        libavformat-dev \
        libswscale-dev

    return 0 
}

verify_dependencies() {
    local dotnetPath=${1:-${PWD}'/.dotnet'}
    local dotnetExecPath=${dotnetPath}'/dotnet'
    
    if [ ! -f "$dotnetExecPath" ]; then
        log_error ".NET runtime not found at $dotnetExecPath"
        return 1
    fi
    
    log "Dependencies verified successfully."
    return 0
}

# =============================================================================
# Common Functions
# =============================================================================

check_root() {
    if [ "$EUID" -ne 0 ]; then
        log_error "This script must be run as root (sudo)"
        exit 1
    fi
}

stop_overseer() {
    log "Attempting to stop Overseer service..."
    
    # First, check if service file exists and get process info
    if [ -f "$SERVICE_PATH" ]; then
        log "Service file found at $SERVICE_PATH"
        
        # Check if systemctl can be used (with timeout to prevent hanging)
        if command -v systemctl &> /dev/null; then
            log "Checking if service is active..."
            
            # Use timeout to prevent systemctl from hanging
            if timeout 5 systemctl is-active --quiet $SERVICE_NAME 2>&1; then
                log "Service is active. Stopping Overseer service..."
                
                # Stop the service without blocking (returns immediately)
                log "Executing systemctl stop command (non-blocking)..."
                if systemctl stop $SERVICE_NAME --no-block 2>&1 | tee -a "$LOG_FILE"; then
                    log "Stop command sent successfully (non-blocking)"
                else
                    local stop_exit=$?
                    log_error "systemctl stop command failed (exit code: $stop_exit)"
                    
                    # Try with timeout as fallback
                    log "Attempting stop with timeout as fallback..."
                    if timeout 10 systemctl stop $SERVICE_NAME 2>&1 | tee -a "$LOG_FILE"; then
                        log "Stop with timeout succeeded"
                    else
                        log_error "Stop with timeout also failed (exit code: $?)"
                        return 1
                    fi
                fi
                
                # Wait for the service to fully stop with more detailed logging
                local max_wait=30
                local waited=0
                while [ $waited -lt $max_wait ]; do
                    if ! timeout 5 systemctl is-active --quiet $SERVICE_NAME 2>&1; then
                        log "Service confirmed stopped after ${waited}s"
                        return 0
                    fi
                    log "Waiting for service to stop... ($waited/${max_wait}s)"
                    sleep 1
                    waited=$((waited + 1))
                done
                
                log_error "Failed to stop service within $max_wait seconds"
                
                # Try force kill as last resort
                log "Attempting force kill..."
                systemctl kill -s SIGKILL $SERVICE_NAME 2>&1 || true
                sleep 2
                
                return 1
            else
                local status_exit=$?
                log "Service is not active (status check exit code: $status_exit)"
            fi
        else
            log "systemctl command not available, skipping systemd stop"
        fi
    else
        log "Service file not found at $SERVICE_PATH"
    fi
    
    # Also check for any orphaned processes by PID
    log "Checking for orphaned Overseer processes..."
    local overseerPID=$(ps auxf 2>/dev/null | grep "${OVERSEER_EXECUTABLE}" | grep -v grep | awk '{print $2}' | head -1)
    
    if [ -n "${overseerPID}" ]; then
        log "Found Overseer process with PID: $overseerPID"
        if kill -0 "${overseerPID}" 2>/dev/null; then
            log "Process is running. Sending SIGTERM to PID $overseerPID"
            if kill -TERM "${overseerPID}" 2>/dev/null; then
                log "SIGTERM sent successfully"
                # Give it time to gracefully exit
                sleep 2
                
                # Check if it's still running
                if kill -0 "${overseerPID}" 2>/dev/null; then
                    log_error "Process still running after SIGTERM, sending SIGKILL"
                    kill -KILL "${overseerPID}" 2>/dev/null || true
                    sleep 1
                    log "SIGKILL sent"
                else
                    log "Process terminated gracefully"
                fi
            else
                log_error "Failed to send SIGTERM to process $overseerPID"
                return 1
            fi
        else
            log "Process $overseerPID is no longer running"
        fi
    else
        log "No Overseer processes found"
    fi
    
    return 0
}

start_overseer() {
    log "Starting Overseer service..."
    
    systemctl daemon-reload
    systemctl start $SERVICE_NAME
    
    # Wait for service to start
    sleep 3
    
    if systemctl is-active --quiet $SERVICE_NAME; then
        log "Service started successfully"
        return 0
    else
        log_error "Failed to start service"
        systemctl status $SERVICE_NAME --no-pager || true
        return 1
    fi
}

download_overseer() {
    local version=$1
    local overseerDirectory=$2
    local overseer_zip_file="overseer_server_${version}.zip"
    local download_url="${GITHUB_RELEASES_URL}/${version}/${overseer_zip_file}"
    local temp_dir=$(mktemp -d)
    
    log "Downloading Overseer ${version} from $download_url..."
    
    cd "$temp_dir"
    
    if ! wget -q --show-progress "$download_url" -O "$overseer_zip_file"; then
        log_error "Failed to download Overseer"
        rm -rf "$temp_dir"
        return 1
    fi
    
    log "Download complete. Extracting..."
    
    # Create directory if it doesn't exist
    mkdir -p "$overseerDirectory"
    
    # Extract to the overseer directory (overwriting existing files)
    if ! unzip -o "$overseer_zip_file" -d "$overseerDirectory"; then
        log_error "Failed to extract Overseer"
        rm -rf "$temp_dir"
        return 1
    fi
    
    log "Extraction complete"
    
    # Cleanup
    rm -rf "$temp_dir"
    
    return 0
}

create_service_file() {
    local overseerDirectory=$1
    local dotnetPath=$2
    local enableService=${3:-true}
    local dotnetExecPath=${dotnetPath}'/dotnet'
    local overseerExecutablePath=${overseerDirectory}'/'${OVERSEER_EXECUTABLE}
    
    log "Creating systemd service file..."
    
    > $SERVICE_PATH
    echo [Unit] >> $SERVICE_PATH
    echo Description=Overseer Daemon >> $SERVICE_PATH
    echo >> $SERVICE_PATH
    echo [Service] >> $SERVICE_PATH
    echo WorkingDirectory=${overseerDirectory} >> $SERVICE_PATH
    echo ExecStart=${dotnetExecPath} ${overseerExecutablePath} >> $SERVICE_PATH
    echo Restart=always >> $SERVICE_PATH
    echo RestartSec=10 >> $SERVICE_PATH
    echo KillSignal=SIGINT >> $SERVICE_PATH
    echo SyslogIdentifier=overseer >> $SERVICE_PATH 
    echo Environment=ASPNETCORE_ENVIRONMENT=Production >> $SERVICE_PATH
    echo Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false >> $SERVICE_PATH
    echo >> $SERVICE_PATH
    echo [Install] >> $SERVICE_PATH
    echo WantedBy=multi-user.target >> $SERVICE_PATH
    echo >> $SERVICE_PATH
    
    log "Service file created at $SERVICE_PATH"
    if [ "$enableService" == "true" ]; then
        log "Enabling service to start on boot..."
        systemctl enable overseer
        log "Service enabled"
    else
        log "Service file created (systemctl not available during build)"
    fi
}

# =============================================================================
# Backup/Restore Functions (for updates)
# =============================================================================

create_backup() {
    local overseerDirectory=$1
    local backup_dir="${overseerDirectory}_backup_$(date +%Y%m%d_%H%M%S)"
    
    if [ -d "$overseerDirectory" ]; then
        log "Creating backup at $backup_dir..."
        cp -r "$overseerDirectory" "$backup_dir"
        log "Backup created successfully"
        echo "$backup_dir"
    else
        log "No existing installation to backup"
        echo ""
    fi
}

restore_backup() {
    local backup_dir=$1
    local overseerDirectory=$2
    
    if [ -n "$backup_dir" ] && [ -d "$backup_dir" ]; then
        log "Restoring from backup..."
        rm -rf "$overseerDirectory"
        mv "$backup_dir" "$overseerDirectory"
        log "Backup restored"
    fi
}

cleanup_old_backups() {
    local overseerDirectory=$1
    local backup_pattern="${overseerDirectory}_backup_*"
    local backups=($(ls -dt ${backup_pattern} 2>/dev/null || true))
    local keep_count=3
    
    if [ ${#backups[@]} -gt $keep_count ]; then
        log "Cleaning up old backups..."
        for ((i=$keep_count; i<${#backups[@]}; i++)); do
            log "Removing old backup: ${backups[$i]}"
            rm -rf "${backups[$i]}"
        done
    fi
}

# =============================================================================
# Nginx Configuration (for install)
# =============================================================================
 
configure_nginx() {
    local externalPort=${1:-80}
    local autoInstall=${2:-false}
    
    if [ ! -f /etc/nginx/sites-available/overseer ]; then
        local installNginx="y"
        
        # Only prompt if not in auto-install mode (e.g., when called from do_configure for Docker)
        if [ "$autoInstall" != "true" ]; then
            read -p "Do you want to install a reverse proxy with Nginx? (y/n): " installNginx
        fi
    else
        log "Nginx configuration for Overseer already exists. Skipping."
        return 0
    fi

    if [ "$installNginx" == "y" ]; then
        # Install nginx if needed
        if ! command -v nginx &> /dev/null; then
            log "Installing Nginx..."
            apt-get install -y nginx
        else
            log "Nginx is already installed."
        fi

        # If not in auto-install mode, prompt for port
        if [ "$autoInstall" != "true" ]; then
            read -p "Enter the external port for Nginx (default is 80): " userPort
            externalPort=${userPort:-80}
        fi
        local externalPort=${1:-80}
        local nginxConfigPath='/etc/nginx/sites-available/overseer'
        
        log "Creating Nginx configuration..."
        
        > $nginxConfigPath
        echo server { >> $nginxConfigPath
        echo '    listen ' ${externalPort}';' >> $nginxConfigPath 
        echo '    location / {' >> $nginxConfigPath
        echo '        proxy_pass http://localhost:9000;' >> $nginxConfigPath
        echo '        proxy_http_version 1.1;' >> $nginxConfigPath
        echo '        proxy_cache_bypass $http_upgrade;' >> $nginxConfigPath
        echo '        proxy_set_header Upgrade $http_upgrade;' >> $nginxConfigPath
        echo '        proxy_set_header Connection keep-alive;' >> $nginxConfigPath
        echo '        proxy_set_header X-Forwarded-Proto $scheme;' >> $nginxConfigPath
        echo '        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;' >> $nginxConfigPath
        echo '        proxy_set_header Host $host;' >> $nginxConfigPath
        echo '    }' >> $nginxConfigPath
        echo '' >> $nginxConfigPath
        echo '    location /push {' >> $nginxConfigPath
        echo '        proxy_pass              http://localhost:9000/push;' >> $nginxConfigPath
        echo '        proxy_http_version      1.1;' >> $nginxConfigPath
        echo '        proxy_cache_bypass      $http_upgrade;' >> $nginxConfigPath
        echo '        proxy_set_header        Upgrade $http_upgrade;' >> $nginxConfigPath
        echo '        proxy_set_header        Connection "upgrade";' >> $nginxConfigPath
        echo '        proxy_set_header        X-Forwarded-For $proxy_add_x_forwarded_for;' >> $nginxConfigPath
        echo '        proxy_set_header        X-Forwarded-Proto $scheme;' >> $nginxConfigPath
        echo '        proxy_set_header        Host $host;' >> $nginxConfigPath
        echo '    }' >> $nginxConfigPath
        echo } >> $nginxConfigPath
        
        # Enable the configuration
        ln -sf $nginxConfigPath /etc/nginx/sites-enabled/
        rm -f /etc/nginx/sites-enabled/default

        # test the nginx configuration
        nginx -t

        if [ "$autoInstall" != "true" ]; then
            # restart nginx to apply the changes
            systemctl restart nginx
        fi
        
        log "Nginx configuration created"    

        echo ""
        echo "-----------------------------------------------------------------------------------------------------"
        echo ""
        log "Nginx has been installed and configured as a reverse proxy."
        echo "This only provides a basic configuration. You may need to make additional changes to suit your needs."
    else
        log "Nginx installation skipped."
    fi
}

# =============================================================================
# Install Command
# =============================================================================

do_install() {
    local version=${1:-$DEFAULT_VERSION}
    local dotnetPath=${PWD}'/.dotnet'
    local overseerDirectory=${PWD}'/overseer'
    
    log "=========================================="
    log "Overseer Installation Starting"
    log "Version: $version"
    log "=========================================="
    
    check_root
    
    # Update system and install dependencies
    update_system_packages
    install_dependencies "$dotnetPath"
    
    # Stop any existing installation
    stop_overseer
    
    # Create directory if needed
    if [ ! -d "$overseerDirectory" ]; then
        mkdir -p $overseerDirectory
        log "Created directory $overseerDirectory"
    fi
    
    # Download and extract
    if ! download_overseer "$version" "$overseerDirectory"; then
        log_error "Installation failed during download"
        exit 1
    fi
    
    # Create/update service file
    create_service_file "$overseerDirectory" "$dotnetPath"
    
    # Start the service
    if ! start_overseer; then
        log_error "Failed to start Overseer after installation"
        exit 1
    fi
    
    # Configure nginx (optional, interactive)
    configure_nginx
    
    log "=========================================="
    log "Overseer Installation Complete"
    log "=========================================="
}

# =============================================================================
# Update Command
# =============================================================================

do_update() {
    local version=$1
    local overseerDirectory=${2:-${PWD}'/overseer'}
    local dotnetPath=${3:-${PWD}'/.dotnet'}
    
    if [ -z "$version" ]; then
        log_error "Version parameter is required for update"
        echo "Usage: $0 update <version> [overseer_directory] [dotnet_path]"
        echo "Example: $0 update 2.0.1 /opt/overseer /opt/.dotnet"
        exit 1
    fi
    
    log "=========================================="
    log "Overseer Update Starting"
    log "Version: $version"
    log "=========================================="
    
    check_root
    
    # Verify service exists for updates
    if [ ! -f "$SERVICE_PATH" ]; then
        log_error "Overseer service not found. Use 'install' for fresh installations."
        exit 1
    fi
    
    # Create backup before making changes
    log "Creating backup of existing installation..."
    local backup_dir=$(create_backup "$overseerDirectory")
    log "Backup directory: $backup_dir"
    
    # Stop the service
    log "Stopping existing Overseer service..."
    if ! stop_overseer; then
        log_error "Failed to stop Overseer service"
        if [ -n "$backup_dir" ] && [ -d "$backup_dir" ]; then
            log "Would restore from backup: $backup_dir"
        fi
        exit 1
    fi
    log "Service stopped successfully"
    
    # Update dependencies
    log "Updating system packages..."
    update_system_packages
    
    log "Installing/updating dependencies..."
    install_dependencies "$dotnetPath" 
    
    log "Verifying dependencies..."
    if ! verify_dependencies "$dotnetPath"; then
        log_error "Dependency verification failed"
        log "Restoring from backup and attempting to restart service..."
        restore_backup "$backup_dir" "$overseerDirectory"
        start_overseer
        exit 1
    fi
    log "Dependencies verified successfully"
    
    # Download and install the update
    log "Downloading Overseer version $version..."
    if ! download_overseer "$version" "$overseerDirectory"; then
        log_error "Update download/install failed"
        log "Restoring from backup and attempting to restart service..."
        restore_backup "$backup_dir" "$overseerDirectory"
        log "Starting service with previous version..."
        start_overseer
        exit 1
    fi
    log "Download and extraction completed successfully"
    
    # Start the service
    log "Starting Overseer service with new version..."
    if ! start_overseer; then
        log_error "Failed to start service after update"
        log "Restoring from backup and attempting to restart service..."
        restore_backup "$backup_dir" "$overseerDirectory"
        log "Starting service with previous version..."
        start_overseer
        exit 1
    fi
    log "Service started successfully with version $version"
    
    # Clean up old backups
    log "Cleaning up old backups..."
    cleanup_old_backups "$overseerDirectory"
    
    if [ -n "$backup_dir" ] && [ -d "$backup_dir" ]; then
        log "Update successful, keeping backup for safety: $backup_dir"
    fi
    
    log "=========================================="
    log "Overseer Update Complete - Version $version"
    log "=========================================="
}

# =============================================================================
# Configure Command (for Docker builds - installs dependencies and creates config files)
# =============================================================================


do_configure() {
    local overseerDirectory=${1:-'/opt/overseer/overseer'}
    local dotnetPath=${2:-'/usr/bin'}
    local nginxPort=${3:-80}
    local skipNginx=${4:-"false"}
    
    log "=========================================="
    log "Overseer Configure (Docker Build Setup)"
    log "Directory: $overseerDirectory"
    log "Dotnet Path: $dotnetPath"
    log "=========================================="
    
    # Install system dependencies
    update_system_packages
    install_dependencies "$dotnetPath" 
    
    # Verify the application exists
    local overseerExecutablePath="${overseerDirectory}/${OVERSEER_EXECUTABLE}"
    if [ ! -f "$overseerExecutablePath" ]; then
        log_error "Overseer executable not found at $overseerExecutablePath"
        exit 1
    fi
    
    # Create service file (without enabling - can't run systemctl during Docker build)
    create_service_file "$overseerDirectory" "$dotnetPath" "false"
    
    # Create nginx config
    configure_nginx "$nginxPort" "true"
    
    log "=========================================="
    log "Overseer Configuration Complete"
    log "Note: Services not started (use 'systemctl enable overseer nginx' at runtime)"
    log "=========================================="
}

# =============================================================================
# Main Entry Point
# =============================================================================

show_usage() {
    echo "Overseer Install/Update Script"
    echo ""
    echo "Usage: $0 <command> [options]"
    echo ""
    echo "Commands:"
    echo "  install [version]                          Fresh installation (default version: $DEFAULT_VERSION)"
    echo "  update <version> [directory] [dotnet_path] Update existing installation"    
    echo "  configure [directory] [dotnet_path] [nginx_port] Create config files only (for Docker)"
    echo ""
    echo "Examples:"
    echo "  $0 install                    # Install default version"
    echo "  $0 install 2.0.1              # Install specific version"
    echo "  $0 update 2.0.1               # Update to version 2.0.1"
    echo "  $0 update 2.0.1 /opt/overseer # Update with custom directory" 
    echo "  $0 configure /opt/overseer/overseer /opt/overseer/.dotnet 80  # Create config files for Docker"
    echo ""
}

main() {
    local command=${1:-""}
    
    case "$command" in
        install)
            shift
            do_install "$@"
            ;;
        update)
            shift
            do_update "$@"
            ;; 
        configure)
            shift
            do_configure "$@"
            ;;
        -h|--help|help)
            show_usage
            ;;
        *)
            if [ -z "$command" ]; then
                show_usage
            else
                log_error "Unknown command: $command"
                show_usage
                exit 1
            fi
            ;;
    esac
}

main "$@"
