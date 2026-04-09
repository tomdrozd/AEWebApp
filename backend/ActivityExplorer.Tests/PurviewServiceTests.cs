using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ActivityExplorer.Services;

namespace ActivityExplorer.Tests;

public class PurviewServiceTests
{
    private readonly PurviewService _service;

    public PurviewServiceTests()
    {
        var logger = new Mock<ILogger<PurviewService>>();
        var runner = new Mock<PowerShellRunner>(
            new Mock<ILogger<PowerShellRunner>>().Object, ".") { CallBase = false };
        var settings = Options.Create(new PurviewSettings
        {
            Organization = "test.onmicrosoft.com",
            AppId = "test-app-id",
            TenantId = "test-tenant-id",
            CertificateThumbprint = "AABBCCDD"
        });

        _service = new PurviewService(logger.Object, settings, runner.Object);
    }

    [Fact]
    public void ValidateSettings_MissingOrganization_Throws()
    {
        var logger = new Mock<ILogger<PurviewService>>();
        var runner = new Mock<PowerShellRunner>(
            new Mock<ILogger<PowerShellRunner>>().Object, ".") { CallBase = false };
        var settings = Options.Create(new PurviewSettings
        {
            Organization = "",
            AppId = "test",
            TenantId = "test",
            CertificateThumbprint = "test"
        });

        var act = () => new PurviewService(logger.Object, settings, runner.Object);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Organization*");
    }

    [Fact]
    public void ValidateSettings_AllMissing_ListsAllErrors()
    {
        var logger = new Mock<ILogger<PurviewService>>();
        var runner = new Mock<PowerShellRunner>(
            new Mock<ILogger<PowerShellRunner>>().Object, ".") { CallBase = false };
        // PurviewSettings has defaults for Organization, so override to blank
        var settings = Options.Create(new PurviewSettings
        {
            Organization = ""
        });

        var act = () => new PurviewService(logger.Object, settings, runner.Object);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Organization")
            .And.Contain("AppId")
            .And.Contain("TenantId")
            .And.Contain("CertificateThumbprint");
    }

    [Fact]
    public void MapToActivity_CoreFields_MappedCorrectly()
    {
        var data = new Dictionary<string, object>
        {
            ["Happened"] = JsonSerializer.SerializeToElement("2026-04-08T10:00:00Z"),
            ["User"] = JsonSerializer.SerializeToElement("john@contoso.com"),
            ["Activity"] = JsonSerializer.SerializeToElement("FileAccessed"),
            ["Workload"] = JsonSerializer.SerializeToElement("SharePoint"),
            ["ResultStatus"] = JsonSerializer.SerializeToElement("Success"),
            ["ClientIP"] = JsonSerializer.SerializeToElement("10.0.0.1"),
        };

        var activity = _service.MapToActivity(data);

        activity.Timestamp.Year.Should().Be(2026);
        activity.Timestamp.Month.Should().Be(4);
        activity.Timestamp.Day.Should().Be(8);
        activity.UserId.Should().Be("john@contoso.com");
        activity.UserPrincipalName.Should().Be("john@contoso.com");
        activity.Operation.Should().Be("FileAccessed");
        activity.Workload.Should().Be("SharePoint");
        activity.ResultStatus.Should().Be("Success");
        activity.ClientIP.Should().Be("10.0.0.1");
    }

    [Fact]
    public void MapToActivity_MissingFields_DefaultsApplied()
    {
        var data = new Dictionary<string, object>
        {
            ["Happened"] = JsonSerializer.SerializeToElement("2026-01-01T00:00:00Z"),
        };

        var activity = _service.MapToActivity(data);

        activity.ResultStatus.Should().Be("Success"); // default
        activity.UserId.Should().BeNull();
        activity.Operation.Should().BeNull();
        activity.Workload.Should().BeNull();
    }

    [Fact]
    public void MapToActivity_FileFields_MappedCorrectly()
    {
        var data = new Dictionary<string, object>
        {
            ["Happened"] = JsonSerializer.SerializeToElement("2026-04-08T10:00:00Z"),
            ["FilePath"] = JsonSerializer.SerializeToElement("/sites/docs/file.docx"),
            ["ItemName"] = JsonSerializer.SerializeToElement("file.docx"),
            ["FileSize"] = JsonSerializer.SerializeToElement(12345),
        };

        var activity = _service.MapToActivity(data);

        activity.FilePath.Should().Be("/sites/docs/file.docx");
        activity.ItemName.Should().Be("file.docx");
        activity.FileSize.Should().Be(12345);
        activity.ObjectId.Should().Be("file.docx"); // ItemName takes priority
    }

    [Fact]
    public void MapToActivity_SensitivityFields_MappedCorrectly()
    {
        var data = new Dictionary<string, object>
        {
            ["Happened"] = JsonSerializer.SerializeToElement("2026-04-08T10:00:00Z"),
            ["SensitivityLabel"] = JsonSerializer.SerializeToElement("Confidential"),
            ["HowApplied"] = JsonSerializer.SerializeToElement("Auto"),
            ["LabelEventType"] = JsonSerializer.SerializeToElement("LabelApplied"),
        };

        var activity = _service.MapToActivity(data);

        activity.SensitivityLabel.Should().Be("Confidential");
        activity.HowApplied.Should().Be("Auto");
        activity.LabelEventType.Should().Be("LabelApplied");
    }

    [Fact]
    public void MapToActivity_ComplexFields_SerializedAsJson()
    {
        var policyData = JsonSerializer.SerializeToElement(new[] { new { Name = "DLP-Rule-1", Match = true } });
        var data = new Dictionary<string, object>
        {
            ["Happened"] = JsonSerializer.SerializeToElement("2026-04-08T10:00:00Z"),
            ["PolicyMatchInfo"] = policyData,
        };

        var activity = _service.MapToActivity(data);

        activity.PolicyMatchInfo.Should().NotBeNullOrEmpty();
        activity.PolicyMatchInfo.Should().Contain("DLP-Rule-1");
    }

    [Fact]
    public void MapToActivity_EmptyComplexArray_ReturnsNull()
    {
        var data = new Dictionary<string, object>
        {
            ["Happened"] = JsonSerializer.SerializeToElement("2026-04-08T10:00:00Z"),
            ["PolicyMatchInfo"] = JsonSerializer.SerializeToElement(new object[0]),
        };

        var activity = _service.MapToActivity(data);

        activity.PolicyMatchInfo.Should().BeNull();
    }

    [Fact]
    public void MapToActivity_ObjectIdFallback_UsesRecordIdentity()
    {
        var data = new Dictionary<string, object>
        {
            ["Happened"] = JsonSerializer.SerializeToElement("2026-04-08T10:00:00Z"),
            ["RecordIdentity"] = JsonSerializer.SerializeToElement("record-123"),
        };

        var activity = _service.MapToActivity(data);

        activity.ObjectId.Should().Be("record-123");
    }

    [Fact]
    public void MapToActivity_GeneratesUniqueId()
    {
        var data = new Dictionary<string, object>
        {
            ["Happened"] = JsonSerializer.SerializeToElement("2026-04-08T10:00:00Z"),
        };

        var a1 = _service.MapToActivity(data);
        var a2 = _service.MapToActivity(data);

        a1.Id.Should().NotBe(a2.Id);
        a1.Id.Should().NotBe(Guid.Empty);
    }
}
