namespace Ticketing.Api.Checkout;

public sealed class QrPaymentWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<QrPaymentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var confirmation = scope.ServiceProvider.GetRequiredService<PaymentConfirmationService>();
                var confirmed = await confirmation.ConfirmDuePaymentsAsync(stoppingToken);
                if (confirmed > 0)
                    logger.LogInformation("Confirmed {Count} due QR payment(s).", confirmed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "QR payment worker iteration failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
