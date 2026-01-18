# Overseer Docker Image
# Multi-stage build for optimized image size

# Build stage for Angular client
FROM node:20-alpine AS client-build
WORKDIR /src/client
COPY src/Overseer.Client/package*.json ./
RUN npm ci
COPY src/Overseer.Client/ ./
RUN npm run build

# Build stage for .NET server
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server-build
WORKDIR /src
COPY src/Overseer.Server/*.csproj ./Overseer.Server/
COPY src/Overseer/*.csproj ./Overseer/
WORKDIR /src/Overseer.Server
RUN dotnet restore
WORKDIR /src
COPY src/Overseer.Server/ ./Overseer.Server/
COPY src/Overseer/ ./Overseer/
WORKDIR /src/Overseer.Server
RUN dotnet publish -c Release -o /app/publish --no-restore

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
COPY --from=client-build /src/client/dist/overseer.client ./wwwroot

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
