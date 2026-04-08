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
   - PurviewService handles PowerShell integration
   - Uses System.Management.Automation for PowerShell commands
   - Certificate-based authentication with Microsoft 365 via Connect-IPPSSession

### Frontend Architecture (React 19 + TypeScript 5)

Single-page application built with Vite and Material-UI 9:

- **src/App.tsx** - Main component with table, filtering, and sync functionality
- **src/services/api.ts** - API service layer using axios
- **src/components/ActivityDetailPanel.tsx** - Detail flyout panel
- Uses MUI 9 (Grid with `size` prop, Drawer with `slotProps`), date-fns 4
- Bundled with Vite (replaced CRA)

### PowerShell Integration

The application uses PowerShell to fetch data from Microsoft Purview:

1. **Authentication Flow**:
   - Certificate-based authentication (CBA) via Azure AD app registration
   - Connects using `Connect-IPPSSession` (Security & Compliance PowerShell)
   - Requires Exchange Online Management module 3.9.2+

2. **Data Fetching**:
   - Uses `Export-ActivityExplorerData` to retrieve activity data
   - Fetches last 30 days of data by default
   - Processes PSObject results into Activity entities

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
   - Exchange Online Management PowerShell module must be installed
   - User must have permissions to read audit logs in Microsoft 365

## API Endpoints

- `GET /api/activities` - Paginated activity list with filtering
- `POST /api/activities/sync` - Triggers PowerShell sync from Purview
- `GET /api/activities/export/csv` - Exports filtered data to CSV
- `GET /api/activities/statistics` - Returns activity statistics

## Development Workflow

1. The database is created/migrated automatically on first run via `context.Database.Migrate()`
2. Certificate-based authentication connects automatically (no manual input needed)
3. Frontend expects backend on http://localhost:5000 (CORS configured)
4. Scalar API Reference available at http://localhost:5000/scalar/v1 for API testing
5. OpenAPI document at http://localhost:5000/openapi/v1.json

## Latest Updates (April 2026)

### Phase 1: Framework Upgrades
- **Backend**: .NET 8 → 9, PowerShell SDK 7.4 → 7.5, Swashbuckle → built-in OpenAPI + Scalar UI
- **Database**: `EnsureCreated()` → EF Core Migrations
- **Frontend**: CRA → Vite, TypeScript 4.9 → 5.6, MUI 5 → 9, React 18 → 19, date-fns 2 → 4
- **Package fix**: Aligned NuGet versions with EXO module 3.9.2 DLLs (MSAL 4.74.1, IdentityModel 8.14.0)

### August 2025 Features
1. **Complete Data Capture**: All 29 fields from Export-ActivityExplorerData are now stored
2. **Activity Detail Flyout Panel**: Click any activity row to view all fields in a side panel
3. **Column Analysis API**: Endpoint to discover available columns from Purview
4. **Enhanced Data Model**: Support for sensitivity labels, DLP policies, and complex metadata
5. **UI Improvements**: Clickable rows, detail side panel, copy-to-clipboard, JSON viewer, smart formatting

## Known Issues & Fixes

### Assembly Version Conflicts with EXO Module
- **Issue**: `Could not load file or assembly 'Microsoft.Identity.Client'` (or `System.IdentityModel.Tokens.Jwt`) at runtime
- **Cause**: Exchange Online Management module loads its own DLLs in-process; versions must match backend NuGet packages
- **Solution**: Check DLL versions in `<EXO module path>/netCore/` and align NuGet packages in ActivityExplorer.Services.csproj
- **Important**: When upgrading the EXO module, always verify and update matching NuGet package versions

### Certificate Authentication
- **Issue**: "Klíč není platný pro použití v zadaném stavu" (The key is not valid for use in the specified state)
- **Cause**: Certificate was imported with strong private key protection
- **Solution**: Reimport certificate from PFX without the `-ProtectPrivateKey` flag:
  ```powershell
  $password = ConvertTo-SecureString -String "YourPassword" -Force -AsPlainText
  Import-PfxCertificate -FilePath "path\to\certificate.pfx" -CertStoreLocation Cert:\CurrentUser\My -Password $password -Exportable
  ```

### PowerShell Result Parsing
- **Fixed**: Export-ActivityExplorerData results are now properly parsed using PSObject properties directly
- **Location**: PurviewService.cs lines 694-727
- **Key**: Access properties via `psObject.Properties["PropertyName"]?.Value` not `BaseObject`

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

## Debugging Tools

The project includes a `DebugHelper` class for inspecting PowerShell results:
```csharp
// In VS Code Debug Console:
DebugHelper.InspectPSObject(results[0])
DebugHelper.AnalyzeExportResult(results[0])
DebugHelper.ToJson(results[0])
```

## Known Limitations

- Certificate-based authentication only (no interactive auth)
- No automatic scheduled syncing (manual trigger only)
- Fetches only last 30 days of data per sync
- No real-time updates (requires manual refresh)