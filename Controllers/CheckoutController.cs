using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using TiendaOnline.Data;
using TiendaOnline.Models;
using TiendaOnline.Services;

namespace TiendaOnline.Controllers;

public class CheckoutController(
 ShopDbContext db,
 CartService cart,
 BusinessEvents events,
 BusinessMetrics metrics,
 MercadoPagoService mpService,
 ILogger<CheckoutController> log) : Controller {

 [HttpGet]
 public async Task<IActionResult> Index() {
  var vm = await cart.ViewAsync(HttpContext.Session);
  if (vm.Lines.Count == 0) return RedirectToAction("Index", "Cart");
  ViewBag.Cart = vm;
  return View(new CheckoutInput());
 }

 [HttpPost, ValidateAntiForgeryToken]
 public async Task<IActionResult> Index(CheckoutInput input) {
  var vm = await cart.ViewAsync(HttpContext.Session);
  if (vm.Lines.Count == 0) return RedirectToAction("Index", "Cart");
  if (!ModelState.IsValid) { ViewBag.Cart = vm; return View(input); }

  await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
  Order order;
  try {
   // La actualización condicional impide vender más unidades que el stock disponible.
   foreach (var line in vm.Lines) {
    int count = await db.Products.Where(p => p.Id == line.Product.Id && p.Stock >= line.Quantity)
      .ExecuteUpdateAsync(updates => updates.SetProperty(p => p.Stock, p => p.Stock - line.Quantity));
    if (count != 1) {
     await tx.RollbackAsync();
     ModelState.AddModelError("", "No hay stock suficiente de: " + line.Product.Name);
     ViewBag.Cart = await cart.ViewAsync(HttpContext.Session);
     return View(input);
    }
   }

   // Se vuelven a leer los precios de la base de datos, no desde el navegador.
   var ids = vm.Lines.Select(x => x.Product.Id).ToArray();
   var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
   order = new Order {
    CustomerName = input.CustomerName,
    Email = input.Email,
    Address = input.Address,
    Items = vm.Lines.Select(l => new OrderItem {
     ProductId = l.Product.Id,
     ProductName = products[l.Product.Id].Name,
     Quantity = l.Quantity,
     UnitPrice = products[l.Product.Id].Price
    }).ToList()
   };
   order.Total = order.Items.Sum(x => x.Quantity * x.UnitPrice);

   // Aplicar la promoción activa con mayor descuento
   var now = DateTime.UtcNow;
   var promo = await db.Promotions.AsNoTracking()
      .Where(x => x.Enabled && x.StartsUtc <= now && x.EndsUtc > now)
      .OrderByDescending(x => x.DiscountPercent)
      .FirstOrDefaultAsync();
   if (promo != null) order.Total = Math.Round(order.Total * (100m - promo.DiscountPercent) / 100m, 2);

   db.Orders.Add(order);
   await db.SaveChangesAsync();
   await tx.CommitAsync();

   var sessionId = HttpContext.Session.Id;
   cart.Clear(HttpContext.Session);
   await events.RecordAsync("order_created", orderId: order.Id, amount: order.Total);
   await metrics.OrderCreatedAsync(order.Total, sessionId, HttpContext.RequestAborted);
  } catch {
   await tx.RollbackAsync();
   throw;
  }

  // Indicador 3d: Crear preferencia en Mercado Pago y redirigir al link de pago
  var baseUrl = $"{Request.Scheme}://{Request.Host}";
  var pref = await mpService.CreatePreferenceAsync(order, baseUrl, HttpContext.RequestAborted);
  if (pref != null && !string.IsNullOrWhiteSpace(pref.InitPoint)) {
   return Redirect(pref.InitPoint);
  }

  // Fallback si no hay credenciales de Mercado Pago configuradas (entorno de pruebas local)
  return RedirectToAction("Success", new { id = order.Id });
 }

 [HttpGet]
 public async Task<IActionResult> Success(
  int? id,
  int? orderId,
  string? payment_id,
  string? collection_id,
  string? collection_status,
  string? status,
  string? preference_id,
  string? external_reference) {

  int resolvedOrderId = id ?? orderId ?? (int.TryParse(external_reference, out var ext) ? ext : 0);
  if (resolvedOrderId <= 0) return RedirectToAction("Index", "Home");

  var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == resolvedOrderId);
  if (order is null) return NotFound();

  ViewBag.OrderId = order.Id;
  var resolvedPaymentId = payment_id ?? collection_id;

  // Indicador 3e: Confirmación segura consultando el pago a Mercado Pago desde el servidor
  if (!string.IsNullOrWhiteSpace(resolvedPaymentId)) {
   var verification = await mpService.VerifyPaymentAsync(resolvedPaymentId, HttpContext.RequestAborted);
   if (verification != null) {
    ViewBag.PaymentId = verification.PaymentId;
    ViewBag.StatusDetail = verification.StatusDetail;

    // Comprobación de seguridad: verificar que el pago corresponde efectivamente a este pedido
    if (verification.ExternalReference == order.Id.ToString() &&
        verification.Status.Equals("approved", StringComparison.OrdinalIgnoreCase)) {
     // Idempotencia: solo actualizar a PAGADO y registrar evento si no estaba ya marcado
     if (order.Status != "PAGADO") {
      order.Status = "PAGADO";
      await db.SaveChangesAsync();
      await events.RecordAsync("payment_confirmed", orderId: order.Id, amount: order.Total);
      log.LogInformation("Pedido #{OrderId} pagado y verificado con Mercado Pago ({PaymentId})", order.Id, resolvedPaymentId);
     }
     ViewBag.PaymentStatus = "approved";
    } else if (verification.Status.Equals("rejected", StringComparison.OrdinalIgnoreCase)) {
     ViewBag.PaymentStatus = "rejected";
     log.LogWarning("Pago #{PaymentId} para pedido #{OrderId} fue rechazado: {Detail}", resolvedPaymentId, order.Id, verification.StatusDetail);
    } else {
     ViewBag.PaymentStatus = "pending";
    }
   } else {
    // Si la verificación falló (por ejemplo, timeout o credencial no encontrada)
    ViewBag.PaymentStatus = order.Status == "PAGADO" ? "approved" : "pending";
    ViewBag.PaymentId = resolvedPaymentId;
    ViewBag.StatusDetail = "No se pudo verificar con el servidor de pagos";
   }
  } else {
   ViewBag.PaymentStatus = order.Status == "PAGADO" ? "approved" : "pending";
   ViewBag.PaymentId = "N/A";
   ViewBag.StatusDetail = order.Status;
  }

  return View(order);
 }
}
