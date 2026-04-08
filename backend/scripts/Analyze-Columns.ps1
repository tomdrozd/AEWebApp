#Requires -Version 7.0
#Requires -Modules ExchangeOnlineManagement
<#
.SYNOPSIS
    Fetches a sample of activities and analyzes available columns.
.DESCRIPTION
    Returns JSON object mapping column names to sample values on stdout.
#>
param(
    [Parameter(Mandatory)][string]$Organization,
    [Parameter(Mandatory)][string]$AppId,
    [Parameter(Mandatory)][string]$CertificateThumbprint,
    [int]$SampleSize = 100
)

$ErrorActionPreference = "Stop"

Import-Module ExchangeOnlineManagement -ErrorAction Stop *>&1 | Out-Null
Connect-IPPSSession -CertificateThumbprint $CertificateThumbprint -AppId $AppId -Organization $Organization -ErrorAction Stop *>&1 | Out-Null

try {
    $result = Export-ActivityExplorerData `
        -StartTime (Get-Date).AddDays(-7).ToUniversalTime() `
        -EndTime (Get-Date).ToUniversalTime() `
        -OutputFormat Json `
        -PageSize $SampleSize

    if (-not $result -or $result.ResultCode -ne "Success") {
        throw "Export failed: ResultCode=$($result.ResultCode), ErrorData=$($result.ErrorData)"
    }

    $columnAnalysis = @{}

    if ($result.ResultData) {
        $activities = $result.ResultData | ConvertFrom-Json -AsHashtable

        if ($activities) {
            Write-Host "Analyzing $($activities.Count) activities..." -ForegroundColor Gray

            foreach ($activity in $activities) {
                foreach ($key in $activity.Keys) {
                    if (-not $columnAnalysis.ContainsKey($key)) {
                        $columnAnalysis[$key] = [System.Collections.Generic.List[string]]::new()
                    }

                    if ($columnAnalysis[$key].Count -lt 5) {
                        $val = $activity[$key]
                        $sample = if ($null -eq $val) { "null" }
                                  elseif ($val -is [System.Collections.IDictionary] -or $val -is [System.Collections.IList]) {
                                      ($val | ConvertTo-Json -Depth 3 -Compress).Substring(0, [Math]::Min(100, ($val | ConvertTo-Json -Depth 3 -Compress).Length))
                                  }
                                  else { $val.ToString().Substring(0, [Math]::Min(100, $val.ToString().Length)) }

                        if ($sample -notin $columnAnalysis[$key]) {
                            $columnAnalysis[$key].Add($sample)
                        }
                    }
                }
            }

            Write-Host "Found $($columnAnalysis.Count) unique columns." -ForegroundColor Gray
        }
    }
}
finally {
    Disconnect-ExchangeOnline -Confirm:$false -ErrorAction SilentlyContinue *>&1 | Out-Null
}

$columnAnalysis | ConvertTo-Json -Depth 5 -Compress
