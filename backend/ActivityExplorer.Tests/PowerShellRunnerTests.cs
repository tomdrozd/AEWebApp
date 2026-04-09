using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ActivityExplorer.Services;

namespace ActivityExplorer.Tests;

public class PowerShellRunnerTests
{
    private readonly Mock<ILogger<PowerShellRunner>> _loggerMock = new();

    [Fact]
    public async Task CheckPwshAsync_ReturnsTrueWhenPwshIsAvailable()
    {
        var runner = new PowerShellRunner(_loggerMock.Object, ".");
        var (available, version) = await runner.CheckPwshAsync();

        // pwsh should be on PATH in the dev environment
        available.Should().BeTrue();
        version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CheckScripts_AllMissing_ReturnsMissingList()
    {
        var runner = new PowerShellRunner(_loggerMock.Object, Path.GetTempPath());
        var (allFound, missing) = runner.CheckScripts();

        allFound.Should().BeFalse();
        missing.Should().Contain("Sync-Activities.ps1");
        missing.Should().Contain("Get-AuthStatus.ps1");
        missing.Should().Contain("Analyze-Columns.ps1");
    }

    [Fact]
    public void CheckScripts_AllPresent_ReturnsAllFound()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Sync-Activities.ps1"), "# test");
            File.WriteAllText(Path.Combine(tempDir, "Get-AuthStatus.ps1"), "# test");
            File.WriteAllText(Path.Combine(tempDir, "Analyze-Columns.ps1"), "# test");

            var runner = new PowerShellRunner(_loggerMock.Object, tempDir);
            var (allFound, missing) = runner.CheckScripts();

            allFound.Should().BeTrue();
            missing.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RunScriptAsync_NonExistentScript_ThrowsFileNotFound()
    {
        var runner = new PowerShellRunner(_loggerMock.Object, Path.GetTempPath());

        var act = () => runner.RunScriptAsync<string>("nonexistent.ps1", new Dictionary<string, string>());

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task RunScriptAsync_ScriptReturnsJson_Deserializes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Create a simple PS1 that outputs JSON
            var script = @"@{ Name = 'Test'; Value = 42 } | ConvertTo-Json -Compress";
            File.WriteAllText(Path.Combine(tempDir, "test.ps1"), script);

            var runner = new PowerShellRunner(_loggerMock.Object, tempDir);
            var result = await runner.RunScriptAsync<Dictionary<string, object>>(
                "test.ps1", new Dictionary<string, string>());

            result.Should().ContainKey("Name");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RunScriptAsync_ScriptFails_ThrowsWithExitCode()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "fail.ps1"), "exit 1");

            var runner = new PowerShellRunner(_loggerMock.Object, tempDir);
            var act = () => runner.RunScriptAsync<string>("fail.ps1", new Dictionary<string, string>());

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*exit code 1*");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RunScriptAsync_Timeout_ThrowsTimeoutException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "slow.ps1"), "Start-Sleep -Seconds 30");

            var runner = new PowerShellRunner(_loggerMock.Object, tempDir);
            var act = () => runner.RunScriptAsync<string>(
                "slow.ps1", new Dictionary<string, string>(), TimeSpan.FromSeconds(1));

            await act.Should().ThrowAsync<TimeoutException>();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
