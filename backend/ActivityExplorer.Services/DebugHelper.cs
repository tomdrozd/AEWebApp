using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Text.Json;

namespace ActivityExplorer.Services
{
    /// <summary>
    /// Helper class for debugging PowerShell command results
    /// </summary>
    public static class DebugHelper
    {
        /// <summary>
        /// Inspects a PowerShell result object and returns detailed information
        /// Use this in the VS Code debug console: DebugHelper.InspectPSObject(results[0])
        /// </summary>
        public static string InspectPSObject(PSObject psObject)
        {
            if (psObject == null)
                return "PSObject is null";

            var sb = new StringBuilder();
            sb.AppendLine("=== PSObject Inspection ===");
            
            // Type information
            sb.AppendLine($"Type: {psObject.BaseObject?.GetType()?.FullName ?? "Unknown"}");
            sb.AppendLine($"TypeNames: {string.Join(", ", psObject.TypeNames)}");
            
            // Properties
            sb.AppendLine("\n=== Properties ===");
            foreach (var prop in psObject.Properties)
            {
                try
                {
                    var value = prop.Value;
                    var valueStr = value?.ToString() ?? "null";
                    
                    // Truncate long values
                    if (valueStr.Length > 200)
                        valueStr = valueStr.Substring(0, 200) + "... (truncated)";
                        
                    sb.AppendLine($"{prop.Name}: {valueStr} [{value?.GetType()?.Name ?? "null"}]");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"{prop.Name}: Error reading value - {ex.Message}");
                }
            }
            
            // Methods (just list them)
            sb.AppendLine("\n=== Methods ===");
            var methods = psObject.Methods.Select(m => m.Name).Distinct().OrderBy(m => m);
            sb.AppendLine(string.Join(", ", methods));
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Extracts all properties as a dictionary for easier inspection
        /// Use this in the VS Code debug console: DebugHelper.ExtractProperties(results[0])
        /// </summary>
        public static Dictionary<string, object> ExtractProperties(PSObject psObject)
        {
            var dict = new Dictionary<string, object>();
            
            if (psObject == null)
                return dict;
                
            foreach (var prop in psObject.Properties)
            {
                try
                {
                    dict[prop.Name] = prop.Value;
                }
                catch
                {
                    dict[prop.Name] = "Error reading value";
                }
            }
            
            return dict;
        }
        
        /// <summary>
        /// Converts PSObject to JSON for easy viewing
        /// Use this in the VS Code debug console: DebugHelper.ToJson(results[0])
        /// </summary>
        public static string ToJson(PSObject psObject)
        {
            try
            {
                var dict = ExtractProperties(psObject);
                return JsonSerializer.Serialize(dict, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    MaxDepth = 5
                });
            }
            catch (Exception ex)
            {
                return $"Error converting to JSON: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Gets a specific property value safely
        /// Use this in the VS Code debug console: DebugHelper.GetProperty(results[0], "ResultData")
        /// </summary>
        public static object GetProperty(PSObject psObject, string propertyName)
        {
            if (psObject == null)
                return null;
                
            var prop = psObject.Properties[propertyName];
            return prop?.Value;
        }
        
        /// <summary>
        /// Analyzes Export-ActivityExplorerData result specifically
        /// Use this in the VS Code debug console: DebugHelper.AnalyzeExportResult(results[0])
        /// </summary>
        public static string AnalyzeExportResult(PSObject psObject)
        {
            if (psObject == null)
                return "PSObject is null";
                
            var sb = new StringBuilder();
            sb.AppendLine("=== Export-ActivityExplorerData Result Analysis ===");
            
            // Expected properties
            var expectedProps = new[] 
            {
                "ResultCode", "ResultData", "WaterMark", "LastPage", 
                "TotalResultCount", "RecordCount", "ErrorData", "Identity",
                "IsValid", "ObjectState"
            };
            
            foreach (var propName in expectedProps)
            {
                var prop = psObject.Properties[propName];
                if (prop != null)
                {
                    var value = prop.Value;
                    var valueStr = value?.ToString() ?? "null";
                    
                    // Special handling for ResultData
                    if (propName == "ResultData" && valueStr.Length > 500)
                    {
                        valueStr = valueStr.Substring(0, 500) + "... (truncated)";
                        sb.AppendLine($"{propName}: [JSON Data - {value.ToString().Length} chars]");
                        
                        // Try to parse and show first record
                        try
                        {
                            var json = JsonDocument.Parse(value.ToString());
                            if (json.RootElement.ValueKind == JsonValueKind.Array)
                            {
                                sb.AppendLine($"  - Array with {json.RootElement.GetArrayLength()} items");
                                if (json.RootElement.GetArrayLength() > 0)
                                {
                                    var firstItem = json.RootElement[0];
                                    sb.AppendLine("  - First item properties:");
                                    foreach (var itemProp in firstItem.EnumerateObject())
                                    {
                                        sb.AppendLine($"    - {itemProp.Name}: {itemProp.Value.ValueKind}");
                                    }
                                }
                            }
                        }
                        catch
                        {
                            sb.AppendLine("  - Unable to parse as JSON");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"{propName}: {valueStr} [{value?.GetType()?.Name ?? "null"}]");
                    }
                }
                else
                {
                    sb.AppendLine($"{propName}: [Property not found]");
                }
            }
            
            // List all actual properties not in expected list
            sb.AppendLine("\n=== Other Properties ===");
            foreach (var prop in psObject.Properties)
            {
                if (!expectedProps.Contains(prop.Name))
                {
                    sb.AppendLine($"{prop.Name}: {prop.Value?.GetType()?.Name ?? "null"}");
                }
            }
            
            return sb.ToString();
        }
    }
}