using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ActivityExplorer.Services;
using System.Net;

namespace ActivityExplorer.SyncFunction
{
    public class SyncActivitiesFunction
    {
        private readonly ActivitySyncService _syncService;
        private readonly ILogger<SyncActivitiesFunction> _logger;

        public SyncActivitiesFunction(
            ActivitySyncService syncService,
            ILogger<SyncActivitiesFunction> logger)
        {
            _syncService = syncService;
            _logger = logger;
        }

        /// <summary>
        /// Timer trigger — runs every 6 hours.
        /// </summary>
        [Function("SyncActivitiesTimer")]
        public async Task RunTimer(
            [TimerTrigger("0 0 */6 * * *")] TimerInfo timer)
        {
            _logger.LogInformation("Scheduled sync triggered at {Time}", DateTime.UtcNow);

            try
            {
                var result = await _syncService.SyncAsync();
                _logger.LogInformation("Scheduled sync: {Message}", result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled sync failed");
                throw;
            }
        }

        /// <summary>
        /// HTTP trigger — on-demand sync via POST.
        /// </summary>
        [Function("SyncActivitiesHttp")]
        public async Task<HttpResponseData> RunHttp(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("On-demand sync triggered via HTTP");

            try
            {
                var result = await _syncService.SyncAsync();

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    message = result.Message,
                    totalFetched = result.TotalFetched,
                    newActivities = result.NewActivities
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "On-demand sync failed");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new
                {
                    error = "Sync failed",
                    details = ex.Message
                });
                return response;
            }
        }
    }
}
