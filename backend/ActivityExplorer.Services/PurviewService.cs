using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ActivityExplorer.Core.Models;
using ActivityExplorer.Core.DTOs;

namespace ActivityExplorer.Services
{
    public class PurviewService
    {
        private readonly ILogger<PurviewService> _logger;
        private readonly PurviewSettings _settings;
        
        public PurviewService(ILogger<PurviewService> logger, IOptions<PurviewSettings> settings)
        {
            _logger = logger;
            _settings = settings.Value;
            ValidateSettings();
        }
        
        private void ValidateSettings()
        {
            var errors = new List<string>();
            
            if (string.IsNullOrWhiteSpace(_settings.Organization))
                errors.Add("Organization is required");
            
            if (string.IsNullOrWhiteSpace(_settings.AppId))
                errors.Add("AppId is required");
            
            if (string.IsNullOrWhiteSpace(_settings.TenantId))
                errors.Add("TenantId is required");
            
            if (string.IsNullOrWhiteSpace(_settings.CertificateThumbprint))
                errors.Add("CertificateThumbprint is required");
            
            if (errors.Any())
            {
                var errorMessage = "Purview configuration is incomplete. Please configure the following in appsettings.json:\n" + 
                                   string.Join("\n", errors);
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
        }
        
        public async Task<AuthStatusDto> GetAuthenticationStatusAsync()
        {
            var status = new AuthStatusDto();
            
            // Check configuration
            CheckConfiguration(status);
            
            // Check PowerShell module
            await CheckPowerShellModuleAsync(status);
            
            // Check certificate
            CheckCertificate(status);
            
            // Test connection if everything looks good
            if (status.IsModuleInstalled && status.IsCertificateFound && status.IsConfigurationValid)
            {
                await TestConnectionAsync(status);
            }
            
            // Add recommendations
            GenerateRecommendations(status);
            
            return status;
        }
        
        private void CheckConfiguration(AuthStatusDto status)
        {
            status.ConfiguredValues["Organization"] = _settings.Organization ?? "Not configured";
            status.ConfiguredValues["AppId"] = string.IsNullOrWhiteSpace(_settings.AppId) ? "Not configured" : _settings.AppId;
            status.ConfiguredValues["TenantId"] = string.IsNullOrWhiteSpace(_settings.TenantId) ? "Not configured" : _settings.TenantId;
            status.ConfiguredValues["CertificateThumbprint"] = string.IsNullOrWhiteSpace(_settings.CertificateThumbprint) 
                ? "Not configured" 
                : _settings.CertificateThumbprint;
            status.ConfiguredValues["CertificateStore"] = $"{_settings.CertificateStoreLocation}/{_settings.CertificateStoreName}";
            
            if (string.IsNullOrWhiteSpace(_settings.Organization))
                status.ConfigurationErrors.Add("Organization is not configured");
            
            if (string.IsNullOrWhiteSpace(_settings.AppId))
                status.ConfigurationErrors.Add("AppId is not configured");
            
            if (string.IsNullOrWhiteSpace(_settings.TenantId))
                status.ConfigurationErrors.Add("TenantId is not configured");
            
            if (string.IsNullOrWhiteSpace(_settings.CertificateThumbprint))
                status.ConfigurationErrors.Add("CertificateThumbprint is not configured");
            
            status.IsConfigurationValid = status.ConfigurationErrors.Count == 0;
        }
        
        private async Task CheckPowerShellModuleAsync(AuthStatusDto status)
        {
            PowerShell? ps = null;
            try
            {
                ps = PowerShell.Create();
                _logger.LogInformation("Starting PowerShell module check...");
                
                // Set execution policy for this session to bypass restrictions
                ps.AddCommand("Set-ExecutionPolicy")
                  .AddParameter("ExecutionPolicy", "Bypass")
                  .AddParameter("Scope", "Process")
                  .AddParameter("Force");
                await Task.Run(() => ps.Invoke());
                ps.Commands.Clear();
                _logger.LogInformation("Set execution policy to Bypass for this session");
                    
                // Get PowerShell version
                ps.AddScript("$PSVersionTable.PSVersion.ToString()");
                    var versionResult = await Task.Run(() => ps.Invoke());
                    if (versionResult.Any())
                    {
                        status.PowerShellVersion = versionResult.First()?.ToString() ?? "Unknown";
                        _logger.LogInformation($"PowerShell version: {status.PowerShellVersion}");
                    }
                    else
                    {
                        status.PowerShellVersion = "Unable to determine";
                        _logger.LogWarning("Could not determine PowerShell version");
                    }
                    
                    // Clear any errors
                    ps.Streams.Error.Clear();
                    ps.Commands.Clear();
                    
                    // Get current module paths - try multiple methods
                    _logger.LogInformation("Getting module paths...");
                    
                    // Method 1: Direct environment variable
                    ps.AddScript(@"
                        $paths = @()
                        if ($env:PSModulePath) {
                            $paths = $env:PSModulePath -split [System.IO.Path]::PathSeparator
                        }
                        # Add default paths if PSModulePath is empty
                        if ($paths.Count -eq 0) {
                            $paths += ""$env:ProgramFiles\WindowsPowerShell\Modules""
                            $paths += ""$env:ProgramFiles\PowerShell\Modules""
                            $paths += ""$env:USERPROFILE\Documents\WindowsPowerShell\Modules""
                            $paths += ""$env:USERPROFILE\Documents\PowerShell\Modules""
                            $paths += ""C:\Program Files\WindowsPowerShell\Modules""
                            $paths += ""C:\Program Files\PowerShell\Modules""
                            $paths += ""C:\Program Files (x86)\WindowsPowerShell\Modules""
                        }
                        $paths | Where-Object { $_ -and (Test-Path $_ -ErrorAction SilentlyContinue) }
                    ");
                    
                    var pathResults = await Task.Run(() => ps.Invoke());
                    
                    if (!pathResults.Any())
                    {
                        _logger.LogWarning("No module paths returned from PowerShell, using defaults");
                        // Fallback: Add common module paths manually
                        var defaultPaths = new[] {
                            @"C:\Program Files\WindowsPowerShell\Modules",
                            @"C:\Program Files\PowerShell\Modules",
                            @"C:\Program Files (x86)\WindowsPowerShell\Modules",
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WindowsPowerShell", "Modules"),
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PowerShell", "Modules"),
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsPowerShell", "Modules"),
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "Modules")
                        };
                        
                        foreach (var path in defaultPaths)
                        {
                            if (Directory.Exists(path))
                            {
                                status.SearchedPaths.Add(path);
                                _logger.LogInformation($"Added default module path: {path}");
                            }
                        }
                    }
                    else
                    {
                        foreach (var path in pathResults)
                        {
                            var pathStr = path?.ToString();
                            if (!string.IsNullOrEmpty(pathStr))
                            {
                                status.SearchedPaths.Add(pathStr);
                                _logger.LogInformation($"Module path from PSModulePath: {pathStr}");
                            }
                        }
                    }
                    
                    // Log any errors
                    if (ps.Streams.Error.Count > 0)
                    {
                        foreach (var error in ps.Streams.Error)
                        {
                            _logger.LogWarning($"PowerShell error while getting paths: {error}");
                        }
                    }
                    
                    ps.Streams.Error.Clear();
                    ps.Commands.Clear();
                    
                    _logger.LogInformation("Attempting to import ExchangeOnlineManagement module...");
                    
                    // Try to import the module first
                    ps.AddCommand("Import-Module")
                      .AddParameter("Name", "ExchangeOnlineManagement")
                      .AddParameter("ErrorAction", "SilentlyContinue")
                      .AddParameter("Verbose");
                    
                    var importResult = await Task.Run(() => ps.Invoke());
                    
                    if (ps.Streams.Error.Count > 0)
                    {
                        foreach (var error in ps.Streams.Error)
                        {
                            _logger.LogWarning($"Error importing module: {error}");
                        }
                    }
                    
                    ps.Streams.Error.Clear();
                    ps.Commands.Clear();
                    
                    // Check if module is available using multiple methods
                    _logger.LogInformation("Checking for ExchangeOnlineManagement module...");
                    
                    // Method 1: Standard Get-Module
                    ps.AddCommand("Get-Module")
                      .AddParameter("ListAvailable")
                      .AddParameter("Name", "ExchangeOnlineManagement");
                    
                    var modules = await Task.Run(() => ps.Invoke());
                    _logger.LogInformation($"Get-Module returned {modules.Count} results");
                    
                    if (!modules.Any())
                    {
                        _logger.LogInformation("Module not found with exact name, trying wildcard search...");
                        ps.Commands.Clear();
                        ps.AddCommand("Get-Module")
                          .AddParameter("ListAvailable")
                          .AddParameter("Name", "*Exchange*");
                        modules = await Task.Run(() => ps.Invoke());
                        _logger.LogInformation($"Wildcard search returned {modules.Count} results");
                        
                        foreach (var module in modules)
                        {
                            var name = module.Properties["Name"]?.Value?.ToString();
                            _logger.LogInformation($"Found module: {name}");
                        }
                    }
                    
                    // Method 2: Direct file system search
                    if (!modules.Any())
                    {
                        _logger.LogInformation("Searching for module in file system...");
                        ps.Commands.Clear();
                        
                        // Search for the module directory
                        foreach (var searchPath in status.SearchedPaths)
                        {
                            var modulePath = Path.Combine(searchPath, "ExchangeOnlineManagement");
                            if (Directory.Exists(modulePath))
                            {
                                _logger.LogInformation($"Found module directory at: {modulePath}");
                                status.ModulePath = modulePath;
                                
                                // Try to import from this specific path
                                ps.Commands.Clear();
                                ps.AddCommand("Import-Module")
                                  .AddParameter("Name", modulePath)
                                  .AddParameter("Force");
                                
                                await Task.Run(() => ps.Invoke());
                                
                                // Check if import succeeded
                                ps.Commands.Clear();
                                ps.AddCommand("Get-Module")
                                  .AddParameter("Name", "ExchangeOnlineManagement");
                                
                                var loadedModules = await Task.Run(() => ps.Invoke());
                                if (loadedModules.Any())
                                {
                                    _logger.LogInformation("Successfully imported module from directory");
                                    modules = loadedModules;
                                    break;
                                }
                            }
                        }
                    }
                    
                    if (modules.Any())
                    {
                        status.IsModuleInstalled = true;
                        var module = modules.First();
                        var versionProp = module.Properties["Version"];
                        if (versionProp != null)
                        {
                            status.ModuleVersion = versionProp.Value?.ToString();
                        }
                        var pathProp = module.Properties["Path"];
                        if (pathProp != null)
                        {
                            status.ModulePath = pathProp.Value?.ToString();
                            _logger.LogInformation($"Module found at: {status.ModulePath}");
                        }
                    }
                    else
                    {
                        status.IsModuleInstalled = false;
                        status.ModuleInstallCommand = "Install-Module -Name ExchangeOnlineManagement -Force -AllowClobber -Scope CurrentUser";
                        _logger.LogWarning("ExchangeOnlineManagement module not found in any searched paths");
                    }
            }
            catch (PSSnapInException snapInEx)
            {
                _logger.LogWarning($"PowerShell snap-in error (this is normal): {snapInEx.Message}");
                // This is expected - continue with module check
                status.IsModuleInstalled = false;
                status.ModuleInstallCommand = "Install-Module -Name ExchangeOnlineManagement -Force -AllowClobber -Scope CurrentUser";
                status.Recommendations.Add("Note: PowerShell snap-in errors are expected and can be ignored");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check PowerShell module status");
                status.IsModuleInstalled = false;
                status.ModuleInstallCommand = "Install-Module -Name ExchangeOnlineManagement -Force -AllowClobber -Scope CurrentUser";
            }
            finally
            {
                ps?.Dispose();
            }
        }
        
        private void CheckCertificate(AuthStatusDto status)
        {
            if (string.IsNullOrWhiteSpace(_settings.CertificateThumbprint))
            {
                status.IsCertificateFound = false;
                return;
            }
            
            var cert = FindCertificate();
            if (cert != null)
            {
                status.IsCertificateFound = true;
                status.CertificateThumbprint = cert.Thumbprint;
                status.CertificateSubject = cert.Subject;
                status.CertificateExpiry = cert.NotAfter;
                status.IsCertificateExpired = cert.NotAfter < DateTime.Now;
                status.CertificateStore = $"{_settings.CertificateStoreLocation}/{_settings.CertificateStoreName}";
            }
            else
            {
                status.IsCertificateFound = false;
                status.CertificateThumbprint = _settings.CertificateThumbprint;
                status.CertificateStore = $"{_settings.CertificateStoreLocation}/{_settings.CertificateStoreName}";
            }
        }
        
        private async Task TestConnectionAsync(AuthStatusDto status)
        {
            using (var ps = PowerShell.Create())
            {
                try
                {
                    // Set execution policy for this session
                    ps.AddCommand("Set-ExecutionPolicy")
                      .AddParameter("ExecutionPolicy", "Bypass")
                      .AddParameter("Scope", "Process")
                      .AddParameter("Force");
                    await Task.Run(() => ps.Invoke());
                    ps.Commands.Clear();
                    
                    // Import module
                    ps.AddCommand("Import-Module")
                      .AddParameter("Name", "ExchangeOnlineManagement")
                      .AddParameter("ErrorAction", "SilentlyContinue");
                    await Task.Run(() => ps.Invoke());
                    ps.Commands.Clear();
                    
                    // Try to connect
                    ps.AddCommand("Connect-IPPSSession")
                      .AddParameter("CertificateThumbprint", _settings.CertificateThumbprint)
                      .AddParameter("AppId", _settings.AppId)
                      .AddParameter("Organization", _settings.Organization)
                      .AddParameter("ErrorAction", "Stop");
                    
                    var connectResults = await Task.Run(() => ps.Invoke());
                    
                    if (ps.HadErrors)
                    {
                        var errors = string.Join("; ", ps.Streams.Error.Select(e => e.ToString()));
                        status.CanConnect = false;
                        status.LastConnectionError = errors;
                        status.ConnectionTestResult = "Failed to connect";
                    }
                    else
                    {
                        status.CanConnect = true;
                        status.ConnectionTestResult = "Successfully connected to Exchange Online";
                        
                        // Disconnect
                        ps.Commands.Clear();
                        ps.AddCommand("Disconnect-ExchangeOnline")
                          .AddParameter("Confirm", false)
                          .AddParameter("ErrorAction", "SilentlyContinue");
                        await Task.Run(() => ps.Invoke());
                    }
                }
                catch (Exception ex)
                {
                    status.CanConnect = false;
                    status.LastConnectionError = ex.Message;
                    status.ConnectionTestResult = "Connection test failed";
                }
            }
        }
        
        private void GenerateRecommendations(AuthStatusDto status)
        {
            if (!status.IsModuleInstalled)
            {
                status.Recommendations.Add("Exchange Online Management module not found.");
                status.Recommendations.Add($"Install using PowerShell as Administrator: {status.ModuleInstallCommand}");
                
                if (status.SearchedPaths.Any())
                {
                    status.Recommendations.Add("Module was searched in the following paths:");
                    foreach (var path in status.SearchedPaths.Take(3))
                    {
                        status.Recommendations.Add($"  - {path}");
                    }
                }
                
                status.Recommendations.Add("Alternative: Install globally with -Scope AllUsers (requires admin):");
                status.Recommendations.Add("  Install-Module -Name ExchangeOnlineManagement -Force -AllowClobber -Scope AllUsers");
                
                if (!string.IsNullOrEmpty(status.PowerShellVersion))
                {
                    status.Recommendations.Add($"Note: Application is using PowerShell {status.PowerShellVersion}");
                }
            }
            
            if (!status.IsCertificateFound)
            {
                status.Recommendations.Add($"Certificate with thumbprint '{_settings.CertificateThumbprint}' not found in {status.CertificateStore}");
                status.Recommendations.Add("Ensure the certificate is installed in the correct store");
                status.Recommendations.Add("Verify the thumbprint is correct (no spaces or special characters)");
            }
            
            if (status.IsCertificateExpired)
            {
                status.Recommendations.Add("Certificate has expired. Generate and upload a new certificate to Azure AD");
            }
            
            if (!status.IsConfigurationValid)
            {
                status.Recommendations.Add("Complete the configuration in appsettings.json");
                foreach (var error in status.ConfigurationErrors)
                {
                    status.Recommendations.Add($"  - {error}");
                }
            }
            
            if (status.IsModuleInstalled && status.IsCertificateFound && status.IsConfigurationValid && !status.CanConnect)
            {
                status.Recommendations.Add("Verify the Azure AD app registration is configured correctly");
                status.Recommendations.Add("Ensure the certificate is uploaded to the Azure AD app");
                status.Recommendations.Add("Check that the service principal is created in Exchange Online");
                status.Recommendations.Add("Verify the application has the required permissions");
            }
        }
        
        private X509Certificate2? FindCertificate()
        {
            try
            {
                var storeLocation = Enum.Parse<StoreLocation>(_settings.CertificateStoreLocation);
                var storeName = Enum.Parse<StoreName>(_settings.CertificateStoreName);
                
                using var store = new X509Store(storeName, storeLocation);
                store.Open(OpenFlags.ReadOnly);
                
                var certificates = store.Certificates.Find(
                    X509FindType.FindByThumbprint, 
                    _settings.CertificateThumbprint.Replace(" ", "").ToUpper(), 
                    false);
                
                if (certificates.Count == 0)
                {
                    _logger.LogError($"Certificate with thumbprint '{_settings.CertificateThumbprint}' not found in {storeLocation}/{storeName}");
                    return null;
                }
                
                return certificates[0];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find certificate");
                return null;
            }
        }
        
        public async Task<List<Activity>> FetchActivitiesAsync()
        {
            return await FetchActivitiesAsync(null, null, null, null);
        }
        
        public async Task<List<Activity>> FetchActivitiesAsync(
            DateTime? startDate = null, 
            DateTime? endDate = null,
            string? workloadFilter = null,
            string? userFilter = null)
        {
            var activities = new List<Activity>();
            
            // Verify certificate exists
            var certificate = FindCertificate();
            if (certificate == null)
            {
                throw new InvalidOperationException(
                    $"Certificate with thumbprint '{_settings.CertificateThumbprint}' not found. " +
                    $"Please ensure the certificate is installed in {_settings.CertificateStoreLocation}/{_settings.CertificateStoreName}");
            }
            
            using (var ps = PowerShell.Create())
            {
                try
                {
                    // Set execution policy for this session
                    ps.AddCommand("Set-ExecutionPolicy")
                      .AddParameter("ExecutionPolicy", "Bypass")
                      .AddParameter("Scope", "Process")
                      .AddParameter("Force");
                    await Task.Run(() => ps.Invoke());
                    ps.Commands.Clear();
                    
                    _logger.LogInformation("Importing Exchange Online Management module...");
                    
                    // Import the Exchange Online Management module
                    ps.AddCommand("Import-Module")
                      .AddParameter("Name", "ExchangeOnlineManagement")
                      .AddParameter("ErrorAction", "Stop");
                    
                    try
                    {
                        await Task.Run(() => ps.Invoke());
                        ps.Commands.Clear();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to import Exchange Online Management module");
                        throw new InvalidOperationException(
                            "Exchange Online Management module is not installed. " +
                            "Please install it using: Install-Module -Name ExchangeOnlineManagement -Force -AllowClobber");
                    }
                    
                    if (ps.HadErrors)
                    {
                        var moduleError = ps.Streams.Error.FirstOrDefault()?.ToString() ?? "Unknown error";
                        throw new InvalidOperationException(
                            $"Failed to import Exchange Online Management module: {moduleError}. " +
                            "Please ensure the module is installed: Install-Module -Name ExchangeOnlineManagement");
                    }
                    
                    _logger.LogInformation($"Connecting to Exchange Online using certificate authentication...");
                    _logger.LogInformation($"Organization: {_settings.Organization}, AppId: {_settings.AppId}");
                    
                    // Connect to Exchange Online Security & Compliance Center using certificate
                    ps.AddCommand("Connect-IPPSSession")
                      .AddParameter("CertificateThumbprint", _settings.CertificateThumbprint)
                      .AddParameter("AppId", _settings.AppId)
                      .AddParameter("Organization", _settings.Organization);
                    
                    var connectResults = await Task.Run(() => ps.Invoke());
                    
                    if (ps.HadErrors)
                    {
                        var errors = string.Join("; ", ps.Streams.Error.Select(e => e.ToString()));
                        _logger.LogError($"PowerShell Error during connection: {errors}");
                        
                        // Provide helpful error messages
                        if (errors.Contains("certificate"))
                        {
                            throw new InvalidOperationException(
                                $"Certificate authentication failed. Please verify:\n" +
                                $"1. Certificate thumbprint '{_settings.CertificateThumbprint}' is correct\n" +
                                $"2. Certificate is installed in {_settings.CertificateStoreLocation}/{_settings.CertificateStoreName}\n" +
                                $"3. Certificate is uploaded to Azure AD app registration");
                        }
                        else if (errors.Contains("AppId") || errors.Contains("application"))
                        {
                            throw new InvalidOperationException(
                                $"Application authentication failed. Please verify:\n" +
                                $"1. AppId '{_settings.AppId}' is correct\n" +
                                $"2. Application is registered in Azure AD\n" +
                                $"3. Service principal is created in Exchange Online\n" +
                                $"4. Application has required permissions");
                        }
                        else
                        {
                            throw new InvalidOperationException($"Failed to connect to Exchange Online: {errors}");
                        }
                    }
                    
                    _logger.LogInformation("Successfully connected to Exchange Online");
                    
                    // Pagination support
                    bool hasMorePages = true;
                    string? pageCookie = null;
                    int pageNumber = 0;
                    const int maxPages = 20; // Safety limit to prevent infinite loops
                    const int pageSize = 5000;
                    
                    var startTime = startDate ?? DateTime.UtcNow.AddDays(-29);
                    var endTime = endDate ?? DateTime.UtcNow;
                    
                    while (hasMorePages && pageNumber < maxPages)
                    {
                        pageNumber++;
                        _logger.LogInformation($"Fetching page {pageNumber} of Activity Explorer data...");
                        
                        // Clear previous commands
                        ps.Commands.Clear();
                        
                        // Build Export-ActivityExplorerData command
                        var exportCommand = ps.AddCommand("Export-ActivityExplorerData")
                            .AddParameter("StartTime", startTime)
                            .AddParameter("EndTime", endTime)
                            .AddParameter("OutputFormat", "Json")
                            .AddParameter("PageSize", pageSize);
                        
                        // Add PageCookie for pagination (after first page)
                        if (!string.IsNullOrEmpty(pageCookie))
                        {
                            exportCommand.AddParameter("PageCookie", pageCookie);
                        }
                        
                        // Add filters if provided
                        if (!string.IsNullOrEmpty(workloadFilter))
                        {
                            exportCommand.AddParameter("Filter1", new string[] { workloadFilter });
                        }
                        
                        if (!string.IsNullOrEmpty(userFilter))
                        {
                            exportCommand.AddParameter("Filter2", new string[] { userFilter });
                        }
                        
                        // Build command string for logging
                        var commandString = $"Export-ActivityExplorerData -StartTime '{startTime:yyyy-MM-dd HH:mm:ss}' -EndTime '{endTime:yyyy-MM-dd HH:mm:ss}' -OutputFormat Json -PageSize {pageSize}";
                        if (!string.IsNullOrEmpty(pageCookie))
                        {
                            commandString += $" -PageCookie '...'"; // Abbreviated for logging
                        }
                        if (!string.IsNullOrEmpty(workloadFilter))
                        {
                            commandString += $" -Filter1 @('{workloadFilter}')";
                        }
                        if (!string.IsNullOrEmpty(userFilter))
                        {
                            commandString += $" -Filter2 @('{userFilter}')";
                        }
                        
                        _logger.LogInformation($"Executing: {commandString}");
                        
                        var results = await Task.Run(() => ps.Invoke());
                    
                        if (ps.HadErrors)
                        {
                            var errors = new List<string>();
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError($"PowerShell Error during data fetch: {error}");
                                errors.Add(error.ToString());
                            }
                            
                            if (errors.Any())
                            {
                                throw new InvalidOperationException(
                                    $"Failed to execute PowerShell command.\n" +
                                    $"Command: {commandString}\n" +
                                    $"Error: {string.Join("; ", errors)}");
                            }
                        }
                        
                        // Process results from Export-ActivityExplorerData
                        if (results.Any())
                        {
                            var result = results.First();
                            
                            // Use PSObject directly instead of BaseObject
                            var psObject = result as PSObject;
                            if (psObject == null)
                            {
                                _logger.LogError("Result is not a PSObject");
                                throw new InvalidOperationException("Unexpected result type from Export-ActivityExplorerData");
                            }
                            
                            // Check if the export was successful - access properties directly from PSObject
                            var resultCode = psObject.Properties["ResultCode"]?.Value?.ToString();
                            _logger.LogDebug($"ResultCode: {resultCode}");
                            
                            if (resultCode != "Success")
                            {
                                var errorData = psObject.Properties["ErrorData"]?.Value?.ToString();
                                throw new InvalidOperationException($"Export-ActivityExplorerData failed with ResultCode: {resultCode}, ErrorData: {errorData}");
                            }
                            
                            // Get metadata - access properties directly from PSObject
                            var waterMark = psObject.Properties["WaterMark"]?.Value?.ToString();
                            var lastPageValue = psObject.Properties["LastPage"]?.Value;
                            var lastPage = lastPageValue != null && Convert.ToBoolean(lastPageValue);
                            var totalResultCount = Convert.ToInt32(psObject.Properties["TotalResultCount"]?.Value ?? 0);
                            var recordCount = Convert.ToInt32(psObject.Properties["RecordCount"]?.Value ?? 0);
                            
                            _logger.LogInformation($"Page {pageNumber}: {recordCount} records, Total: {totalResultCount}, LastPage: {lastPage}");
                            
                            // Get the actual activity data from ResultData property
                            var resultDataJson = psObject.Properties["ResultData"]?.Value?.ToString();
                            
                            if (!string.IsNullOrEmpty(resultDataJson))
                            {
                                try
                                {
                                    // ResultData is a JSON array of activities
                                    var activityArray = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(resultDataJson);
                                    
                                    if (activityArray != null)
                                    {
                                        int pageActivities = 0;
                                        foreach (var activityData in activityArray)
                                        {
                                            try
                                            {
                                                var activity = new Activity
                                                {
                                                    Id = Guid.NewGuid(),
                                                    
                                                    // Core fields
                                                    Timestamp = ParseDateTime(activityData, "Happened") ?? 
                                                               ParseDateTime(activityData, "CreationTime") ?? 
                                                               DateTime.UtcNow,
                                                    UserId = GetDictionaryValue<string>(activityData, "User") ?? 
                                                            GetDictionaryValue<string>(activityData, "UserId"),
                                                    UserPrincipalName = GetDictionaryValue<string>(activityData, "User") ?? 
                                                                       GetDictionaryValue<string>(activityData, "UserPrincipalName"),
                                                    Operation = GetDictionaryValue<string>(activityData, "Activity"),
                                                    Workload = GetDictionaryValue<string>(activityData, "Workload"),
                                                    ResultStatus = GetDictionaryValue<string>(activityData, "ResultStatus") ?? "Success",
                                                    ClientIP = GetDictionaryValue<string>(activityData, "ClientIP") ?? 
                                                              GetDictionaryValue<string>(activityData, "ClientIp"),
                                                    
                                                    // Identity and classification fields
                                                    RecordIdentity = GetDictionaryValue<string>(activityData, "RecordIdentity"),
                                                    ActivityId = GetDictionaryValue<string>(activityData, "ActivityId"),
                                                    Application = GetDictionaryValue<string>(activityData, "Application"),
                                                    ContentType = GetDictionaryValue<string>(activityData, "ContentType"),
                                                    DataPlatform = GetDictionaryValue<string>(activityData, "DataPlatform"),
                                                    
                                                    // Device and location fields
                                                    DeviceName = GetDictionaryValue<string>(activityData, "DeviceName"),
                                                    Platform = GetDictionaryValue<string>(activityData, "Platform"),
                                                    SourceLocationType = GetDictionaryValue<string>(activityData, "SourceLocationType"),
                                                    
                                                    // File and item fields
                                                    FilePath = GetDictionaryValue<string>(activityData, "FilePath"),
                                                    ItemName = GetDictionaryValue<string>(activityData, "ItemName"),
                                                    FileSize = ParseLong(activityData, "FileSize"),
                                                    ObjectId = GetDictionaryValue<string>(activityData, "ItemName") ?? 
                                                              GetDictionaryValue<string>(activityData, "FilePath") ?? 
                                                              GetDictionaryValue<string>(activityData, "ObjectId"),
                                                    
                                                    // User type fields
                                                    UserType = GetDictionaryValue<string>(activityData, "UserType"),
                                                    UserSku = GetDictionaryValue<string>(activityData, "UserSku"),
                                                    
                                                    // Sensitivity and protection fields
                                                    SensitivityLabel = GetDictionaryValue<string>(activityData, "SensitivityLabel"),
                                                    HowApplied = GetDictionaryValue<string>(activityData, "HowApplied"),
                                                    HowAppliedDetail = GetDictionaryValue<string>(activityData, "HowAppliedDetail"),
                                                    LabelEventType = GetDictionaryValue<string>(activityData, "LabelEventType"),
                                                    ProtectionEventType = GetDictionaryValue<string>(activityData, "ProtectionEventType"),
                                                    
                                                    // Complex fields - serialize to JSON strings
                                                    EmailInfo = SerializeComplexField(activityData, "EmailInfo"),
                                                    PolicyMatchInfo = SerializeComplexField(activityData, "PolicyMatchInfo"),
                                                    SensitiveInfoTypeData = SerializeComplexField(activityData, "SensitiveInfoTypeData"),
                                                    SensitiveInfoTypeBucketsData = SerializeComplexField(activityData, "SensitiveInfoTypeBucketsData"),
                                                    SensitivityLabelIdsReferenced = SerializeComplexField(activityData, "SensitivityLabelIdsReferenced"),
                                                    AttachmentDetails = SerializeComplexField(activityData, "AttachmentDetails"),
                                                    
                                                    // Metadata
                                                    CreatedAt = DateTime.UtcNow
                                                };
                                                
                                                // Store the record identity as ObjectId if ObjectId is still empty
                                                if (string.IsNullOrEmpty(activity.ObjectId))
                                                {
                                                    activity.ObjectId = activity.RecordIdentity;
                                                }
                                                
                                                activities.Add(activity);
                                                pageActivities++;
                                                _logger.LogDebug($"Processed activity: {activity.Operation} by {activity.UserPrincipalName} at {activity.Timestamp}");
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger.LogWarning($"Failed to process individual activity record: {ex.Message}");
                                            }
                                        }
                                        
                                        _logger.LogInformation($"Page {pageNumber}: Processed {pageActivities} activities");
                                    }
                                }
                                catch (System.Text.Json.JsonException jsonEx)
                                {
                                    _logger.LogError($"Failed to parse ResultData JSON: {jsonEx.Message}");
                                    _logger.LogDebug($"ResultData content (first 1000 chars): {resultDataJson.Substring(0, Math.Min(1000, resultDataJson.Length))}");
                                }
                            }
                            else
                            {
                                _logger.LogWarning($"Page {pageNumber}: ResultData is empty or null");
                            }
                            
                            // Update pagination state
                            hasMorePages = !lastPage;
                            pageCookie = waterMark;
                            
                            if (hasMorePages && !string.IsNullOrEmpty(pageCookie))
                            {
                                _logger.LogInformation($"More pages available. Preparing to fetch page {pageNumber + 1}...");
                            }
                            else
                            {
                                _logger.LogInformation($"Reached last page. Total pages fetched: {pageNumber}");
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"Page {pageNumber}: Export-ActivityExplorerData returned no results");
                            hasMorePages = false;
                        }
                    } // End of while loop
                    
                    _logger.LogInformation($"Fetched {activities.Count} activities");
                    
                    // Disconnect
                    ps.Commands.Clear();
                    ps.AddCommand("Disconnect-ExchangeOnline")
                      .AddParameter("Confirm", false);
                    await Task.Run(() => ps.Invoke());
                }
                catch (InvalidOperationException)
                {
                    // Re-throw InvalidOperationException with command details
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch activities from Purview");
                    // Add command information to generic exceptions
                    throw new InvalidOperationException(
                        $"Failed to fetch activities from Purview.\n" +
                        $"Last PowerShell command attempted: Export-ActivityExplorerData\n" +
                        $"Error: {ex.Message}", ex);
                }
            }
            
            return activities;
        }
        
        private T? GetPropertyValue<T>(dynamic obj, string propertyName, T? defaultValue = default)
        {
            try
            {
                if (obj == null) return defaultValue;
                
                var psObject = obj as PSObject;
                if (psObject != null)
                {
                    var prop = psObject.Properties[propertyName];
                    if (prop != null && prop.Value != null)
                    {
                        if (typeof(T) == typeof(string))
                            return (T)(object)prop.Value.ToString();
                        return (T)prop.Value;
                    }
                }
                
                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }
        
        private DateTime? ParseDateTime(Dictionary<string, object> dict, string key)
        {
            try
            {
                if (dict != null && dict.ContainsKey(key) && dict[key] != null)
                {
                    var value = dict[key];
                    if (value is System.Text.Json.JsonElement jsonElement)
                    {
                        if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            if (DateTime.TryParse(jsonElement.GetString(), out var dateTime))
                                return dateTime;
                        }
                        return jsonElement.GetDateTime();
                    }
                    if (value is string strValue && DateTime.TryParse(strValue, out var dt))
                        return dt;
                    if (value is DateTime dateValue)
                        return dateValue;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        private T? GetDictionaryValue<T>(Dictionary<string, object> dict, string key, T? defaultValue = default)
        {
            try
            {
                if (dict != null && dict.ContainsKey(key) && dict[key] != null)
                {
                    var value = dict[key];
                    if (value is System.Text.Json.JsonElement jsonElement)
                    {
                        if (typeof(T) == typeof(string))
                            return (T)(object)jsonElement.GetString();
                        if (typeof(T) == typeof(DateTime))
                            return (T)(object)jsonElement.GetDateTime();
                    }
                    return (T)value;
                }
                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }
        
        private long? ParseLong(Dictionary<string, object> dict, string key)
        {
            try
            {
                if (dict != null && dict.ContainsKey(key) && dict[key] != null)
                {
                    var value = dict[key];
                    if (value is System.Text.Json.JsonElement jsonElement)
                    {
                        if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            return jsonElement.GetInt64();
                        }
                        else if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            if (long.TryParse(jsonElement.GetString(), out var longValue))
                                return longValue;
                        }
                    }
                    else if (value is long longVal)
                    {
                        return longVal;
                    }
                    else if (value is int intVal)
                    {
                        return intVal;
                    }
                    else if (value is string strVal && long.TryParse(strVal, out var parsed))
                    {
                        return parsed;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        private string? SerializeComplexField(Dictionary<string, object> dict, string key)
        {
            try
            {
                if (dict != null && dict.ContainsKey(key) && dict[key] != null)
                {
                    var value = dict[key];
                    
                    // If it's already a JsonElement, serialize it directly
                    if (value is System.Text.Json.JsonElement jsonElement)
                    {
                        // Don't serialize if it's null or empty array
                        if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Null)
                            return null;
                        if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array && jsonElement.GetArrayLength() == 0)
                            return null;
                            
                        return jsonElement.GetRawText();
                    }
                    
                    // Otherwise serialize the object
                    return System.Text.Json.JsonSerializer.Serialize(value);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to serialize complex field {key}: {ex.Message}");
                return null;
            }
        }
        
        public async Task<Dictionary<string, List<string>>> AnalyzeActivityColumnsAsync(int sampleSize = 100)
        {
            var columnAnalysis = new Dictionary<string, List<string>>();
            
            using (var ps = PowerShell.Create())
            {
                try
                {
                    // Set execution policy
                    ps.AddCommand("Set-ExecutionPolicy")
                      .AddParameter("ExecutionPolicy", "Bypass")
                      .AddParameter("Scope", "Process")
                      .AddParameter("Force");
                    await Task.Run(() => ps.Invoke());
                    ps.Commands.Clear();
                    
                    // Import module
                    ps.AddCommand("Import-Module")
                      .AddParameter("Name", "ExchangeOnlineManagement")
                      .AddParameter("ErrorAction", "Stop");
                    await Task.Run(() => ps.Invoke());
                    ps.Commands.Clear();
                    
                    _logger.LogInformation("Connecting to Exchange Online for column analysis...");
                    
                    // Connect to Exchange Online
                    ps.AddCommand("Connect-IPPSSession")
                      .AddParameter("CertificateThumbprint", _settings.CertificateThumbprint)
                      .AddParameter("AppId", _settings.AppId)
                      .AddParameter("Organization", _settings.Organization);
                    
                    await Task.Run(() => ps.Invoke());
                    
                    if (ps.HadErrors)
                    {
                        var errors = string.Join("; ", ps.Streams.Error.Select(e => e.ToString()));
                        throw new InvalidOperationException($"Failed to connect to Exchange Online: {errors}");
                    }
                    
                    ps.Commands.Clear();
                    
                    // Fetch a sample of activities
                    _logger.LogInformation($"Fetching sample of {sampleSize} activities for column analysis...");
                    
                    var exportCommand = ps.AddCommand("Export-ActivityExplorerData")
                        .AddParameter("StartTime", DateTime.UtcNow.AddDays(-7))
                        .AddParameter("EndTime", DateTime.UtcNow)
                        .AddParameter("OutputFormat", "Json")
                        .AddParameter("PageSize", sampleSize);
                    
                    var results = await Task.Run(() => ps.Invoke());
                    
                    if (ps.HadErrors)
                    {
                        var errors = string.Join("; ", ps.Streams.Error.Select(e => e.ToString()));
                        throw new InvalidOperationException($"Failed to export activity data: {errors}");
                    }
                    
                    if (results.Any())
                    {
                        var result = results.First();
                        var psObject = result as PSObject;
                        
                        if (psObject != null)
                        {
                            var resultDataJson = psObject.Properties["ResultData"]?.Value?.ToString();
                            
                            if (!string.IsNullOrEmpty(resultDataJson))
                            {
                                var activityArray = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(resultDataJson);
                                
                                if (activityArray != null && activityArray.Any())
                                {
                                    // Analyze each activity to find all unique columns
                                    var allColumns = new HashSet<string>();
                                    var columnSamples = new Dictionary<string, HashSet<string>>();
                                    
                                    foreach (var activity in activityArray)
                                    {
                                        foreach (var kvp in activity)
                                        {
                                            allColumns.Add(kvp.Key);
                                            
                                            // Collect sample values for each column
                                            if (!columnSamples.ContainsKey(kvp.Key))
                                            {
                                                columnSamples[kvp.Key] = new HashSet<string>();
                                            }
                                            
                                            // Add sample value (limit to 10 samples per column)
                                            if (columnSamples[kvp.Key].Count < 10)
                                            {
                                                var sampleValue = kvp.Value?.ToString() ?? "null";
                                                
                                                // Handle JsonElement
                                                if (kvp.Value is System.Text.Json.JsonElement jsonElement)
                                                {
                                                    if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                                                    {
                                                        sampleValue = "[Object with properties: " + 
                                                            string.Join(", ", jsonElement.EnumerateObject().Select(p => p.Name)) + "]";
                                                    }
                                                    else if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                                                    {
                                                        sampleValue = $"[Array with {jsonElement.GetArrayLength()} items]";
                                                    }
                                                    else
                                                    {
                                                        sampleValue = jsonElement.ToString();
                                                    }
                                                }
                                                
                                                // Truncate long values
                                                if (sampleValue.Length > 100)
                                                {
                                                    sampleValue = sampleValue.Substring(0, 100) + "...";
                                                }
                                                
                                                columnSamples[kvp.Key].Add(sampleValue);
                                            }
                                        }
                                    }
                                    
                                    // Convert to result format
                                    foreach (var column in allColumns.OrderBy(c => c))
                                    {
                                        columnAnalysis[column] = columnSamples[column].Take(5).ToList();
                                    }
                                    
                                    _logger.LogInformation($"Found {allColumns.Count} unique columns across {activityArray.Count} activities");
                                }
                            }
                        }
                    }
                    
                    // Disconnect
                    ps.Commands.Clear();
                    ps.AddCommand("Disconnect-ExchangeOnline")
                      .AddParameter("Confirm", false);
                    await Task.Run(() => ps.Invoke());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to analyze activity columns");
                    throw;
                }
            }
            
            return columnAnalysis;
        }
    }
}