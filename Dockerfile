# Overseer Docker Image
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
# Update version in package.json and Angular environment file
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

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install OpenCV dependencies for Emgu.CV
RUN apt-get update && apt-get install -y --no-install-recommends \
  libopencv-dev \
  libgdiplus \
  libc6-dev \
  && rm -rf /var/lib/apt/lists/*

# Copy published .NET application
COPY --from=server-build /app/publish .

# Copy Angular build output
COPY --from=client-build /src/client/dist/overseer.client .

# Create data directory for LiteDB
RUN mkdir -p /app/data

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:9000
ENV DOTNET_PRINT_TELEMETRY_MESSAGE=false

# Expose the application port
EXPOSE 9000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:9000/api/configuration/info || exit 1

# Run the application
ENTRYPOINT ["dotnet", "Overseer.Server.dll"]
