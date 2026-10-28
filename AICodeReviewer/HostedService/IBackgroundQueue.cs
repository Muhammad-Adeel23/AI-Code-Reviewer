namespace AICodeReviewer.HostedService
{
    
        public interface IBackgroundQueue
        {
            Task QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem);

            Task<Func<CancellationToken, Task>> DequeueAsync(
                CancellationToken cancellationToken);
        }
  }
