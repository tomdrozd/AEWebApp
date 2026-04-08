using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ActivityExplorer.Core.Models;
using ActivityExplorer.Data;

namespace ActivityExplorer.Services
{
    public class ActivitySyncResult
    {
        public int TotalFetched { get; set; }
        public int NewActivities { get; set; }
        public string Message => $"Sync completed. Added {NewActivities} new activities out of {TotalFetched} fetched.";
    }

    public class ActivitySyncService
    {
        private readonly PurviewService _purviewService;
        private readonly ActivityContext _context;
        private readonly ILogger<ActivitySyncService> _logger;

        public ActivitySyncService(
            PurviewService purviewService,
            ActivityContext context,
            ILogger<ActivitySyncService> logger)
        {
            _purviewService = purviewService;
            _context = context;
            _logger = logger;
        }

        public async Task<ActivitySyncResult> SyncAsync()
        {
            _logger.LogInformation("Starting activity sync...");

            var activities = await _purviewService.FetchActivitiesAsync();

            int newCount = 0;
            foreach (var activity in activities)
            {
                var exists = await _context.Activities.AnyAsync(a =>
                    a.RecordIdentity == activity.RecordIdentity ||
                    (a.Timestamp == activity.Timestamp &&
                     a.UserId == activity.UserId &&
                     a.Operation == activity.Operation));

                if (!exists)
                {
                    _context.Activities.Add(activity);
                    newCount++;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Sync completed: {New} new out of {Total} fetched", newCount, activities.Count);

            return new ActivitySyncResult
            {
                TotalFetched = activities.Count,
                NewActivities = newCount
            };
        }
    }
}
