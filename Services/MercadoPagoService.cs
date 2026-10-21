using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TiendaOnline.Models;

namespace TiendaOnline.Services;

public record MercadoPagoPreferenceResult(string PreferenceId, string InitPoint);
public record MercadoPagoPaymentVerification(
 string PaymentId,
 string Status,
 string StatusDetail,
 string? ExternalReference,
 decimal TransactionAmount
);

public class MercadoPagoService(HttpClient http, IConfiguration config, ILogger<MercadoPagoService> log) {
 private const string BaseApiUrl = "https://api.mercadopago.com";

 public async Task<MercadoPagoPreferenceResult?> CreatePreferenceAsync(Order order, string baseUrl, CancellationToken ct = default) {
  var token = PaymentSecret.GetMercadoPagoToken(config);
  if (string.IsNullOrWhiteSpace(token)) {
   log.LogWarning("No se encontró token de Mercado Pago (ni en Secrets Manager ni en configuración). Saltando creación de preferencia.");
   return null;
  }

  var successUrl = $"{baseUrl}/Checkout/Success?orderId={order.Id}";
  var failureUrl = $"{baseUrl}/Checkout/Success?orderId={order.Id}";
  var pendingUrl = $"{baseUrl}/Checkout/Success?orderId={order.Id}";

  var payload = new {
   items = order.Items.Select(item => new {
    id = item.ProductId.ToString(),
    title = string.IsNullOrWhiteSpace(item.ProductName) ? $"Producto #{item.ProductId}" : item.ProductName,
    quantity = item.Quantity,
    unit_price = (double)item.UnitPrice,
    currency_id = "PEN"
   }).ToList(),
   payer = new {
    name = order.CustomerName,
    email = order.Email
   },
   external_reference = order.Id.ToString(),
   back_urls = new {
    success = successUrl,
    failure = failureUrl,
    pending = pendingUrl
   },
   auto_return = "approved"
  };

  var json = JsonSerializer.Serialize(payload);
  using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseApiUrl}/checkout/preferences");
  request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
  request.Content = new StringContent(json, Encoding.UTF8, "application/json");

  try {
   using var response = await http.SendAsync(request, ct);
   var responseBody = await response.Content.ReadAsStringAsync(ct);

   if (!response.IsSuccessStatusCode) {
    log.LogError("Mercado Pago retornó error {StatusCode}: {Body}", response.StatusCode, responseBody);
    return null;
   }

   using var doc = JsonDocument.Parse(responseBody);
   var root = doc.RootElement;
   var prefId = root.GetProperty("id").GetString() ?? "";
   // En modo de prueba con sandbox, preferir sandbox_init_point si está disponible
   string? initPoint = null;
   if (root.TryGetProperty("sandbox_init_point", out var sbox) && !string.IsNullOrWhiteSpace(sbox.GetString())) {
    initPoint = sbox.GetString();
   }
   if (string.IsNullOrWhiteSpace(initPoint) && root.TryGetProperty("init_point", out var init)) {
    initPoint = init.GetString();
   }

   if (string.IsNullOrWhiteSpace(initPoint)) {
    log.LogError("La respuesta de Mercado Pago no incluye init_point ni sandbox_init_point");
    return null;
   }

   return new MercadoPagoPreferenceResult(prefId, initPoint);
  } catch (Exception ex) when (ex is not OperationCanceledException) {
   log.LogError(ex, "Error comunicándose con Mercado Pago al crear preferencia para el pedido #{OrderId}", order.Id);
   return null;
  }
 }

 public async Task<MercadoPagoPaymentVerification?> VerifyPaymentAsync(string paymentId, CancellationToken ct = default) {
  var token = PaymentSecret.GetMercadoPagoToken(config);
  if (string.IsNullOrWhiteSpace(token)) {
   log.LogWarning("No se encontró token de Mercado Pago para verificar el pago #{PaymentId}", paymentId);
   return null;
  }

  using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseApiUrl}/v1/payments/{paymentId}");
  request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

  try {
   using var response = await http.SendAsync(request, ct);
   var responseBody = await response.Content.ReadAsStringAsync(ct);

   if (!response.IsSuccessStatusCode) {
    log.LogError("Mercado Pago retornó error {StatusCode} al verificar pago {PaymentId}: {Body}", response.StatusCode, paymentId, responseBody);
    return null;
   }

   using var doc = JsonDocument.Parse(responseBody);
   var root = doc.RootElement;

   var idStr = root.GetProperty("id").ToString();
   var status = root.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
   var statusDetail = root.TryGetProperty("status_detail", out var sd) ? sd.GetString() ?? "" : "";
   var externalRef = root.TryGetProperty("external_reference", out var er) && er.ValueKind == JsonValueKind.String ? er.GetString() : null;
   var amount = root.TryGetProperty("transaction_amount", out var am) ? am.GetDecimal() : 0m;

   return new MercadoPagoPaymentVerification(idStr, status, statusDetail, externalRef, amount);
  } catch (Exception ex) when (ex is not OperationCanceledException) {
   log.LogError(ex, "Error verificando el pago #{PaymentId} en Mercado Pago", paymentId);
   return null;
  }
 }
}
