#Requires -Version 7.0
#Requires -Modules ExchangeOnlineManagement
<#
.SYNOPSIS
    Fetches activities from Purview Activity Explorer via Export-ActivityExplorerData.
.DESCRIPTION
    Connects to Exchange Online using CBA, fetches all pages, outputs JSON array of activities on stdout.
    Progress/errors go to stderr.
#>
param(
    [Parameter(Mandatory)][string]$Organization,
    [Parameter(Mandatory)][string]$AppId,
    [Parameter(Mandatory)][string]$CertificateThumbprint,
    [DateTime]$StartTime = (Get-Date).AddDays(-29).ToUniversalTime(),
    [DateTime]$EndTime = (Get-Date).ToUniversalTime(),
    [int]$PageSize = 5000,
    [int]$MaxPages = 20,
    [string]$WorkloadFilter,
    [string]$UserFilter
)

$ErrorActionPreference = "Stop"

# --- Connect (all output redirected to stderr via Write-Host) ---
Write-Host "Importing ExchangeOnlineManagement module..."
Import-Module ExchangeOnlineManagement -ErrorAction Stop *>&1 | Out-Null

Write-Host "Connecting to Exchange Online (Organization: $Organization)..."
Connect-IPPSSession -CertificateThumbprint $CertificateThumbprint -AppId $AppId -Organization $Organization -ErrorAction Stop *>&1 | Out-Null

# --- Fetch with pagination ---
$allActivities = [System.Collections.Generic.List[object]]::new()
$pageCookie = $null
$pageNumber = 0

try {
    do {
        $pageNumber++
        Write-Host "Fetching page $pageNumber..." -ForegroundColor Gray

        $params = @{
            StartTime    = $StartTime
            EndTime      = $EndTime
            OutputFormat = "Json"
            PageSize     = $PageSize
        }

        if ($pageCookie) {
            $params.PageCookie = $pageCookie
        }
        if ($WorkloadFilter) {
            $params.Filter1 = @($WorkloadFilter)
        }
        if ($UserFilter) {
            $params.Filter2 = @($UserFilter)
        }

        $result = Export-ActivityExplorerData @params

        if (-not $result) {
            Write-Host "Page $pageNumber: No results returned." -ForegroundColor Yellow
            break
        }

        $resultCode = $result.ResultCode
        if ($resultCode -ne "Success") {
            throw "Export-ActivityExplorerData failed: ResultCode=$resultCode, ErrorData=$($result.ErrorData)"
        }

        $recordCount = [int]($result.RecordCount ?? 0)
        $totalCount  = [int]($result.TotalResultCount ?? 0)
        $lastPage    = [bool]($result.LastPage ?? $true)
        $pageCookie  = $result.WaterMark

        Write-Host "Page $pageNumber: $recordCount records (total: $totalCount, lastPage: $lastPage)" -ForegroundColor Gray

        if ($result.ResultData) {
            $activities = $result.ResultData | ConvertFrom-Json -AsHashtable
            if ($activities) {
                foreach ($a in $activities) {
                    $allActivities.Add($a)
                }
            }
        }

    } while (-not $lastPage -and $pageNumber -lt $MaxPages)

    Write-Host "Fetched $($allActivities.Count) activities in $pageNumber pages." -ForegroundColor Gray
}
finally {
    Disconnect-ExchangeOnline -Confirm:$false -ErrorAction SilentlyContinue *>&1 | Out-Null
}

# Output JSON array on stdout
$allActivities | ConvertTo-Json -Depth 10 -Compress -AsArray
