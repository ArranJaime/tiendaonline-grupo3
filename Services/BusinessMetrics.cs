using System.Security.Cryptography;
using System.Text;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace TiendaOnline.Services;

// Publica métricas de negocio en Amazon CloudWatch con el AWS SDK (namespace "URPOnline").
// Se ven en el dashboard URPOnline-<pila>. En la EC2 las credenciales vienen del rol IAM (sin claves).
// Se activa con Metrics:CloudWatchEnabled=true (la plantilla lo pone en las EC2; en tu PC queda apagado).
public class BusinessMetrics(
 IAmazonCloudWatch cloudWatch,
 IConfiguration config,
 ILogger<BusinessMetrics> log,
 IServiceProvider serviceProvider) {

 private const string ActiveCartsKey = "urponline:active-carts";
 private const int ActiveWindowSeconds = 300; // 5 minutos de ventana activa

 private IConnectionMultiplexer? GetRedis() => serviceProvider.GetService<IConnectionMultiplexer>();

 // Anonimización obligatoria: nunca registrar el identificador de sesión real en métricas ni en Redis.
 public static string HashSessionId(string sessionId) {
  var bytes = Encoding.UTF8.GetBytes(sessionId);
  var hash = SHA256.HashData(bytes);
  return Convert.ToHexString(hash).ToLowerInvariant();
 }

 public async Task OrderCreatedAsync(decimal amount, string? sessionId = null, CancellationToken ct = default) {
  // Al comprar, retiramos la sesión de carritos activos
  if (!string.IsNullOrWhiteSpace(sessionId)) {
   try {
    var redis = GetRedis();
    if (redis != null && redis.IsConnected) {
     var db = redis.GetDatabase();
     await db.SortedSetRemoveAsync(ActiveCartsKey, HashSessionId(sessionId));
    }
   } catch (Exception ex) {
    log.LogDebug(ex, "No se pudo retirar la sesión de carritos activos tras la compra");
   }
  }

  if (!config.GetValue("Metrics:CloudWatchEnabled", false)) return;
  try {
   await cloudWatch.PutMetricDataAsync(new PutMetricDataRequest {
    Namespace = "URPOnline",
    MetricData = new List<MetricDatum> {
     new() { MetricName = "OrdersCreated", Value = 1, Unit = StandardUnit.Count },
     new() { MetricName = "OrderAmount", Value = (double)amount, Unit = StandardUnit.None }
    }
   }, ct);
  } catch (Exception ex) when (ex is not OperationCanceledException) {
   // La venta ya se guardó en PostgreSQL; si CloudWatch falla, solo se registra el aviso.
   log.LogWarning(ex, "No se pudo publicar la métrica del pedido en CloudWatch");
  }
 }

 // Indicador 3a: Registra CartItemsAdded (cada clic) y calcula ActiveCarts (sesiones únicas en los últimos 5 min).
 public async Task CartItemAddedAsync(string sessionId, CancellationToken ct = default) {
  long activeCartsCount = 1;
  try {
   var redis = GetRedis();
   if (redis != null && redis.IsConnected) {
    var db = redis.GetDatabase();
    var hashed = HashSessionId(sessionId);
    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // 1. Anotar sesión en Redis con timestamp actual
    await db.SortedSetAddAsync(ActiveCartsKey, hashed, now);
    // 2. Purgar sesiones con más de 5 minutos de inactividad
    await db.SortedSetRemoveRangeByScoreAsync(ActiveCartsKey, 0, now - ActiveWindowSeconds);
    // 3. Contar personas distintas activas (cardinalidad del Sorted Set)
    activeCartsCount = await db.SortedSetLengthAsync(ActiveCartsKey);
   }
  } catch (Exception ex) {
   log.LogWarning(ex, "Error actualizando carritos activos en Redis");
  }

  if (!config.GetValue("Metrics:CloudWatchEnabled", false)) return;
  try {
   await cloudWatch.PutMetricDataAsync(new PutMetricDataRequest {
    Namespace = "URPOnline",
    MetricData = new List<MetricDatum> {
     new() { MetricName = "CartItemsAdded", Value = 1, Unit = StandardUnit.Count },
     new() { MetricName = "ActiveCarts", Value = activeCartsCount, Unit = StandardUnit.Count }
    }
   }, ct);
  } catch (Exception ex) when (ex is not OperationCanceledException) {
   log.LogWarning(ex, "No se pudo publicar la métrica de carrito en CloudWatch");
  }
 }

 // Recalcula periódicamente y publica ActiveCarts en CloudWatch cada minuto según el diseño del anexo.
 public async Task<long> PruneAndReportActiveCartsAsync(CancellationToken ct = default) {
  long activeCartsCount = 0;
  try {
   var redis = GetRedis();
   if (redis != null && redis.IsConnected) {
    var db = redis.GetDatabase();
    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    await db.SortedSetRemoveRangeByScoreAsync(ActiveCartsKey, 0, now - ActiveWindowSeconds);
    activeCartsCount = await db.SortedSetLengthAsync(ActiveCartsKey);
   }
  } catch (Exception ex) {
   log.LogDebug(ex, "Error al purgar carritos activos");
   return 0;
  }

  if (config.GetValue("Metrics:CloudWatchEnabled", false)) {
   try {
    await cloudWatch.PutMetricDataAsync(new PutMetricDataRequest {
     Namespace = "URPOnline",
     MetricData = new List<MetricDatum> {
      new() { MetricName = "ActiveCarts", Value = activeCartsCount, Unit = StandardUnit.Count }
     }
    }, ct);
   } catch (Exception ex) when (ex is not OperationCanceledException) {
    log.LogDebug(ex, "No se pudo reportar el conteo periódico de ActiveCarts en CloudWatch");
   }
  }

  return activeCartsCount;
 }
}
