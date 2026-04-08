# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview

This is a Purview Activity Explorer application that emulates Microsoft Purview's Activity Explorer functionality. It consists of a .NET Core backend that integrates with Microsoft 365 via PowerShell and a React TypeScript frontend for displaying audit log data.

## Critical Commands

### Backend Commands
```bash
# Build and run the backend (from root directory)
cd backend/ActivityExplorer.API
dotnet restore
dotnet build
dotnet run

# Run all backend projects build
cd backend
dotnet build ActivityExplorer.sln

# Create EF Core migrations
cd backend/ActivityExplorer.API
dotnet ef migrations add MigrationName -p ../ActivityExplorer.Data -s .
dotnet ef database update
```

### Frontend Commands
```bash
# Install dependencies and start frontend (from root directory)
cd frontend/activity-explorer-ui
npm install
npm run dev

# Build for production
npm run build

# Preview production build
npm run preview
```

### Full Application Startup
```bash
# Terminal 1 - Backend
cd backend/ActivityExplorer.API && dotnet run

# Terminal 2 - Frontend (Vite dev server)
cd frontend/activity-explorer-ui && npm run dev
```

## Architecture Overview

### Backend Architecture (.NET 9.0)

The backend follows a layered architecture with clear separation of concerns:

1. **ActivityExplorer.API** - Web API layer
   - Controllers handle HTTP requests
   - Program.cs configures services, CORS, and database initialization
   - Uses port 5000 by default

2. **ActivityExplorer.Core** - Domain layer
   - Contains domain models (Activity, SavedFilter)
   - DTOs for data transfer (ActivityFilterDto, PagedResult)
   - No dependencies on other projects

3. **ActivityExplorer.Data** - Data access layer
   - ActivityContext (EF Core DbContext)
   - Handles database operations and migrations
   - Uses SQL Server with LocalDB by default

4. **ActivityExplorer.Services** - Business logic layer
   - PurviewService orchestrates PowerShell script execution
   - PowerShellRunner executes `pwsh.exe` out-of-process (PS1 scripts return JSON on stdout)
   - ActivitySyncService handles fetch + deduplicate + save workflow
   - Certificate-based authentication with Microsoft 365 via Connect-IPPSSession

5. **ActivityExplorer.SyncFunction** - Azure Function (isolated worker, .NET 9)
   - Timer trigger (every 6 hours) for scheduled sync
   - HTTP trigger for on-demand sync
   - Shares ActivitySyncService with API project

### Frontend Architecture (React 19 + TypeScript 5)

Single-page application built with Vite and Material-UI 9:

- **src/App.tsx** - Main component with table, filtering, sync, and saved filters
- **src/services/api.ts** - API service layer using axios
- Uses TanStack Query for server state (caching, auto-refetch, mutations)
- **src/components/ActivityDetailPanel.tsx** - Detail flyout panel
- Uses MUI 9 (Grid with `size` prop, Drawer with `slotProps`), date-fns 4
- Bundled with Vite (replaced CRA)

### PowerShell Integration (Out-of-Process)

PowerShell runs out-of-process via `pwsh.exe` — no in-process SDK, no assembly conflicts:

1. **PS1 Scripts** (`backend/scripts/`):
   - `Sync-Activities.ps1` — Connect + Export-ActivityExplorerData with pagination, outputs JSON array
   - `Get-AuthStatus.ps1` — Checks EXO module, certificate, connectivity, outputs JSON status
   - `Analyze-Columns.ps1` — Fetches sample data for column analysis

2. **PowerShellRunner.cs** — C# wrapper around `System.Diagnostics.Process`:
   - Runs `pwsh.exe -NoProfile -NonInteractive -File script.ps1`
   - Reads JSON from stdout, logs stderr, handles timeouts
   - Pre-flight checks: `CheckPwshAsync()` (is pwsh on PATH?), `CheckScripts()` (do PS1 files exist?)

3. **Authentication**: Certificate-based (CBA) via Azure AD app registration, handled entirely in PS1 scripts
   - Requires Exchange Online Management module 3.9.2+ and `pwsh.exe` (PowerShell 7+)

### Database Schema

SQL Server database managed via EF Core Migrations:
- **Activities** table - Stores audit log entries with indexes on Timestamp, UserId, Operation, Workload
- **SavedFilters** table - Stores user filter presets (prepared but not fully implemented)
- Uses LocalDB by default (no setup required)
- Schema changes via `dotnet ef migrations add` (not EnsureCreated)

## Configuration Requirements

Before running, update these configurations:

1. **backend/ActivityExplorer.API/appsettings.json**:
   - `Purview:Organization` - Set to your Microsoft 365 domain
   - `ConnectionStrings:ActivityDatabase` - Update if not using LocalDB

2. **Prerequisites**:
   - `pwsh.exe` (PowerShell 7+) must be on PATH
   - Exchange Online Management PowerShell module must be installed
   - User must have permissions to read audit logs in Microsoft 365

## API Endpoints

- `GET /api/activities` - Paginated activity list with filtering (workloads, operations, status, user, dates)
- `POST /api/activities/sync` - Triggers PowerShell sync from Purview
- `GET /api/activities/export/csv` - Exports filtered data to CSV
- `GET /api/activities/statistics` - Returns activity statistics
- `GET /api/activities/filter-options` - Returns distinct workloads, operations, statuses for filter dropdowns
- `GET /api/activities/columns` - Analyzes available columns from Purview
- `GET /api/filters` - List saved filters
- `POST /api/filters` - Save a filter preset
- `DELETE /api/filters/{id}` - Delete a saved filter

## Development Workflow

1. The database is created/migrated automatically on first run via `context.Database.Migrate()`
2. Certificate-based authentication connects automatically (no manual input needed)
3. Frontend expects backend on http://localhost:5000 (CORS origins configurable in appsettings.json)
4. Scalar API Reference available at http://localhost:5000/scalar/v1 for API testing
5. OpenAPI document at http://localhost:5000/openapi/v1.json

## Latest Updates (April 2026)

### Phase 1: Framework Upgrades
- **Backend**: .NET 8 → 9, PowerShell SDK 7.4 → 7.5, Swashbuckle → built-in OpenAPI + Scalar UI
- **Database**: `EnsureCreated()` → EF Core Migrations
- **Frontend**: CRA → Vite, TypeScript 4.9 → 5.6, MUI 5 → 9, React 18 → 19, date-fns 2 → 4
- **Package fix**: Aligned NuGet versions with EXO module 3.9.2 DLLs (MSAL 4.74.1, IdentityModel 8.14.0)

### Phase 2: Azure v2 Architecture
- **PowerShell out-of-process**: Replaced in-process `System.Management.Automation` with `pwsh.exe` + PS1 scripts
- **PS SDK removed**: 12 NuGet packages removed (~100MB savings), no more assembly version conflicts
- **Configurable CORS/URL**: Origins from appsettings.json, frontend API URL from env variable
- **Azure SQL ready**: `EnableRetryOnFailure()` for transient fault handling
- **Azure Function**: New `ActivityExplorer.SyncFunction` project (timer + HTTP triggers)
- **Shared sync logic**: `ActivitySyncService` used by both API controller and Azure Function
- **TanStack Query**: Frontend server state management with caching, auto-refetch, mutations
- **Saved Filters**: CRUD API + UI for saving/loading filter presets (workload, operation, status, user, dates)
- **Serilog**: Structured logging to console + rolling files (`logs/activity-explorer-*.log`, 14 day retention)

### August 2025 Features
1. **Complete Data Capture**: All 29 fields from Export-ActivityExplorerData are now stored
2. **Activity Detail Flyout Panel**: Click any activity row to view all fields in a side panel
3. **Column Analysis API**: Endpoint to discover available columns from Purview
4. **Enhanced Data Model**: Support for sensitivity labels, DLP policies, and complex metadata
5. **UI Improvements**: Clickable rows, detail side panel, copy-to-clipboard, JSON viewer, smart formatting

## Known Issues & Fixes

### Certificate Authentication
- **Issue**: "Klíč není platný pro použití v zadaném stavu" (The key is not valid for use in the specified state)
- **Cause**: Certificate was imported with strong private key protection
- **Solution**: Reimport certificate from PFX without the `-ProtectPrivateKey` flag:
  ```powershell
  $password = ConvertTo-SecureString -String "YourPassword" -Force -AsPlainText
  Import-PfxCertificate -FilePath "path\to\certificate.pfx" -CertStoreLocation Cert:\CurrentUser\My -Password $password -Exportable
  ```

### Database Schema Updates
- **Process**: Use EF Core Migrations for schema changes:
  1. `cd backend/ActivityExplorer.API`
  2. `dotnet ef migrations add MigrationName --project ../ActivityExplorer.Data`
  3. Restart application (auto-applies via `Database.Migrate()`)
- **For clean reset**: Drop DB and restart: `sqlcmd -S "(localdb)\mssqllocaldb" -Q "DROP DATABASE ActivityExplorer"`

## Certificate Configuration

- **Certificate Location**: `C:\Users\DrozdTomas(KPCSCZ)\OneDrive\Home_online\PowerShell\!Certs\ActivityExplorerEXO-CBAcert.pfx`
- **Certificate Thumbprint**: 2F333B7E2923CA32F699E977F2952423990878F7
- **App ID**: f88ad2f0-dd16-4b4f-ab8b-5061605bd939
- **Organization**: drozdovo.cz

## Known Limitations

- Certificate-based authentication only (no interactive auth)
- Scheduled syncing available via Azure Function (every 6 hours), manual trigger from UI
- Fetches only last 30 days of data per sync
- No real-time updates (requires manual refresh)
- Requires `pwsh.exe` (PowerShell 7+) on PATH for PowerShell integration