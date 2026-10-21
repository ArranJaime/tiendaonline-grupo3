using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TiendaOnline.Data;
using TiendaOnline.Services;

namespace TiendaOnline.Controllers;

// Simulación de pago local: queda exclusivamente para desarrollo y pruebas en máquina local.
// En AWS el flujo de cobro utiliza Mercado Pago Sandbox con verificación servidor a servidor (Indicador 3e).
[Route("payments")]
public class PaymentsController(ShopDbContext db, BusinessEvents events, IConfiguration config, IWebHostEnvironment env) : Controller {
 [HttpPost("simulate"), ValidateAntiForgeryToken]
 public async Task<IActionResult> Simulate([FromForm] int orderId, [FromForm] string? demoKey) {
  if (!env.IsDevelopment() && !config.GetValue<bool>("Payments:EnableSimulation")) {
   return Forbid("La simulación directa de pagos solo está permitida en entorno local de desarrollo.");
  }

  if (demoKey != config["Payments:DemoKey"])
   return Forbid();

  var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
  if (order is null) return NotFound();

  if (order.Status == "PENDIENTE_PAGO") {
   order.Status = "PAGADO";
   await db.SaveChangesAsync();
   await events.RecordAsync("payment_confirmed", orderId: order.Id, amount: order.Total);
  }

  return Ok(new { order.Id, order.Status, Demo = true });
 }
}
