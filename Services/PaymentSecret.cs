using System.Text.Json;
using Amazon.Extensions.NETCore.Setup;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace TiendaOnline.Services;

// Lee el token de prueba de Mercado Pago desde AWS Secrets Manager con el AWS SDK (GetSecretValue).
// En la EC2 el token NO se guarda en archivos ni en variables planas: llega Payment:SecretArn
// y el rol IAM de la instancia (AppRole) tiene permiso secretsmanager:GetSecretValue sobre ese recurso.
// En local (sin Payment:SecretArn) se puede usar Payments:MercadoPago:AccessToken desde appsettings o env var.
public static class PaymentSecret {
 public static string? GetMercadoPagoToken(IConfiguration config) {
  var secretArn = config["Payment:SecretArn"] ?? config["Payments:SecretArn"];
  if (string.IsNullOrWhiteSpace(secretArn)) {
   return config["Payments:MercadoPago:AccessToken"];
  }

  try {
   using var client = config.GetAWSOptions().CreateServiceClient<IAmazonSecretsManager>();
   var secret = client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretArn }).GetAwaiter().GetResult();
   if (string.IsNullOrWhiteSpace(secret.SecretString)) return null;

   try {
    using var json = JsonDocument.Parse(secret.SecretString);
    if (json.RootElement.TryGetProperty("access_token", out var tokenProp)) {
     return tokenProp.GetString();
    }
   } catch (JsonException) {
    // Si el secreto se guardó como texto plano en lugar de JSON
    return secret.SecretString.Trim();
   }
   return secret.SecretString.Trim();
  } catch {
   // Fallback en caso de contingencia o prueba local
   return config["Payments:MercadoPago:AccessToken"];
  }
 }
}
