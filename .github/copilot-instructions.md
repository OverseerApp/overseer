# Overseer - AI Agent Instructions

Overseer is a 3D printer monitoring application with support for Octoprint, RepRapFirmware, Elegoo, and Bambu Labs machines. It provides real-time monitoring, control, and AI-powered failure detection.

## Architecture

**Full-stack architecture:**

- **Frontend**: Angular 21 SPA (`src/Overseer.Client`) - standalone application at [overseer.live](https://overseer.live) or served by server
- **Backend**: ASP.NET Core 10.0 Minimal API (`src/Overseer.Server`) with SignalR for real-time updates
- **Database**: LiteDB for all persistent data (machines, users, settings) stored in user profile directory (`~/Overseer.db`)

**Key design pattern - Provider-based machine abstraction:**

- Each printer type has a `Machine` model (e.g., `OctoprintMachine`) and corresponding `IMachineProvider<T>` implementation
- Providers inherit from either `PollingMachineProvider<T>` (REST-based like Octoprint, RepRapFirmware), `WebSocketMachineProvider<T>` (WebSocket-based like Moonraker, Elegoo), or `MachineProvider<T>` directly (e.g., Bambu uses MQTT via MQTTnet)
- `MachineProviderManager` dynamically instantiates providers using reflection and caches them by machine ID
- See [Machines/MachineProviderManager.cs](../src/Overseer.Server/Machines/MachineProviderManager.cs) for the factory pattern

**SignalR Channels for real-time communication:**

- `ChannelBase<T>` implements pub-sub pattern with concurrent dictionary of subscribers
- `MachineStatusChannel`, `NotificationChannel`, `JobFailureChannel` broadcast updates to connected clients
- SignalR hubs at `/push/status` and `/push/notifications` for bi-directional real-time communication

## Development Workflows

**Running locally (dual-mode development):**

```powershell
# Quick start both frontend and backend:
./src/start-dev.ps1

# Or manually:
# Terminal 1 - Backend (port 9000):
cd src/Overseer.Server
dotnet watch --Environment=Development

# Terminal 2 - Frontend (port 4200):
cd src/Overseer.Client
npm install  # First time only
npm start
```

**Frontend proxies API calls to backend in development** - check `proxy.conf.json` for proxy configuration to avoid CORS issues.

**Building and running with Docker/Podman:**

```bash
# Multi-stage build creates optimized image
docker build -t overseer:latest .
docker run --rm -d -p 80:80 -p 9000:9000 overseer:latest
```

**Available VS Code tasks** (`.vscode/tasks.json` not shown, but referenced in workspace):

- `build` - Builds the .NET solution
- `publish` - Publishes release artifacts
- `watch` - Runs with hot reload

## Project Conventions

**API registration pattern:**

- APIs are static classes with extension methods like `MapMachineApi(this RouteGroupBuilder)`
- All registered in [Api/OverseerApi.cs](../src/Overseer.Server/Api/OverseerApi.cs) via `MapOverseerApi()` called from `Program.cs`
- Routes grouped under `/api` prefix with role-based authorization (`Readonly` or `Administrator`)

**Custom authentication:**

- Token-based via `OverseerAuthenticationHandler` - checks `Authorization` header or `access_token` query param
- Two roles: `Readonly` (view-only) and `Administrator` (full control)
- See [Authentication.cs](../src/Overseer.Server/Authentication.cs) for implementation

**Dependency injection setup:**

- All services registered in `AddOverseerDependencies()` in [Extensions.cs](../src/Overseer.Server/Extensions.cs)
- Uses factory functions for dynamic provider instantiation: `Func<Machine, IMachineProvider>`
- Background services (hosted services) handle continuous monitoring and notifications

**Database access:**

- `LiteDataContext` provides repository pattern via `Repository<T>()` for entities
- `IValueStore` for singleton settings (e.g., `ApplicationSettings`)
- Database auto-migrates on startup via `UpdateManager.Update()` in `Program.cs`
- Located at `~/Overseer.db` (moves from app directory to user profile on first run)

## Critical Features

**PrintGuard AI failure detection:**

- Located in [Automation/PrintGuard/](../src/Overseer.Server/Automation/PrintGuard/)
- Uses ONNX Runtime with ShuffleNet + prototypical networks for few-shot learning
- Detects spaghetti prints, layer shifts via camera feed analysis
- Based on research by Oliver Bravery - see PrintGuard README for full attribution and architecture details
- Configured per-machine with camera URL and sensitivity thresholds

**Adding new printer types:**

1. Create machine model inheriting from `Machine` (optionally implement `IPollingMachine` or `IWebSocketMachine`)
2. Create provider inheriting from `PollingMachineProvider<T>` or `WebSocketMachineProvider<T>`
3. Implement abstract methods: `UpdateStatus()`, control methods (`PauseJob()`, etc.)
4. Provider auto-discovered via reflection in `MachineProviderManager`

## Common Patterns

**Extension method utilities:**

- `GetAssignableTypes()` - Reflection helper to find all concrete implementations of interface/base class in assembly
- `AddOrReplace()` - Dictionary helper for upsert operations
- `DoNotAwait()` - Fire-and-forget task pattern (use sparingly)

**Error handling:**

- `HandleOverseerExceptions()` middleware in [Extensions.cs](../src/Overseer.Server/Extensions.cs) provides global exception handling
- Frontend has `ErrorHandlerService` with custom HTTP interceptor

**Logging:**

- Backend uses log4net configured via [log4net.config](../src/Overseer.Server/log4net.config)
- Console logging enabled for Docker/container environments

## Integration Points

**Frontend-Backend API contract:**

- Frontend makes HTTP calls via Angular services (e.g., `MachinesService`, `ControlService`)
- SignalR connections for real-time status updates via `MonitoringService` and `NotificationService`
- Authentication token stored in browser and passed in headers/query params

**Machine provider communication:**

- Each provider type uses different protocols: REST (Octoprint, RRF), WebSocket (Moonraker, Elegoo), MQTT (Bambu via MQTTnet)
- Providers handle connection management, auto-reconnect, and polling intervals
- Status updates pushed through `IMachineStatusChannel` to SignalR clients

**Container deployment:**

- Multi-stage Dockerfile builds client and server separately, then combines in final nginx + .NET image
- Version injected at build time via `ARG VERSION`
- Database persists via volume mount at `/root/Overseer.db`
