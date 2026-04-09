using Microsoft.EntityFrameworkCore;
using ActivityExplorer.Core.Models;
using ActivityExplorer.Data;

namespace ActivityExplorer.Tests;

public static class TestDbHelper
{
    public static ActivityContext CreateContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ActivityContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new ActivityContext(options);
    }

    public static async Task<ActivityContext> CreateSeededContext()
    {
        var context = CreateContext();

        context.Activities.AddRange(
            new Activity
            {
                Id = Guid.NewGuid(),
                Timestamp = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
                UserId = "alice@contoso.com",
                UserPrincipalName = "alice@contoso.com",
                Operation = "FileAccessed",
                Workload = "SharePoint",
                ResultStatus = "Success",
                ClientIP = "10.0.0.1",
                RecordIdentity = "rec-1"
            },
            new Activity
            {
                Id = Guid.NewGuid(),
                Timestamp = new DateTime(2026, 4, 2, 14, 0, 0, DateTimeKind.Utc),
                UserId = "bob@contoso.com",
                UserPrincipalName = "bob@contoso.com",
                Operation = "FileSensitivityLabelApplied",
                Workload = "OneDrive",
                ResultStatus = "Success",
                ClientIP = "10.0.0.2",
                RecordIdentity = "rec-2"
            },
            new Activity
            {
                Id = Guid.NewGuid(),
                Timestamp = new DateTime(2026, 4, 3, 8, 0, 0, DateTimeKind.Utc),
                UserId = "alice@contoso.com",
                UserPrincipalName = "alice@contoso.com",
                Operation = "FileAccessed",
                Workload = "Exchange",
                ResultStatus = "Failed",
                ClientIP = "10.0.0.1",
                RecordIdentity = "rec-3"
            }
        );

        await context.SaveChangesAsync();
        return context;
    }
}
