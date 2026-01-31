# Overseer Docker Image with Systemd Support
# This image supports auto-updates similar to native Linux installations
# Multi-stage build for optimized image size

# Accept version as build argument (defaults to dev version for local builds)
ARG VERSION=2.0.0-dev

# Build stage for Angular client
FROM node:20-alpine AS client-build
ARG VERSION=2.0.0-dev
WORKDIR /src/client
COPY src/Overseer.Client/package*.json ./
RUN npm ci --legacy-peer-deps 
COPY src/Overseer.Client/ ./
RUN npm version ${VERSION} --no-git-tag-version 
RUN sed -i "s/appVersion: '.*'/appVersion: '${VERSION}'/" src/environments/versions.ts
RUN npm run build

# Build stage for .NET server
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server-build
ARG VERSION=2.0.0-dev
WORKDIR /src
COPY src/Overseer.Server/*.csproj ./Overseer.Server/
WORKDIR /src/Overseer.Server
RUN dotnet restore Overseer.Server.csproj
WORKDIR /src
COPY src/Overseer.Server/ ./Overseer.Server/ 
WORKDIR /src/Overseer.Server
RUN dotnet build Overseer.Server.csproj -c Release -o /app/publish --no-restore /p:Version=${VERSION}

# Final runtime image with systemd support
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Set up application directory structure (matching manual installation)
# Manual install uses ${PWD} as base, we use /opt as the equivalent
WORKDIR /opt

# Define paths matching manual install defaults
ENV DOTNET_PATH=/opt/.dotnet
ENV DOTNET_EXEC_PATH=/opt/.dotnet/dotnet
ENV OVERSEER_DIR=/opt/overseer

# Create directories matching manual install structure
RUN mkdir -p ${OVERSEER_DIR} ${DOTNET_PATH}

# Copy published .NET application
COPY --from=server-build /app/publish ${OVERSEER_DIR}/

# Copy Angular build output to the application directory
COPY --from=client-build /src/client/dist/overseer.client ${OVERSEER_DIR}/

# Copy the setup script and use it to create config files
# Convert Windows line endings to Unix format during copy
COPY scripts/overseer.sh ./overseer/overseer.sh
RUN sed -i 's/\r$//' ./overseer/overseer.sh && chmod +x ./overseer/overseer.sh

# Use the script's configure command to install dependencies and create config files
RUN ./overseer/overseer.sh configure ${OVERSEER_DIR} ${DOTNET_PATH} 80
# Enable services to start automatically when container starts
RUN systemctl enable overseer nginx

# Expose ports (80 for nginx, 9000 for direct access)
EXPOSE 80 9000

# Volume for persistent data (database is stored in /root/ by LiteDataContext)
VOLUME ["/root", "/sys/fs/cgroup"]

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_PRINT_TELEMETRY_MESSAGE=false
ENV container=docker
# Add .dotnet to PATH so dotnet command works from anywhere
ENV PATH="${DOTNET_PATH}:${PATH}"

# Health check using systemctl to verify the overseer service is running
# This is more appropriate than HTTP checks since systemd manages the service lifecycle
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
  CMD systemctl is-active overseer || exit 1

# Start systemd as init
STOPSIGNAL SIGRTMIN+3
ENTRYPOINT ["/lib/systemd/systemd"]
