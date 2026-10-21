# Guía de Implementación y Sustentación: Bloque 3 (Carrito y Pago)

Este documento detalla la implementación técnica del **Bloque 3 (5 puntos, todo o nada)** de la rúbrica de evaluación URPOnline, junto con las instrucciones para su demostración en el video y la sustentación en clase.

---

## 1. Resumen de lo Implementado

| Indicador | Descripción | Archivos Involucrados |
|---|---|---|
| **3a. Métricas del carrito con AWS SDK** | `CartItemsAdded` (por cada clic) y `ActiveCarts` (conteo de personas únicas en una ventana deslizante de 5 minutos mediante Redis ZSET con hash SHA-256 anónimo). Reporte continuo en CloudWatch cada minuto. | `Services/BusinessMetrics.cs`<br>`Services/ActiveCartsReportingService.cs`<br>`Controllers/CartController.cs`<br>`Program.cs` |
| **3b. Gráfica, alarma y usuarios simulados** | Widget de conversión (`ActiveCarts` vs `OrdersCreated` vs `CartItemsAdded`) en CloudWatch Dashboard. Alarma `AbandonedCartsAlarm` por Metric Math (3 periodos de 5 min = 15 min sin compras). Script k6 con 30 clientes concurrentes. | `infra/urponline-oneclick.yaml`<br>`pruebas/carritos-abandonados.js` |
| **3c. Credenciales de pago seguras** | Recurso `MercadoPagoSecret` en AWS Secrets Manager creado por CloudFormation sin el token privado. Permiso IAM estricto en la EC2 (`secretsmanager:GetSecretValue`). Lectura mediante AWS SDK sin claves en código. | `infra/urponline-oneclick.yaml`<br>`Services/PaymentSecret.cs` |
| **3d. Pago con tarjeta de prueba** | Creación de preferencia en Mercado Pago Checkout Pro (`POST /checkout/preferences`) con ítems y redirección a `sandbox_init_point`. | `Services/MercadoPagoService.cs`<br>`Controllers/CheckoutController.cs` |
| **3e. Confirmación segura al volver** | Verificación servidor a servidor (`GET /v1/payments/{id}`) con Mercado Pago antes de cambiar el estado a `PAGADO`. Idempotencia (recargar la página no duplica eventos ni ventas). Manejo de rechazos (`OTHE`, `FUND`, `SECU`). | `Controllers/CheckoutController.cs`<br>`Views/Checkout/Success.cshtml`<br>`Controllers/PaymentsController.cs` |

---

## 2. Indicador 3a: Métricas del Carrito con AWS SDK y Redis

### Diseño Técnico
- **Privacidad y Seguridad:** El identificador de sesión (`HttpContext.Session.Id`) **nunca** se envía a CloudWatch ni se almacena directamente. Se calcula su hash criptográfico SHA-256 (`BusinessMetrics.HashSessionId`).
- **Ventana Deslizante de 5 Minutos (Redis ZSET):**
  - Clave en Redis: `urponline:active-carts` (Sorted Set).
  - Al agregar un producto: `ZADD urponline:active-carts <timestamp_unix> <hash_sesion>`.
  - Purgado automático: `ZREMRANGEBYSCORE urponline:active-carts 0 (now - 300)`.
  - Conteo de personas únicas: `ZCARD urponline:active-carts`.
  - **Diferencia entre clics y personas:** Si una misma persona agrega 10 productos, su miembro en el ZSET es el mismo y solo se actualiza su timestamp; por tanto, `ActiveCarts` cuenta **1 persona**, no 10.
- **Worker Periódico:**
  - `ActiveCartsReportingService` se ejecuta en segundo plano cada 60 segundos, purga sesiones expiradas (> 300 s) y reporta el valor actual de `ActiveCarts` a CloudWatch para mantener las métricas vivas y reflejar el retorno a 0 tras la inactividad.

---

## 3. Indicador 3b: Gráfica, Alarma y Simulación k6

### Alarma en CloudWatch (`AbandonedCartsAlarm`)
Creada en `infra/urponline-oneclick.yaml` con **Metric Math**:
- Métrica `m1`: `ActiveCarts` (`Maximum`, Periodo: 300 s).
- Métrica `m2`: `OrdersCreated` (`Sum`, Periodo: 300 s).
- Expresión: `IF(FILL(m1,0) >= 1 AND FILL(m2,0) == 0, FILL(m1,0), 0)`.
- Evaluación: 3 periodos consecutivos de 300 s (15 minutos).
- Umbral: $\ge 1$.

### Simulación con k6 (`pruebas/carritos-abandonados.js`)
Para activar la alarma:
```bash
k6 run -e TARGET_URL=https://d2hzwwqzi26wgr.cloudfront.net pruebas/carritos-abandonados.js
```
1. 30 usuarios virtuales (`vus: 30`) agregan productos al carrito.
2. Cada usuario mantiene su sesión y su cookie aislada.
3. El script los mantiene activos durante 16 minutos.
4. En CloudWatch:
   - `ActiveCarts` sube a ~30.
   - `OrdersCreated` se mantiene en 0.
   - Tras 15 minutos, `AbandonedCartsAlarm` pasa a estado **ALARM** (en rojo).
5. **Recuperación:** Entras al navegador, realizas una compra real y `OrdersCreated` sube a 1; la alarma regresa inmediatamente a **OK** (verde).

---

## 4. Indicador 3c: Credenciales Seguras en AWS Secrets Manager

1. El CloudFormation crea el secreto:
   ```yaml
   MercadoPagoSecret:
     Type: AWS::SecretsManager::Secret
     Properties:
       Name: !Sub '${AWS::StackName}/mercadopago-token'
   ```
2. Para configurar tu Access Token de prueba de Mercado Pago en AWS (sin que aparezca en Git):
   ```bash
   aws secretsmanager put-secret-value \
     --secret-id URPOnlineLab/mercadopago-token \
     --secret-string '{"access_token":"TEST-tu-token-aqui"}'
   ```
3. La aplicación lee el secreto al momento de procesar el pago usando el AWS SDK (`AmazonSecretsManagerClient`) aprovechando el rol IAM de la instancia EC2 (`AppRole`).

---

## 5. Indicadores 3d y 3e: Flujo de Pago y Verificación Segura

### Flujo Completo
```
[Cliente]                     [Tienda URPOnline]                     [Mercado Pago]
   │                                   │                                    │
   │── 1. Clic en "Confirmar pedido" ──>│                                    │
   │                                   │── 2. Guarda pedido PENDIENTE_PAGO ─│
   │                                   │── 3. Crea Preferencia (Checkout) ─>│
   │                                   │<─ 4. Retorna init_point (sandbox) ─│
   │<─ 5. Redirección a Mercado Pago ──│                                    │
   │                                                                        │
   │─────────── 6. Paga con Tarjeta de Prueba (APRO / OTHE) ───────────────>│
   │<────────── 7. Redirige de vuelta a /Checkout/Success?payment_id=... ───│
   │                                   │                                    │
   │── 8. Llega a /Checkout/Success ──>│                                    │
   │                                   │── 9. GET /v1/payments/{payment_id}─>│
   │                                   │<─ 10. Retorna estado oficial ──────│
   │                                   │                                    │
   │                                   │── 11. Valida external_reference y  │
   │                                   │       si status == "approved":     │
   │                                   │       actualiza a PAGADO en DB     │
   │<─ 12. Muestra resultado real ─────│                                    │
```

### Tarjetas de Prueba Oficiales (Anexo E)
En el formulario de Mercado Pago:
- **Número de tarjeta:** Consultar la sección oficial de "Tarjetas de prueba" de Mercado Pago para Perú.
- **Titular de la tarjeta:**
  - `APRO`: Pago Aprobado $\rightarrow$ la tienda cambia el estado a **`PAGADO`** y muestra check verde con ID de transacción.
  - `OTHE`: Error general $\rightarrow$ la tienda mantiene **`PENDIENTE_PAGO`** y muestra aviso rojo de rechazo.
  - `FUND`: Fondos insuficientes $\rightarrow$ la tienda mantiene **`PENDIENTE_PAGO`** y muestra rechazo.
  - `SECU`: Código de seguridad inválido $\rightarrow$ la tienda mantiene **`PENDIENTE_PAGO`** y muestra rechazo.
- **Idempotencia:** Si el cliente recarga la página `/Checkout/Success`, la aplicación detecta que la orden ya está `PAGADO` y **no duplica** los eventos ni altera los saldos.

### Límite del Diseño para la Sustentación (Pregunta Típica)
- **Pregunta:** *¿Cuál es el límite del diseño de confirmación al volver?*
- **Respuesta:** Si el cliente paga exitosamente en Mercado Pago pero cierra el navegador o pierde la conexión a Internet antes de ser redirigido de vuelta a la tienda, la petición GET a `/Checkout/Success` nunca llega y el pedido queda temporalmente en `PENDIENTE_PAGO`.
- **Solución en producción:** Implementar **Webhooks asíncronos** (notificaciones IPN servidor a servidor con firma HMAC verificada) para que Mercado Pago avise a la tienda en segundo plano independientemente de lo que haga el navegador del cliente.

