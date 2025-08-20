# Activity Explorer - Column Mapping Documentation

## Overview
This document describes all columns available from Export-ActivityExplorerData and how they are mapped to the Activity model.

## Database Schema Updates

### Core Fields (Previously Existing)
- `Id` (Guid) - Primary key
- `Timestamp` (DateTime) - Mapped from: **Happened**
- `UserId` (string) - Mapped from: **User**
- `UserPrincipalName` (string) - Mapped from: **User**
- `Operation` (string) - Mapped from: **Activity**
- `Workload` (string) - Mapped from: **Workload**
- `ResultStatus` (string) - Mapped from: **ResultStatus** (defaults to "Success")
- `ClientIP` (string) - Mapped from: **ClientIP**
- `ObjectId` (string) - Mapped from: **ItemName** → **FilePath** → **ObjectId** → **RecordIdentity**
- `TargetUser` (string) - Not currently mapped

### New Fields Added

#### Identity & Classification
- `RecordIdentity` (string) - Unique identifier for the activity record
- `ActivityId` (string) - Activity type identifier
- `Application` (string) - Application used (e.g., "Outlook")
- `ContentType` (string) - Type of content (e.g., "Email")
- `DataPlatform` (string) - Platform (e.g., "M365")

#### Device & Location
- `DeviceName` (string) - Device or service name
- `Platform` (string) - Platform type (e.g., "WebBrowser")
- `SourceLocationType` (string) - Location type (e.g., "Cloud")

#### File & Item
- `FilePath` (string) - Path to the file or item
- `ItemName` (string) - Name of the item
- `FileSize` (long?) - Size of the file in bytes

#### User Information
- `UserType` (string) - Type of user (e.g., "Regular")
- `UserSku` (string) - User license SKU

#### Sensitivity & Protection
- `SensitivityLabel` (string) - GUID of sensitivity label
- `HowApplied` (string) - How label was applied (e.g., "Auto", "Default")
- `HowAppliedDetail` (string) - Additional details on application
- `LabelEventType` (string) - Label event type (e.g., "LabelUpgraded")
- `ProtectionEventType` (string) - Protection event type

#### Complex Fields (Stored as JSON)
- `EmailInfo` (string/JSON) - Email metadata (Sender, Receivers, Subject, MessageID)
- `PolicyMatchInfo` (string/JSON) - DLP policy match information
- `SensitiveInfoTypeData` (string/JSON) - Sensitive information types detected
- `SensitiveInfoTypeBucketsData` (string/JSON) - Bucketed sensitive info data
- `SensitivityLabelIdsReferenced` (string/JSON) - Referenced label IDs
- `AttachmentDetails` (string/JSON) - Attachment information

#### Metadata
- `CreatedAt` (DateTime) - When record was added to database
- `UpdatedAt` (DateTime?) - When record was last updated
- `AdditionalDetails` (Dictionary) - For any unmapped fields

## Duplicate Detection
The system now uses `RecordIdentity` as the primary unique identifier for duplicate detection, with a fallback to the combination of:
- Timestamp
- UserId  
- Operation

## Data Type Handling

### Simple Types
- Strings: Direct mapping
- Numbers: `FileSize` parsed as long
- Dates: `Happened` parsed to DateTime

### Complex Types
Complex objects and arrays are serialized to JSON strings for storage:
- Empty arrays are stored as NULL
- Objects are serialized with their full structure
- Arrays maintain their element count and structure

## Usage

### Fetching Activities
```csharp
// The sync endpoint now maps all columns automatically
POST /api/activities/sync
```

### Analyzing Available Columns
```csharp
// Use this endpoint to discover new columns in the future
GET /api/activities/columns?sampleSize=100
```

## Database Reset
Due to the schema changes, the database needs to be recreated:

1. Run PowerShell as Administrator
2. Execute: `.\reset-database.ps1`
3. Start the application - database will be recreated
4. Run sync to fetch data with all columns

## Benefits
- **Complete Data Capture**: All available activity data is now stored
- **Future-Proof**: New columns can be discovered via the columns API
- **Rich Filtering**: More fields available for filtering and analysis
- **Audit Trail**: Sensitivity labels and DLP policy matches are captured
- **Extensible**: AdditionalDetails field captures any unmapped data