using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using MesaiYonetimSistemi.Application.Interfaces;

namespace MesaiYonetimSistemi.Infrastructure.BackgroundServices
{
    public class OvertimeOfferTimeoutBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OvertimeOfferTimeoutBackgroundService> _logger;

        public OvertimeOfferTimeoutBackgroundService(IServiceProvider serviceProvider, ILogger<OvertimeOfferTimeoutBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OvertimeOfferTimeoutBackgroundService başlatıldı.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var overtimeService = scope.ServiceProvider.GetRequiredService<IOvertimeService>();

                    await overtimeService.ProcessExpiredOffersAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Zaman aşımı kontrolü sırasında bir hata oluştu.");
                }

                // Check every 60 seconds
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }

            _logger.LogInformation("OvertimeOfferTimeoutBackgroundService durduruldu.");
        }
    }
}
