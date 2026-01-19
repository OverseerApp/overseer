# Development Guide

## Running the Application Locally

This guide covers how to run the Overseer application locally for development purposes.

### Prerequisites

- Node.js and npm (for the frontend)
- .NET SDK (for the backend)

### Frontend (Angular Client)

The frontend is an Angular application located in `src/Overseer.Client/`.

#### Initial Setup

Navigate to the client directory and install dependencies:

```bash
cd src/Overseer.Client
npm install
```

#### Running the Development Server

Start the Angular development server:

```bash
npm start
```

The application will be available at `http://localhost:4200/` by default. The dev server will automatically reload when you make changes to the source files.

#### Other Available Scripts

- `npm run build` - Build the project for production
- `npm run test` - Run unit tests
- `npm run lint` - Lint the codebase

### Backend (.NET Server)

The backend is a .NET application located in `src/Overseer.Server/`.

#### Running with Development Configuration

Navigate to the server directory:

```bash
cd src/Overseer.Server
```

Run the application with the development configuration using `dotnet watch`:

```bash
dotnet watch --Environment=Development
```

This will:

- Start the server with the Development configuration
- Use settings from `appsettings.Development.json`
- Automatically rebuild and restart when code changes are detected
- Enable detailed error pages and logging

The API will typically be available at `http://localhost:9000/`.

#### Alternative: Running without Watch Mode

If you don't need automatic rebuilding, you can run directly:

```bash
dotnet run --Environment=Development
```

#### Building the Solution

To build the entire solution:

```bash
dotnet build Overseer.Server.sln
```

#### Publishing for Deployment

To create a production build:

```bash
dotnet publish Overseer.Server.sln --configuration Release
```

### Running Both Frontend and Backend

For full local development, you'll need to run both the frontend and backend simultaneously:

1. In one terminal, start the backend:

   ```bash
   cd src/Overseer.Server
   dotnet watch --configuration Development
   ```

2. In another terminal, start the frontend:
   ```bash
   cd src/Overseer.Client
   npm start
   ```
