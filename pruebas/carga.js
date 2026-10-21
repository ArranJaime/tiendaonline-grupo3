import http from 'k6/http';
import { sleep, check } from 'k6';

// URL de la tienda en CloudFront (ej: https://d2hzwwqzi26wgr.cloudfront.net)
const URL = __ENV.URL;

export const options = {
  stages: [
    { duration: '1m', target: 50 },     // Sube progresivamente a 50 usuarios
    { duration: '1m', target: 200 },    // Sube a 200 usuarios concurrentes
    { duration: '2m30s', target: 200 }, // Mantiene la carga máxima
    { duration: '30s', target: 0 },     // Rampa de bajada (Total: 5 minutos)
  ],
  thresholds: {
    http_req_failed: ['rate<0.1'], // Alerta si más del 10% de peticiones falla
  },
};

export default function () {
  // 1. Visitar la portada
  const r1 = http.get(`${URL}/`);
  check(r1, { 'portada 200': (r) => r.status === 200 });

  // 2. Búsqueda con vectores en PostgreSQL (simula tráfico real a la base de datos)
  const r2 = http.get(`${URL}/?q=libro de programacion`);
  check(r2, { 'busqueda 200': (r) => r.status === 200 });

  sleep(1);
}
