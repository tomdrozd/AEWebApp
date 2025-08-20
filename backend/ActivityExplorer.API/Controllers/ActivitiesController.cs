using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ActivityExplorer.Core.DTOs;
using ActivityExplorer.Core.Models;
using ActivityExplorer.Data;
using ActivityExplorer.Services;
using System.Text;

namespace ActivityExplorer.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ActivitiesController : ControllerBase
    {
        private readonly ActivityContext _context;
        private readonly PurviewService _purviewService;
        private readonly ILogger<ActivitiesController> _logger;
        
        public ActivitiesController(
            ActivityContext context, 
            PurviewService purviewService, 
            ILogger<ActivitiesController> logger)
        {
            _context = context;
            _purviewService = purviewService;
            _logger = logger;
        }
        
        [HttpGet]
        public async Task<IActionResult> GetActivities([FromQuery] ActivityFilterDto filter)
        {
            var query = _context.Activities.AsQueryable();
            
            // Apply filters
            if (filter.StartDate.HasValue)
                query = query.Where(a => a.Timestamp >= filter.StartDate.Value);
            
            if (filter.EndDate.HasValue)
                query = query.Where(a => a.Timestamp <= filter.EndDate.Value);
            
            if (filter.Workloads?.Any() == true)
                query = query.Where(a => filter.Workloads.Contains(a.Workload));
            
            if (filter.Operations?.Any() == true)
                query = query.Where(a => filter.Operations.Contains(a.Operation));
            
            if (!string.IsNullOrEmpty(filter.UserSearch))
                query = query.Where(a => a.UserPrincipalName != null && a.UserPrincipalName.Contains(filter.UserSearch));
            
            if (!string.IsNullOrEmpty(filter.ResultStatus))
                query = query.Where(a => a.ResultStatus == filter.ResultStatus);
            
            // Get total count before pagination
            var totalCount = await query.CountAsync();
            
            // Apply pagination
            var items = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();
            
            var result = new PagedResult<Activity>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
            
            return Ok(result);
        }
        
        [HttpGet("status")]
        public async Task<IActionResult> GetAuthenticationStatus()
        {
            try
            {
                _logger.LogInformation("Checking authentication status");
                var status = await _purviewService.GetAuthenticationStatusAsync();
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get authentication status");
                return StatusCode(500, new { 
                    error = "Failed to get status", 
                    details = ex.Message 
                });
            }
        }
        
        [HttpPost("sync")]
        public async Task<IActionResult> SyncActivities()
        {
            try
            {
                _logger.LogInformation("Manual sync triggered");
                
                // Fetch from Purview
                var activities = await _purviewService.FetchActivitiesAsync();
                
                // Check for duplicates and save to database
                int newActivitiesCount = 0;
                foreach (var activity in activities)
                {
                    // Check if activity already exists (using RecordIdentity as unique key)
                    var exists = await _context.Activities.AnyAsync(a => 
                        a.RecordIdentity == activity.RecordIdentity ||
                        (a.Timestamp == activity.Timestamp && 
                         a.UserId == activity.UserId &&
                         a.Operation == activity.Operation));
                    
                    if (!exists)
                    {
                        _context.Activities.Add(activity);
                        newActivitiesCount++;
                    }
                }
                
                await _context.SaveChangesAsync();
                
                return Ok(new { 
                    message = $"Sync completed. Added {newActivitiesCount} new activities out of {activities.Count} fetched.",
                    totalFetched = activities.Count,
                    newActivities = newActivitiesCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync failed");
                return StatusCode(500, new { 
                    error = "Sync failed", 
                    details = ex.Message,
                    hint = "Make sure you have the Exchange Online Management module installed and proper permissions."
                });
            }
        }
        
        [HttpGet("export/csv")]
        public async Task<IActionResult> ExportCsv([FromQuery] ActivityFilterDto filter)
        {
            var query = _context.Activities.AsQueryable();
            
            // Apply same filters as GetActivities
            if (filter.StartDate.HasValue)
                query = query.Where(a => a.Timestamp >= filter.StartDate.Value);
            
            if (filter.EndDate.HasValue)
                query = query.Where(a => a.Timestamp <= filter.EndDate.Value);
            
            if (!string.IsNullOrEmpty(filter.UserSearch))
                query = query.Where(a => a.UserPrincipalName != null && a.UserPrincipalName.Contains(filter.UserSearch));
            
            var activities = await query
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
            
            var csv = new StringBuilder();
            csv.AppendLine("Timestamp,User,Operation,Workload,Status,ClientIP");
            
            foreach (var activity in activities)
            {
                csv.AppendLine($"{activity.Timestamp:yyyy-MM-dd HH:mm:ss},{activity.UserPrincipalName},{activity.Operation},{activity.Workload},{activity.ResultStatus},{activity.ClientIP}");
            }
            
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"activities_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }
        
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var query = _context.Activities.AsQueryable();
            
            if (startDate.HasValue)
                query = query.Where(a => a.Timestamp >= startDate.Value);
            
            if (endDate.HasValue)
                query = query.Where(a => a.Timestamp <= endDate.Value);
            
            var activities = await query.ToListAsync();
            
            var statistics = new
            {
                totalActivities = activities.Count,
                uniqueUsers = activities.Select(a => a.UserId).Distinct().Count(),
                workloadDistribution = activities
                    .Where(a => !string.IsNullOrEmpty(a.Workload))
                    .GroupBy(a => a.Workload)
                    .Select(g => new { workload = g.Key, count = g.Count() })
                    .OrderByDescending(x => x.count)
                    .Take(10),
                topOperations = activities
                    .Where(a => !string.IsNullOrEmpty(a.Operation))
                    .GroupBy(a => a.Operation)
                    .Select(g => new { operation = g.Key, count = g.Count() })
                    .OrderByDescending(x => x.count)
                    .Take(10),
                activityByDay = activities
                    .GroupBy(a => a.Timestamp.Date)
                    .Select(g => new { date = g.Key, count = g.Count() })
                    .OrderBy(x => x.date)
            };
            
            return Ok(statistics);
        }
        
        [HttpGet("columns")]
        public async Task<IActionResult> AnalyzeColumns([FromQuery] int sampleSize = 100)
        {
            try
            {
                _logger.LogInformation($"Column analysis requested with sample size: {sampleSize}");
                
                // Validate sample size
                if (sampleSize < 1 || sampleSize > 5000)
                {
                    return BadRequest(new { 
                        error = "Invalid sample size", 
                        details = "Sample size must be between 1 and 5000" 
                    });
                }
                
                // Perform column analysis
                var columns = await _purviewService.AnalyzeActivityColumnsAsync(sampleSize);
                
                // Return formatted result
                var result = new
                {
                    columnCount = columns.Count,
                    sampleSize = sampleSize,
                    analysisDate = DateTime.UtcNow,
                    columns = columns.Select(kvp => new
                    {
                        name = kvp.Key,
                        sampleValues = kvp.Value,
                        sampleCount = kvp.Value.Count
                    }).OrderBy(c => c.name),
                    summary = new
                    {
                        totalColumns = columns.Count,
                        currentlyMappedColumns = new[]
                        {
                            "Happened -> Timestamp",
                            "User -> UserId/UserPrincipalName",
                            "Activity -> Operation",
                            "Workload/DataPlatform -> Workload",
                            "ItemName/FilePath -> ObjectId",
                            "ClientIP -> ClientIP",
                            "ResultStatus -> ResultStatus"
                        },
                        unmappedColumns = columns.Keys
                            .Where(k => !new[] { "Happened", "User", "Activity", "Workload", "DataPlatform", 
                                                 "ItemName", "FilePath", "ClientIP", "ResultStatus", "RecordIdentity" }.Contains(k))
                            .OrderBy(k => k).ToList()
                    }
                };
                
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Column analysis failed - operation error");
                return StatusCode(500, new { 
                    error = "Column analysis failed", 
                    details = ex.Message,
                    hint = "Check that Exchange Online connection is working and you have proper permissions."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Column analysis failed - unexpected error");
                return StatusCode(500, new { 
                    error = "Column analysis failed", 
                    details = ex.Message 
                });
            }
        }
    }
}