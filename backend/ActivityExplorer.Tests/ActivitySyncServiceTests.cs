using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ActivityExplorer.Core.Models;
using ActivityExplorer.Data;
using ActivityExplorer.Services;

namespace ActivityExplorer.Tests;

public class ActivitySyncServiceTests : IDisposable
{
    private readonly ActivityContext _context;
    private readonly Mock<PurviewService> _purviewServiceMock;
    private readonly ActivitySyncService _syncService;

    public ActivitySyncServiceTests()
    {
        var options = new DbContextOptionsBuilder<ActivityContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ActivityContext(options);

        var loggerPs = new Mock<ILogger<PurviewService>>();
        var runner = new Mock<PowerShellRunner>(
            new Mock<ILogger<PowerShellRunner>>().Object, ".") { CallBase = false };
        var settings = Options.Create(new PurviewSettings
        {
            Organization = "test.onmicrosoft.com",
            AppId = "test",
            TenantId = "test",
            CertificateThumbprint = "test"
        });

        _purviewServiceMock = new Mock<PurviewService>(loggerPs.Object, settings, runner.Object) { CallBase = false };
        var loggerSync = new Mock<ILogger<ActivitySyncService>>();

        _syncService = new ActivitySyncService(_purviewServiceMock.Object, _context, loggerSync.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task SyncAsync_NewActivities_AddsToDatabase()
    {
        var activities = new List<Activity>
        {
            new() { Id = Guid.NewGuid(), RecordIdentity = "r1", Timestamp = DateTime.UtcNow, Operation = "Op1", UserId = "u1" },
            new() { Id = Guid.NewGuid(), RecordIdentity = "r2", Timestamp = DateTime.UtcNow, Operation = "Op2", UserId = "u2" },
        };

        _purviewServiceMock.Setup(s => s.FetchActivitiesAsync())
            .ReturnsAsync(activities);

        var result = await _syncService.SyncAsync();

        result.TotalFetched.Should().Be(2);
        result.NewActivities.Should().Be(2);
        _context.Activities.Count().Should().Be(2);
    }

    [Fact]
    public async Task SyncAsync_DuplicateByRecordIdentity_Skipped()
    {
        // Pre-populate DB with existing activity
        _context.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            RecordIdentity = "existing-record",
            Timestamp = DateTime.UtcNow,
            Operation = "Op1",
            UserId = "u1"
        });
        await _context.SaveChangesAsync();

        var activities = new List<Activity>
        {
            new() { Id = Guid.NewGuid(), RecordIdentity = "existing-record", Timestamp = DateTime.UtcNow, Operation = "Op1", UserId = "u1" },
            new() { Id = Guid.NewGuid(), RecordIdentity = "new-record", Timestamp = DateTime.UtcNow, Operation = "Op2", UserId = "u2" },
        };

        _purviewServiceMock.Setup(s => s.FetchActivitiesAsync())
            .ReturnsAsync(activities);

        var result = await _syncService.SyncAsync();

        result.TotalFetched.Should().Be(2);
        result.NewActivities.Should().Be(1);
        _context.Activities.Count().Should().Be(2); // 1 existing + 1 new
    }

    [Fact]
    public async Task SyncAsync_DuplicateByTimestampUserOperation_Skipped()
    {
        var timestamp = new DateTime(2026, 4, 8, 10, 0, 0, DateTimeKind.Utc);

        _context.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            RecordIdentity = "old-record",
            Timestamp = timestamp,
            Operation = "FileAccessed",
            UserId = "john@contoso.com"
        });
        await _context.SaveChangesAsync();

        // New activity with different RecordIdentity but same timestamp+user+operation
        var activities = new List<Activity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                RecordIdentity = "different-record",
                Timestamp = timestamp,
                Operation = "FileAccessed",
                UserId = "john@contoso.com"
            },
        };

        _purviewServiceMock.Setup(s => s.FetchActivitiesAsync())
            .ReturnsAsync(activities);

        var result = await _syncService.SyncAsync();

        result.NewActivities.Should().Be(0);
        _context.Activities.Count().Should().Be(1);
    }

    [Fact]
    public async Task SyncAsync_EmptyResult_ReturnsZeros()
    {
        _purviewServiceMock.Setup(s => s.FetchActivitiesAsync())
            .ReturnsAsync(new List<Activity>());

        var result = await _syncService.SyncAsync();

        result.TotalFetched.Should().Be(0);
        result.NewActivities.Should().Be(0);
        result.Message.Should().Contain("0");
    }
}
