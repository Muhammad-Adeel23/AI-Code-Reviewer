namespace AICodeReviewer.HostedService
{
    public class BackgroundQueueService : BackgroundService
    {
        private Task _backgroundTask;
        private readonly ILogger _logger;

        public BackgroundQueueService(IBackgroundQueue taskQueue, ILoggerFactory loggerFactory)
        {
            TaskQueue = taskQueue;
            _logger = loggerFactory.CreateLogger<BackgroundQueueService>();
        }
        public IBackgroundQueue TaskQueue { get; }

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (false == stoppingToken.IsCancellationRequested)
            {
                var workItem = await TaskQueue.DequeueAsync(stoppingToken);
                try
                {
                    await workItem(stoppingToken);
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex, $"Error occurred executing {nameof(workItem)}.");
                }
            }
        }
    }
}
