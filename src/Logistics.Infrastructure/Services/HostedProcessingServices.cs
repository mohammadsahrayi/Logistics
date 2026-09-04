using Logistics.Shared.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Logistics.Infrastructure.Services
{
    public sealed class ExpiryWorkerHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ExpiryWorkerHostedService> _logger;

        public ExpiryWorkerHostedService(IServiceScopeFactory scopeFactory, ILogger<ExpiryWorkerHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Expiry worker hosted service started");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var worker = scope.ServiceProvider.GetRequiredService<ExpiryWorker>();
                    await worker.ProcessExpiredHoldsOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while processing expired holds");
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            _logger.LogInformation("Expiry worker hosted service stopped");
        }
    }

    public sealed class OutboxPublisherHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OutboxPublisherHostedService> _logger;

        public OutboxPublisherHostedService(IServiceScopeFactory scopeFactory, ILogger<OutboxPublisherHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Outbox publisher hosted service started");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var publisher = scope.ServiceProvider.GetRequiredService<OutboxPublisher>();
                    await publisher.PublishPendingAsync(ct: stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while publishing outbox messages");
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            _logger.LogInformation("Outbox publisher hosted service stopped");
        }
    }
    namespace Logistics.Infrastructure.Services
    {
        public sealed class LoggingMessageSender : IMessageSender
        {
            private readonly ILogger<LoggingMessageSender> _logger;

            public LoggingMessageSender(
                ILogger<LoggingMessageSender> logger)
            {
                _logger = logger
                    ?? throw new ArgumentNullException(nameof(logger));
            }

            public Task SendAsync(IntegrationEvent @event, CancellationToken ct = default)
            {
                ArgumentNullException.ThrowIfNull(@event);

                ct.ThrowIfCancellationRequested();

                _logger.LogInformation(
                    "Publishing integration event {EventType} with Payload {Payload}",
                    @event.Type,
                    @event.Payload);

                return Task.CompletedTask;
            }
        }
    }
}
