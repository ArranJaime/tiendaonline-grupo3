namespace TiendaOnline.Services;

// Background worker que cada minuto limpia las sesiones inactivas de los últimos 5 minutos
// y publica el valor actualizado de ActiveCarts en CloudWatch (indicador 3a).
public class ActiveCartsReportingService(IServiceScopeFactory scopeFactory, ILogger<ActiveCartsReportingService> log) : BackgroundService {
 protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
  // Esperar un poco al inicio para que el arranque de la app termine
  await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

  while (!stoppingToken.IsCancellationRequested) {
   try {
    using var scope = scopeFactory.CreateScope();
    var metrics = scope.ServiceProvider.GetRequiredService<BusinessMetrics>();
    var count = await metrics.PruneAndReportActiveCartsAsync(stoppingToken);
    log.LogDebug("ActiveCarts revisado: {Count} carritos activos en los últimos 5 minutos", count);
   } catch (Exception ex) when (ex is not OperationCanceledException) {
    log.LogWarning(ex, "Error en el ciclo de reporte periódico de carritos activos");
   }

   try {
    await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
   } catch (OperationCanceledException) {
    break;
   }
  }
 }
}
