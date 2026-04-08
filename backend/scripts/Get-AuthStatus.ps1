#Requires -Version 7.0
<#
.SYNOPSIS
    Checks prerequisites for Activity Explorer sync: EXO module, certificate, connection.
.DESCRIPTION
    Returns JSON object with status of all prerequisites on stdout.
    Errors go to stderr. Exit code 0 = check completed (even if some checks failed).
#>
param(
    [Parameter(Mandatory)][string]$Organization,
    [Parameter(Mandatory)][string]$AppId,
    [Parameter(Mandatory)][string]$CertificateThumbprint,
    [string]$CertificateStoreLocation = "CurrentUser",
    [string]$CertificateStoreName = "My"
)

$ErrorActionPreference = "Stop"

$status = @{
    PowerShellVersion    = $PSVersionTable.PSVersion.ToString()
    IsModuleInstalled    = $false
    ModuleVersion        = $null
    ModulePath           = $null
    ModuleInstallCommand = "Install-Module -Name ExchangeOnlineManagement -Force -AllowClobber -Scope CurrentUser"
    SearchedPaths        = @()
    IsCertificateFound   = $false
    CertificateThumbprint = $CertificateThumbprint
    CertificateSubject   = $null
    CertificateExpiry    = $null
    IsCertificateExpired = $false
    CertificateStore     = "$CertificateStoreLocation/$CertificateStoreName"
    IsConfigurationValid = $true
    ConfigurationErrors  = @()
    CanConnect           = $false
    ConnectionTestResult = $null
    LastConnectionError  = $null
    ConfiguredValues     = @{
        Organization         = $Organization
        AppId                = $AppId
        CertificateThumbprint = $CertificateThumbprint
        CertificateStore     = "$CertificateStoreLocation/$CertificateStoreName"
    }
    Recommendations      = @()
}

# --- Check EXO module ---
try {
    $searchPaths = $env:PSModulePath -split [IO.Path]::PathSeparator | Where-Object { $_ -and (Test-Path $_ -ErrorAction SilentlyContinue) }
    $status.SearchedPaths = @($searchPaths)

    $module = Get-Module -ListAvailable -Name ExchangeOnlineManagement -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($module) {
        $status.IsModuleInstalled = $true
        $status.ModuleVersion = $module.Version.ToString()
        $status.ModulePath = $module.ModuleBase
    }
    else {
        $status.Recommendations += "Exchange Online Management module not found."
        $status.Recommendations += "Install using: $($status.ModuleInstallCommand)"
    }
}
catch {
    Write-Error "Module check failed: $_"
    $status.Recommendations += "Failed to check EXO module: $_"
}

# --- Check certificate ---
try {
    $storeLoc = [System.Security.Cryptography.X509Certificates.StoreLocation]$CertificateStoreLocation
    $storeNm  = [System.Security.Cryptography.X509Certificates.StoreName]$CertificateStoreName
    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new($storeNm, $storeLoc)
    $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)

    $certs = $store.Certificates.Find(
        [System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint,
        $CertificateThumbprint.Replace(" ", "").ToUpper(),
        $false
    )
    $store.Close()

    if ($certs.Count -gt 0) {
        $cert = $certs[0]
        $status.IsCertificateFound = $true
        $status.CertificateSubject = $cert.Subject
        $status.CertificateExpiry = $cert.NotAfter.ToString("o")
        $status.IsCertificateExpired = $cert.NotAfter -lt (Get-Date)

        if ($status.IsCertificateExpired) {
            $status.Recommendations += "Certificate has expired. Generate and upload a new certificate to Azure AD."
        }
    }
    else {
        $status.Recommendations += "Certificate with thumbprint '$CertificateThumbprint' not found in $CertificateStoreLocation/$CertificateStoreName."
    }
}
catch {
    Write-Error "Certificate check failed: $_"
    $status.Recommendations += "Failed to check certificate: $_"
}

# --- Test connection (only if module + cert are OK) ---
if ($status.IsModuleInstalled -and $status.IsCertificateFound) {
    try {
        Import-Module ExchangeOnlineManagement -ErrorAction Stop *>&1 | Out-Null
        Connect-IPPSSession -CertificateThumbprint $CertificateThumbprint -AppId $AppId -Organization $Organization -ErrorAction Stop *>&1 | Out-Null
        $status.CanConnect = $true
        $status.ConnectionTestResult = "Successfully connected to Exchange Online"

        Disconnect-ExchangeOnline -Confirm:$false -ErrorAction SilentlyContinue *>&1 | Out-Null
    }
    catch {
        $status.CanConnect = $false
        $status.LastConnectionError = $_.Exception.Message
        $status.ConnectionTestResult = "Connection test failed"
        $status.Recommendations += "Verify Azure AD app registration, certificate upload, and service principal."
    }
}

# Output JSON
$status | ConvertTo-Json -Depth 5 -Compress
