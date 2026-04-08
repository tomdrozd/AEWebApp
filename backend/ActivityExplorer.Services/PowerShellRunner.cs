using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ActivityExplorer.Services
{
    public class PowerShellRunner
    {
        private readonly ILogger<PowerShellRunner> _logger;
        private readonly string _scriptsPath;
        private static readonly string[] RequiredScripts =
        {
            "Sync-Activities.ps1",
            "Get-AuthStatus.ps1",
            "Analyze-Columns.ps1"
        };

        public PowerShellRunner(ILogger<PowerShellRunner> logger, string scriptsPath)
        {
            _logger = logger;
            _scriptsPath = scriptsPath;
        }

        /// <summary>
        /// Checks if pwsh.exe is available and returns its version.
        /// </summary>
        public async Task<(bool Available, string? Version)> CheckPwshAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "pwsh",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return (false, null);

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    var version = output.Trim().Replace("PowerShell ", "");
                    return (true, version);
                }

                return (false, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "pwsh.exe not found on PATH");
                return (false, null);
            }
        }

        /// <summary>
        /// Checks which required PS1 scripts are present.
        /// </summary>
        public (bool AllFound, List<string> Missing) CheckScripts()
        {
            var missing = new List<string>();
            foreach (var script in RequiredScripts)
            {
                var path = Path.Combine(_scriptsPath, script);
                if (!File.Exists(path))
                    missing.Add(script);
            }
            return (missing.Count == 0, missing);
        }

        /// <summary>
        /// Runs a PS1 script and deserializes JSON stdout to T.
        /// </summary>
        public async Task<T> RunScriptAsync<T>(string scriptName, Dictionary<string, string> parameters,
            TimeSpan? timeout = null)
        {
            var scriptPath = Path.Combine(_scriptsPath, scriptName);
            if (!File.Exists(scriptPath))
                throw new FileNotFoundException($"PowerShell script not found: {scriptPath}");

            var args = new StringBuilder();
            args.Append($"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\"");

            foreach (var (key, value) in parameters)
            {
                args.Append($" -{key} \"{value}\"");
            }

            _logger.LogInformation("Running: pwsh {Args}", args.ToString());

            var psi = new ProcessStartInfo
            {
                FileName = "pwsh",
                Arguments = args.ToString(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start pwsh.exe process");

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(10);
            var completed = await Task.Run(() => process.WaitForExit((int)effectiveTimeout.TotalMilliseconds));

            if (!completed)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException(
                    $"PowerShell script '{scriptName}' timed out after {effectiveTimeout.TotalMinutes} minutes");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            // Log stderr as informational (PS1 scripts use it for progress)
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                foreach (var line in stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    _logger.LogInformation("[pwsh] {Line}", line.TrimEnd());
                }
            }

            if (process.ExitCode != 0)
            {
                _logger.LogError("pwsh exited with code {ExitCode}. stderr: {Stderr}", process.ExitCode, stderr);
                throw new InvalidOperationException(
                    $"PowerShell script '{scriptName}' failed with exit code {process.ExitCode}: {stderr}");
            }

            if (string.IsNullOrWhiteSpace(stdout))
            {
                throw new InvalidOperationException(
                    $"PowerShell script '{scriptName}' produced no output on stdout");
            }

            // Log raw stdout for debugging (first 200 chars)
            _logger.LogDebug("Raw stdout from {Script} (first 200 chars): {Output}",
                scriptName, stdout.Substring(0, Math.Min(200, stdout.Length)));

            // Strip any non-JSON prefix (e.g. module import messages leaked to stdout)
            var jsonStart = stdout.IndexOfAny(new[] { '{', '[' });
            if (jsonStart > 0)
            {
                var skipped = stdout.Substring(0, jsonStart);
                _logger.LogWarning("Stripped non-JSON prefix from {Script} stdout: {Prefix}",
                    scriptName, skipped.Trim());
                stdout = stdout.Substring(jsonStart);
            }

            try
            {
                var result = JsonSerializer.Deserialize<T>(stdout, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result ?? throw new InvalidOperationException(
                    $"PowerShell script '{scriptName}' returned null after deserialization");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse JSON from {Script}. Output (first 500 chars): {Output}",
                    scriptName, stdout.Substring(0, Math.Min(500, stdout.Length)));
                throw new InvalidOperationException(
                    $"Failed to parse JSON output from '{scriptName}': {ex.Message}", ex);
            }
        }
    }
}
