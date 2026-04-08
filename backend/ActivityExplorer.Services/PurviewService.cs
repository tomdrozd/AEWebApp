using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
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
        private readonly PowerShellRunner _runner;

        public PurviewService(
            ILogger<PurviewService> logger,
            IOptions<PurviewSettings> settings,
            PowerShellRunner runner)
        {
            _logger = logger;
            _settings = settings.Value;
            _runner = runner;
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

            if (errors.Count > 0)
            {
                var errorMessage = "Purview configuration is incomplete:\n" + string.Join("\n", errors);
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
        }

        public async Task<AuthStatusDto> GetAuthenticationStatusAsync()
        {
            var status = new AuthStatusDto();

            // 1. Check pwsh.exe availability (C# level)
            var (pwshAvailable, pwshVersion) = await _runner.CheckPwshAsync();
            status.IsPwshAvailable = pwshAvailable;
            status.PwshVersion = pwshVersion;

            if (!pwshAvailable)
            {
                status.Recommendations.Add("pwsh.exe (PowerShell 7+) not found on PATH.");
                status.Recommendations.Add("Install from: https://github.com/PowerShell/PowerShell/releases");
                return status;
            }

            // 2. Check PS1 scripts exist (C# level)
            var (scriptsFound, missingScripts) = _runner.CheckScripts();
            status.AreScriptsFound = scriptsFound;
            status.MissingScripts = missingScripts;

            if (!scriptsFound)
            {
                status.Recommendations.Add("Missing PowerShell scripts: " + string.Join(", ", missingScripts));
                return status;
            }

            // 3. Check configuration
            CheckConfiguration(status);
            if (!status.IsConfigurationValid)
                return status;

            // 4. Run Get-AuthStatus.ps1 for module, certificate, and connection checks
            try
            {
                var psStatus = await _runner.RunScriptAsync<AuthStatusDto>("Get-AuthStatus.ps1",
                    BuildAuthParams());

                // Merge PS1 results into our status (preserve C#-level fields)
                status.PowerShellVersion = psStatus.PowerShellVersion;
                status.IsModuleInstalled = psStatus.IsModuleInstalled;
                status.ModuleVersion = psStatus.ModuleVersion;
                status.ModulePath = psStatus.ModulePath;
                status.ModuleInstallCommand = psStatus.ModuleInstallCommand;
                status.SearchedPaths = psStatus.SearchedPaths;
                status.IsCertificateFound = psStatus.IsCertificateFound;
                status.CertificateThumbprint = psStatus.CertificateThumbprint;
                status.CertificateSubject = psStatus.CertificateSubject;
                status.CertificateExpiry = psStatus.CertificateExpiry;
                status.IsCertificateExpired = psStatus.IsCertificateExpired;
                status.CertificateStore = psStatus.CertificateStore;
                status.CanConnect = psStatus.CanConnect;
                status.ConnectionTestResult = psStatus.ConnectionTestResult;
                status.LastConnectionError = psStatus.LastConnectionError;
                status.ConfiguredValues = psStatus.ConfiguredValues;
                status.Recommendations.AddRange(psStatus.Recommendations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Get-AuthStatus.ps1 failed");
                status.Recommendations.Add($"Auth status check failed: {ex.Message}");
            }

            return status;
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
            var parameters = BuildAuthParams();

            if (startDate.HasValue)
                parameters["StartTime"] = startDate.Value.ToString("o");
            if (endDate.HasValue)
                parameters["EndTime"] = endDate.Value.ToString("o");
            if (!string.IsNullOrEmpty(workloadFilter))
                parameters["WorkloadFilter"] = workloadFilter;
            if (!string.IsNullOrEmpty(userFilter))
                parameters["UserFilter"] = userFilter;

            _logger.LogInformation("Starting activity sync via Sync-Activities.ps1...");

            var rawActivities = await _runner.RunScriptAsync<List<Dictionary<string, object>>>(
                "Sync-Activities.ps1", parameters, TimeSpan.FromMinutes(15));

            _logger.LogInformation("Received {Count} raw activities from PowerShell", rawActivities.Count);

            var activities = new List<Activity>();
            foreach (var activityData in rawActivities)
            {
                try
                {
                    var activity = MapToActivity(activityData);
                    activities.Add(activity);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to process activity record: {Message}", ex.Message);
                }
            }

            _logger.LogInformation("Mapped {Count} activities", activities.Count);
            return activities;
        }

        public async Task<Dictionary<string, List<string>>> AnalyzeActivityColumnsAsync(int sampleSize = 100)
        {
            var parameters = BuildAuthParams();
            parameters["SampleSize"] = sampleSize.ToString();

            _logger.LogInformation("Starting column analysis via Analyze-Columns.ps1...");

            return await _runner.RunScriptAsync<Dictionary<string, List<string>>>(
                "Analyze-Columns.ps1", parameters);
        }

        // --- Private helpers ---

        private Dictionary<string, string> BuildAuthParams()
        {
            return new Dictionary<string, string>
            {
                ["Organization"] = _settings.Organization,
                ["AppId"] = _settings.AppId,
                ["CertificateThumbprint"] = _settings.CertificateThumbprint
            };
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

        private Activity MapToActivity(Dictionary<string, object> data)
        {
            var activity = new Activity
            {
                Id = Guid.NewGuid(),

                // Core fields
                Timestamp = ParseDateTime(data, "Happened") ??
                           ParseDateTime(data, "CreationTime") ??
                           DateTime.UtcNow,
                UserId = GetValue<string>(data, "User") ??
                        GetValue<string>(data, "UserId"),
                UserPrincipalName = GetValue<string>(data, "User") ??
                                   GetValue<string>(data, "UserPrincipalName"),
                Operation = GetValue<string>(data, "Activity"),
                Workload = GetValue<string>(data, "Workload"),
                ResultStatus = GetValue<string>(data, "ResultStatus") ?? "Success",
                ClientIP = GetValue<string>(data, "ClientIP") ??
                          GetValue<string>(data, "ClientIp"),

                // Identity and classification
                RecordIdentity = GetValue<string>(data, "RecordIdentity"),
                ActivityId = GetValue<string>(data, "ActivityId"),
                Application = GetValue<string>(data, "Application"),
                ContentType = GetValue<string>(data, "ContentType"),
                DataPlatform = GetValue<string>(data, "DataPlatform"),

                // Device and location
                DeviceName = GetValue<string>(data, "DeviceName"),
                Platform = GetValue<string>(data, "Platform"),
                SourceLocationType = GetValue<string>(data, "SourceLocationType"),

                // File and item
                FilePath = GetValue<string>(data, "FilePath"),
                ItemName = GetValue<string>(data, "ItemName"),
                FileSize = ParseLong(data, "FileSize"),
                ObjectId = GetValue<string>(data, "ItemName") ??
                          GetValue<string>(data, "FilePath") ??
                          GetValue<string>(data, "ObjectId"),

                // User type
                UserType = GetValue<string>(data, "UserType"),
                UserSku = GetValue<string>(data, "UserSku"),

                // Sensitivity and protection
                SensitivityLabel = GetValue<string>(data, "SensitivityLabel"),
                HowApplied = GetValue<string>(data, "HowApplied"),
                HowAppliedDetail = GetValue<string>(data, "HowAppliedDetail"),
                LabelEventType = GetValue<string>(data, "LabelEventType"),
                ProtectionEventType = GetValue<string>(data, "ProtectionEventType"),

                // Complex fields
                EmailInfo = SerializeComplexField(data, "EmailInfo"),
                PolicyMatchInfo = SerializeComplexField(data, "PolicyMatchInfo"),
                SensitiveInfoTypeData = SerializeComplexField(data, "SensitiveInfoTypeData"),
                SensitiveInfoTypeBucketsData = SerializeComplexField(data, "SensitiveInfoTypeBucketsData"),
                SensitivityLabelIdsReferenced = SerializeComplexField(data, "SensitivityLabelIdsReferenced"),
                AttachmentDetails = SerializeComplexField(data, "AttachmentDetails"),

                CreatedAt = DateTime.UtcNow
            };

            if (string.IsNullOrEmpty(activity.ObjectId))
                activity.ObjectId = activity.RecordIdentity;

            return activity;
        }

        private static T? GetValue<T>(Dictionary<string, object> dict, string key, T? defaultValue = default)
        {
            try
            {
                if (dict.TryGetValue(key, out var value) && value != null)
                {
                    if (value is JsonElement jsonElement)
                    {
                        if (typeof(T) == typeof(string))
                            return (T)(object)(jsonElement.GetString() ?? string.Empty);
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

        private static DateTime? ParseDateTime(Dictionary<string, object> dict, string key)
        {
            try
            {
                if (dict.TryGetValue(key, out var value) && value != null)
                {
                    if (value is JsonElement jsonElement)
                    {
                        if (jsonElement.ValueKind == JsonValueKind.String)
                        {
                            if (DateTime.TryParse(jsonElement.GetString(), out var dt))
                                return dt;
                        }
                        return jsonElement.GetDateTime();
                    }
                    if (value is string strValue && DateTime.TryParse(strValue, out var dt2))
                        return dt2;
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

        private static long? ParseLong(Dictionary<string, object> dict, string key)
        {
            try
            {
                if (dict.TryGetValue(key, out var value) && value != null)
                {
                    if (value is JsonElement jsonElement)
                    {
                        if (jsonElement.ValueKind == JsonValueKind.Number)
                            return jsonElement.GetInt64();
                        if (jsonElement.ValueKind == JsonValueKind.String &&
                            long.TryParse(jsonElement.GetString(), out var parsed))
                            return parsed;
                    }
                    else if (value is long longVal) return longVal;
                    else if (value is int intVal) return intVal;
                    else if (value is string strVal && long.TryParse(strVal, out var p)) return p;
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
                if (dict.TryGetValue(key, out var value) && value != null)
                {
                    if (value is JsonElement jsonElement)
                    {
                        if (jsonElement.ValueKind == JsonValueKind.Null)
                            return null;
                        if (jsonElement.ValueKind == JsonValueKind.Array && jsonElement.GetArrayLength() == 0)
                            return null;
                        return jsonElement.GetRawText();
                    }
                    return JsonSerializer.Serialize(value);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to serialize complex field {Key}: {Message}", key, ex.Message);
                return null;
            }
        }
    }
}
