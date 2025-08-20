# Activity Explorer - Microsoft Purview Integration

A comprehensive web application that replicates Microsoft Purview's Activity Explorer functionality, providing detailed audit log analysis and monitoring capabilities for Microsoft 365 environments.

## 🚀 Features

### Core Functionality
- **Certificate-based Authentication**: Secure connection to Microsoft 365 using Azure AD app registration
- **Complete Data Capture**: Stores all 29+ fields from Export-ActivityExplorerData
- **Real-time Sync**: Fetch latest activity data from Microsoft Purview on-demand
- **Advanced Filtering**: Filter by date range, user, workload, and operations
- **Data Export**: Export filtered results to CSV format
- **Column Analysis**: API endpoint to discover all available fields from Purview

### User Interface
- **Interactive Activity Table**: 
  - Clickable rows for detailed view
  - Pagination with customizable page sizes (10, 25, 50, 100)
  - Color-coded status indicators
  - Hover effects for better UX

- **Activity Detail Flyout Panel** (NEW):
  - Comprehensive view of ALL activity fields
  - Organized sections (Core Info, Device, File, Sensitivity)
  - Copy-to-clipboard functionality for important values
  - JSON viewer for complex fields (Email Info, Policy Matches)
  - Expandable accordions for nested data
  - File size formatting (KB, MB, GB)

### Data Fields Captured
- **Core Fields**: Timestamp, User, Operation, Workload, Client IP, Result Status
- **Identity**: Record ID, Activity ID, Application
- **Device Info**: Platform, Device Name, Source Location
- **File Details**: Path, Name, Size (formatted)
- **Sensitivity**: Labels, DLP policies, Protection events
- **Complex Metadata**: Email info, Policy matches, Attachments (stored as JSON)

## Prerequisites

- **Windows** with PowerShell 7+
- **.NET 8.0 SDK** or later
- **Node.js 18+** and npm
- **SQL Server** (LocalDB, Express, or Developer Edition)
- **Exchange Online Management Module** installed:
  ```powershell
  Install-Module -Name ExchangeOnlineManagement
  ```

## Project Structure

```
ClaudeWebApp1/
├── backend/                      # .NET Core Backend
│   ├── ActivityExplorer.API/     # Web API
│   ├── ActivityExplorer.Core/    # Domain models
│   ├── ActivityExplorer.Data/    # Database context
│   └── ActivityExplorer.Services/# Business logic
└── frontend/                     # React Frontend
    └── activity-explorer-ui/     # React app
```

## Setup Instructions

### 1. Install Exchange Online Management Module
```powershell
Install-Module -Name ExchangeOnlineManagement -Force -AllowClobber -Scope CurrentUser
```

### 2. Import Certificate
```powershell
$password = Read-Host -AsSecureString "Enter PFX password"
Import-PfxCertificate -FilePath "path\to\certificate.pfx" -CertStoreLocation Cert:\CurrentUser\My -Password $password -Exportable
```

### 3. Configure the Backend

Update `backend/ActivityExplorer.API/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "ActivityDatabase": "Server=(localdb)\\mssqllocaldb;Database=ActivityExplorer;Trusted_Connection=True"
  },
  "Purview": {
    "Organization": "yourdomain.com",
    "AppId": "your-app-id-guid",
    "TenantId": "your-tenant-id",
    "CertificateThumbprint": "your-certificate-thumbprint"
  }
}
```

### 4. Build and Start the Backend

```bash
cd backend
dotnet build
cd ActivityExplorer.API
dotnet run
```

The API will be available at:
- API: http://localhost:5000
- Swagger UI: http://localhost:5000/swagger

Database will be created automatically on first run.

### 5. Install Frontend Dependencies

```bash
cd frontend/activity-explorer-ui
npm install
npm install @mui/icons-material@^5.14.0 --save
```

### 6. Start the Frontend

```bash
cd frontend/activity-explorer-ui
npm start
```

The React app will open at http://localhost:3000

## How to Use

### 1. Initial Setup
- Open http://localhost:3000 in your browser
- Click **"Check Auth Status"** to verify configuration
- Ensure all checks pass (Module ✅, Certificate ✅, Configuration ✅)

### 2. Sync Activities from Purview
- Click **"Sync Activities from Purview"** button
- Wait for connection and data fetch (30-60 seconds)
- Success message will show count of new activities

### 3. View Activity Details (NEW)
- **Click any row** in the activities table
- A detailed panel slides in from the right showing:
  - All 29+ activity fields
  - Organized sections for easy navigation
  - Copy buttons for important values
  - Expandable JSON viewers for complex data
- Click X or outside the panel to close

### 4. Filter Activities
- Use **date pickers** for time range filtering
- **Search by user** email/name
- Click **"Clear Filters"** to reset all filters

### 5. Export Data
- Click **"Export to CSV"** to download filtered results
- CSV includes core fields for Excel analysis

## Authentication

The application uses **Certificate-based Authentication**:
- Secure connection using X.509 certificate
- No passwords stored in configuration
- Certificate must be in CurrentUser\My store
- Requires Azure AD app registration with certificate uploaded

## Troubleshooting

### Database Schema Issues
**Error**: "Invalid column name 'RecordIdentity'"
- **Cause**: Database has old schema after model updates
- **Solution**:
  ```powershell
  # Stop the backend
  # Drop the database
  sqlcmd -S "(localdb)\mssqllocaldb" -Q "DROP DATABASE ActivityExplorer"
  # Rebuild backend
  cd backend
  dotnet build --no-incremental
  # Restart backend - database will be recreated
  ```

### Certificate Authentication Errors
**Error**: "The key is not valid for use in the specified state"
- **Solution**: Reimport certificate without strong key protection:
  ```powershell
  $password = ConvertTo-SecureString -String "YourPassword" -Force -AsPlainText
  Import-PfxCertificate -FilePath "path\to\cert.pfx" -CertStoreLocation Cert:\CurrentUser\My -Password $password -Exportable
  ```

### Module Not Found
**Error**: "ExchangeOnlineManagement module not found"
- **Solution**: Install as Administrator:
  ```powershell
  Install-Module -Name ExchangeOnlineManagement -Force -AllowClobber
  ```

### CORS Errors
- Ensure backend is running on http://localhost:5000
- Check frontend is on http://localhost:3000

## API Endpoints

- `GET /api/activities` - Get paginated activities with filtering
- `POST /api/activities/sync` - Trigger sync from Purview
- `GET /api/activities/export/csv` - Export filtered activities to CSV
- `GET /api/activities/statistics` - Get activity statistics
- `GET /api/activities/status` - Check authentication status
- `GET /api/activities/columns?sampleSize=100` - Analyze available columns from Purview

## Development Notes

- Database is created automatically on first run using Entity Framework
- Certificate-based authentication for secure, automated connections
- Data is fetched from the last 30 days by default
- All 29+ fields from Export-ActivityExplorerData are captured
- Complex fields (Email, Policy info) stored as JSON
- SQL Server LocalDB requires no additional setup

## Architecture

- **Backend**: .NET Core 8.0 with Entity Framework Core
- **Frontend**: React 18 with TypeScript and Material-UI
- **Database**: SQL Server LocalDB
- **Authentication**: Certificate-based via Exchange Online PowerShell
- **Data Flow**: PowerShell → .NET Service → SQL Database → REST API → React UI

## Future Enhancements

- Real-time activity monitoring with SignalR
- Advanced search with full-text indexing
- Custom dashboard creation
- Automated report generation
- Integration with Microsoft Graph API
- Support for multiple tenants