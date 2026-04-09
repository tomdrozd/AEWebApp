using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ActivityExplorer.API.Controllers;
using ActivityExplorer.Core.DTOs;
using ActivityExplorer.Core.Models;
using ActivityExplorer.Data;
using ActivityExplorer.Services;

namespace ActivityExplorer.Tests;

public class ActivitiesControllerTests : IAsyncLifetime
{
    private ActivityContext _context = null!;
    private ActivitiesController _controller = null!;

    public async Task InitializeAsync()
    {
        _context = await TestDbHelper.CreateSeededContext();

        var loggerCtrl = new Mock<ILogger<ActivitiesController>>();
        var loggerPs = new Mock<ILogger<PurviewService>>();
        var loggerSync = new Mock<ILogger<ActivitySyncService>>();
        var runner = new Mock<PowerShellRunner>(
            new Mock<ILogger<PowerShellRunner>>().Object, ".") { CallBase = false };
        var settings = Options.Create(new PurviewSettings
        {
            Organization = "test.onmicrosoft.com",
            AppId = "test",
            TenantId = "test",
            CertificateThumbprint = "test"
        });

        var purviewService = new Mock<PurviewService>(loggerPs.Object, settings, runner.Object) { CallBase = false };
        var syncService = new ActivitySyncService(purviewService.Object, _context, loggerSync.Object);

        _controller = new ActivitiesController(_context, purviewService.Object, syncService, loggerCtrl.Object);
    }

    public Task DisposeAsync()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetActivities_ReturnsAllActivities()
    {
        var filter = new ActivityFilterDto { PageNumber = 1, PageSize = 50 };
        var result = await _controller.GetActivities(filter);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeAssignableTo<PagedResult<Activity>>().Subject;

        paged.TotalCount.Should().Be(3);
        paged.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetActivities_FilterByWorkload_ReturnsFiltered()
    {
        var filter = new ActivityFilterDto
        {
            PageNumber = 1,
            PageSize = 50,
            Workloads = new List<string> { "SharePoint" }
        };

        var result = await _controller.GetActivities(filter);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeAssignableTo<PagedResult<Activity>>().Subject;

        paged.TotalCount.Should().Be(1);
        paged.Items.First().Workload.Should().Be("SharePoint");
    }

    [Fact]
    public async Task GetActivities_FilterByUser_ReturnsFiltered()
    {
        var filter = new ActivityFilterDto
        {
            PageNumber = 1,
            PageSize = 50,
            UserSearch = "alice"
        };

        var result = await _controller.GetActivities(filter);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeAssignableTo<PagedResult<Activity>>().Subject;

        paged.TotalCount.Should().Be(2);
        paged.Items.Should().AllSatisfy(a => a.UserPrincipalName.Should().Contain("alice"));
    }

    [Fact]
    public async Task GetActivities_FilterByDateRange_ReturnsFiltered()
    {
        var filter = new ActivityFilterDto
        {
            PageNumber = 1,
            PageSize = 50,
            StartDate = new DateTime(2026, 4, 2),
            EndDate = new DateTime(2026, 4, 2, 23, 59, 59)
        };

        var result = await _controller.GetActivities(filter);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeAssignableTo<PagedResult<Activity>>().Subject;

        paged.TotalCount.Should().Be(1);
        paged.Items.First().UserId.Should().Be("bob@contoso.com");
    }

    [Fact]
    public async Task GetActivities_Pagination_ReturnsCorrectPage()
    {
        var filter = new ActivityFilterDto { PageNumber = 1, PageSize = 2 };
        var result = await _controller.GetActivities(filter);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeAssignableTo<PagedResult<Activity>>().Subject;

        paged.Items.Should().HaveCount(2);
        paged.TotalCount.Should().Be(3);
        paged.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task GetActivities_FilterByResultStatus_ReturnsFiltered()
    {
        var filter = new ActivityFilterDto
        {
            PageNumber = 1,
            PageSize = 50,
            ResultStatus = "Failed"
        };

        var result = await _controller.GetActivities(filter);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var paged = ok.Value.Should().BeAssignableTo<PagedResult<Activity>>().Subject;

        paged.TotalCount.Should().Be(1);
        paged.Items.First().ResultStatus.Should().Be("Failed");
    }

    [Fact]
    public async Task GetFilterOptions_ReturnsDistinctValues()
    {
        var result = await _controller.GetFilterOptions();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = ok.Value!;

        // Use reflection to check anonymous type
        var workloads = value.GetType().GetProperty("workloads")!.GetValue(value) as List<string>;
        var operations = value.GetType().GetProperty("operations")!.GetValue(value) as List<string>;
        var statuses = value.GetType().GetProperty("statuses")!.GetValue(value) as List<string>;

        workloads.Should().HaveCount(3).And.Contain("SharePoint").And.Contain("OneDrive").And.Contain("Exchange");
        operations.Should().HaveCount(2).And.Contain("FileAccessed").And.Contain("FileSensitivityLabelApplied");
        statuses.Should().HaveCount(2).And.Contain("Success").And.Contain("Failed");
    }

    [Fact]
    public async Task GetStatistics_ReturnsCorrectStats()
    {
        var result = await _controller.GetStatistics(null, null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = ok.Value!;

        var totalActivities = (int)value.GetType().GetProperty("totalActivities")!.GetValue(value)!;
        var uniqueUsers = (int)value.GetType().GetProperty("uniqueUsers")!.GetValue(value)!;

        totalActivities.Should().Be(3);
        uniqueUsers.Should().Be(2);
    }
}
