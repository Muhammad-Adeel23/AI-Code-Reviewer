using Components.Common;
using Components.Configuration;
using Repository.Email;
using Repository.EmailNotifications;
using Repository.Settings;

namespace AICodeReviewer.BackgroundEmailSerivce
{
    public class EmailNotificationBackgroundService : IHostedService, IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailNotificationBackgroundService> _logger;
        private Timer _timerWorkspaceMapping;
        private Timer _timerArchive;
        private Timer _timerUser;
        public EmailNotificationBackgroundService(
           IServiceScopeFactory scopeFactory,
           ILogger<EmailNotificationBackgroundService> logger
         )
        {
            _scopeFactory = scopeFactory;
            _logger = logger;

        }




        public Task StartAsync(CancellationToken cancellationToken)
        {
            {
                _logger.LogInformation("📬 Email Notification Hosted Service started.");
                var hostedTime = AppConfiguration._archiverProcessTime;
                _timerUser = new Timer(async (state) => await ProcessHostedServiceMethod(),
                        null, TimeSpan.Zero, TimeSpan.FromMinutes(hostedTime));

                return Task.CompletedTask;
            }
        }
        private async Task ProcessHostedServiceMethod()
        {
            try
            {
                await ProcessEmailsAsync();
                System.Threading.Thread.Sleep(2000);
                //await ProcessNotificationsAsync();
                System.Threading.Thread.Sleep(2000);


            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "An error occurred while setting up the workspace mapping timer.");
            }
        }

        public void Dispose()
        {
            _timerArchive?.Dispose();
            _timerWorkspaceMapping?.Dispose();
        }
        private async Task ProcessEmailsAsync()
        {

            try
            {
                using var scope = _scopeFactory.CreateScope();

                var emailRepo = scope.ServiceProvider.GetRequiredService<IEmailNotificationRepository>();
                var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSenderRepository>();

                var pendingEmails = await emailRepo.GetPendingEmailsAsync();

                if (pendingEmails == null || !pendingEmails.Any())
                {

                    return;
                }
                var settingRepository =scope.ServiceProvider.GetRequiredService<ISettingRepository>();
                string FromEmail = settingRepository.GetSettingsKeyValue(SystemVariables.SenderEmail);

                foreach (var email in pendingEmails)
                {
                    _logger.LogInformation($" Processing email ID: {email.Id}, To: {email.To}");

                    try
                    {
                        _logger.LogInformation("🔹 Fetching sender email address from settings...");



                        _logger.LogInformation($" Sending email to: {email.To}, Subject: {email.Subject}");

                        await emailSender.SendEmailAsync(
                                FromEmail,
                                email.To,
                                email.Subject,
                                email.Body,
                                email.Cc,
                                email.Bcc
                            );
                        _logger.LogInformation(" Email sent successfully. Updating status in database...");

                        email.StatusId = (long)EmailNotificationEnum.Completed;
                        email.Message = "Email sent successfully";
                        email.UpdatedOn = DateTime.UtcNow;
                        await emailRepo.UpdateEmailStatusAsync(email);

                        _logger.LogInformation($" Email status updated successfully for: {email.To}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($" Error sending email to {email.To}: {ex.Message}");
                        email.StatusId = (long)EmailNotificationEnum.Failed;
                        email.Retries = (email.Retries ?? 0) + 1;
                        email.Message = ex.Message;
                        email.UpdatedOn = DateTime.UtcNow;

                        _logger.LogInformation($" Updating failed email status for retry #{email.Retries}...");

                        await emailRepo.UpdateEmailStatusAsync(email);

                        _logger.LogWarning($" Email to {email.To} failed. Retry Count: {email.Retries}, Status updated.");
                    }
                    _logger.LogInformation(" Waiting briefly before processing next email...");

                    await Task.Delay(1000);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($" Critical error in Email Notification Hosted Service: {ex.Message}");
                _logger.LogError($" StackTrace: {ex.StackTrace}");
            }

        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
