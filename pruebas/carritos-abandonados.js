import http from 'k6/http';
import { check, sleep } from 'k6';
import { parseHTML } from 'k6/html';

// Simulación de Carritos Abandonados (Indicador 3b de la Rúbrica)
// Simula 30 clientes concurrentes que agregan productos a su carrito y lo abandonan sin pagar.
//
// Requisitos de la rúbrica:
// 1. Cada usuario tiene su propia sesión (cookie jar aislada) y token anti-falsificación.
// 2. ActiveCarts sube a ~30 en CloudWatch (namespace URPOnline).
// 3. OrdersCreated se mantiene en 0.
// 4. Tras 15 minutos (3 periodos de 5 min), la alarma AbandonedCartsAlarm pasa a estado ALARM.
// 5. Una compra real devuelve la alarma a OK.
//
// Ejecución contra CloudFront HTTPS (despliegue en AWS):
//   k6 run -e TARGET_URL=https://d2hzwwqzi26wgr.cloudfront.net pruebas/carritos-abandonados.js
//
// Ejecución contra local:
//   k6 run -e TARGET_URL=http://localhost:8080 pruebas/carritos-abandonados.js

const TARGET_URL = __ENV.TARGET_URL || 'https://d2hzwwqzi26wgr.cloudfront.net';

export const options = {
  scenarios: {
    abandoned_carts: {
      executor: 'constant-vus',
      vus: 30,             // 30 compradores independientes
      duration: '16m',     // 16 minutos para cubrir holgadamente los 15 minutos de la alarma
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.05'], // Menos de 5% de errores
  },
};

export default function () {
  // 1. Visitar la tienda para inicializar la sesión y obtener el token CSRF
  const homeRes = http.get(TARGET_URL);
  check(homeRes, {
    'home status is 200': (r) => r.status === 200,
  });

  const doc = parseHTML(homeRes.body);
  const token = doc.find('input[name="__RequestVerificationToken"]').first().attr('value');

  if (!token) {
    // Si no se encuentra el token en el HTML, esperar y reintentar
    sleep(5);
    return;
  }

  // 2. Agregar un producto al carrito (IDs entre 1 y 5)
  const productId = ((__VU % 5) + 1).toString();
  const payload = {
    id: productId,
    quantity: '1',
    __RequestVerificationToken: token,
  };

  const addRes = http.post(`${TARGET_URL}/Cart/Add`, payload, {
    redirects: 1,
  });

  check(addRes, {
    'cart add succeeded (200 or 302)': (r) => r.status === 200 || r.status === 302,
  });

  // 3. Mantener el carrito activo en la ventana deslizante de 5 minutos (300 segundos).
  // Dormir 90 segundos antes del siguiente ciclo para que Redis mantenga su timestamp actualizado.
  // El usuario NO realiza el checkout (simulando abandono).
  sleep(90);
}
