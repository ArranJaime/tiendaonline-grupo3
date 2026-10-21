# GUÍA MAESTRA DE EVALUACIÓN: CASO URPONLINE EN AWS
**Curso AWS Fundamentos · Laboratorio TiendaOnline (.NET 8) · Evaluación Grupal (20 Puntos)**

Esta es la **guía única e integral** que cubre todos los requerimientos de la rúbrica oficial (Bloques 1, 2, 3 y 4), los pasos técnicos, los comandos exactos para AWS CloudShell / terminal y el guion sugerido para grabar el video grupal.

---

## ÍNDICE
1. [Requisitos Obligatorios para Presentar](#1-requisitos-obligatorios-para-presentar)
2. [Puesta en Marcha (Levantar la Tienda)](#2-puesta-en-marcha-levantar-la-tienda)
3. [Bloque 1: Despliegue, AWS SDK y Fallas (5 Puntos)](#3-bloque-1-despliegue-aws-sdk-y-fallas-5-puntos)
   - 1a. Pipeline y cambio automático
   - 1b. AWS SDK sin claves
   - 1c. Cae un servidor (EC2)
   - 1d. Cae una zona completa (AZ A)
   - 1e. Evidencia y restauración
4. [Bloque 2: PostgreSQL: Carga y Búsqueda por Vectores (5 Puntos)](#4-bloque-2-postgresql-carga-y-búsqueda-por-vectores-5-puntos)
   - 2b. Prueba de carga base con k6 (sin PgBouncer)
   - 2a. Despliegue de PgBouncer (puerto 6432)
   - 2c. Segunda prueba k6, comparación e interpretación
   - 2d. 44 Productos con vectores en RDS
   - 2e. Experimento de búsqueda semántica (0.3 vs 0.6)
5. [Bloque 3: Carrito y Pago (5 Puntos)](#5-bloque-3-carrito-y-pago-5-puntos)
   - 3a. Métricas del carrito en CloudWatch (`CartItemsAdded` y `ActiveCarts`)
   - 3b. Simulación de carritos abandonados (k6) y Alarma
   - 3c. Credenciales seguras en Secrets Manager
   - 3d. Pago con tarjeta de prueba (Mercado Pago Sandbox)
   - 3e. Confirmación segura servidor a servidor e idempotencia
6. [Bloque 4: Sustentación Individual y Reparto del Video](#6-bloque-4-sustentación-individual-y-reparto-del-video)

---

## 1. REQUISITOS OBLIGATORIOS PARA PRESENTAR

| Requisito | Detalle |
|---|---|
| **Grupo** | De 7 a 10 integrantes. Todos participan en el video y sustentan individualmente. |
| **Video Completo** | Entrega como enlace no listado (YouTube o Drive) antes del **viernes 23 de octubre de 2026**. Debe mostrar toda la implementación de principio a fin, con la voz de cada integrante. |
| **Seguridad** | **PROHIBIDO mostrar secretos en pantalla** (contraseñas de BD, Access Keys, tokens de Mercado Pago). Si aparece un secreto, el bloque vale 0. |
| **Puntaje** | Cada bloque es **todo o nada (5 o 0 puntos)**. Total: 20 puntos. |

---

## 2. PUESTA EN MARCHA (LEVANTAR LA TIENDA)

Dado que la pila fue destruida para ahorrar costos, para encenderla nuevamente:

1. Ve a tu repositorio en GitHub: `https://github.com/Jesussv2/tiendaonline-grupo3`.
2. Haz clic en la pestaña **Actions**.
3. En la barra lateral izquierda, selecciona el workflow **Desplegar en AWS** (`deploy-aws.yml`).
4. Haz clic en **Run workflow** $\rightarrow$ botón verde **Run workflow**.
5. Espera unos 15 minutos a que finalice con check verde (`✔`).
6. En la salida del job o en CloudFormation obtendrás las URLs:
   - **Tienda HTTPS (CloudFront):** `https://d2hzwwqzi26wgr.cloudfront.net`
   - **Dashboard CloudWatch:** `https://us-east-2.console.aws.amazon.com/cloudwatch/home?region=us-east-2#dashboards/dashboard/URPOnline-URPOnlineLab`

---

## 3. BLOQUE 1: DESPLIEGUE, AWS SDK Y FALLAS (5 PUNTOS)

### 1a. Pipeline: Primer despliegue y cambio automático
- **Qué demostrar:**
  1. Mostrar en GitHub Actions el historial de ejecuciones exitosas de `deploy-aws.yml` utilizando OIDC (sin Access Keys).
  2. Hacer un cambio visible en vivo (por ejemplo, cambiar el texto del banner en `Views/Home/Index.cshtml`), hacer `git commit` y `git push`.
  3. Mostrar cómo GitHub Actions compila, sube el ZIP a S3 y ejecuta el Rolling Update en Auto Scaling.
  4. Recargar la tienda por HTTPS y mostrar el cambio aplicado sin tiempo de inactividad (*zero-downtime*).

### 1b. AWS SDK configurado y en uso, sin claves
- **Qué demostrar:**
  1. **En la EC2:** Conectar por SSM Session Manager o mostrar el archivo `/etc/urponline.env`:
     - Mostrar que **NO existen** `AWS_ACCESS_KEY_ID` ni `AWS_SECRET_ACCESS_KEY`.
     - Mostrar `Database__SecretArn`: la app pide la contraseña a AWS Secrets Manager en tiempo de ejecución.
  2. **En el código:** Mostrar `Services/BusinessMetrics.cs` (llama a `cloudWatch.PutMetricDataAsync`) y `Worker/Program.cs` (usa `AmazonS3Client`).
  3. **En IAM:** Mostrar el rol `URPOnlineLab-AppRole` con permisos temporales asumidos por la instancia EC2 (`Instance Profile`).
  4. **En CloudWatch:** Mostrar las métricas `OrdersCreated` y `OrderAmount` recibiendo datos bajo el namespace `URPOnline`.

### 1c. Cae un servidor (EC2)
*Tener abiertos la tienda en una pestaña (con productos en el carrito) y el Dashboard de CloudWatch en otra.*

**En AWS CloudShell (us-east-2):**
```bash
STACK=URPOnlineLab
VPC=$(aws cloudformation describe-stacks --stack-name $STACK --query "Stacks[0].Outputs[?OutputKey=='VpcId'].OutputValue" --output text)

# Listar instancias activas y sus zonas
aws ec2 describe-instances --filters Name=vpc-id,Values=$VPC Name=instance-state-name,Values=running \
  --query "Reservations[].Instances[].[InstanceId,Placement.AvailabilityZone]" --output table

# Terminar una instancia a propósito
aws ec2 terminate-instances --instance-ids <ID_DE_UNA_INSTANCIA>
```
- **Resultado a mostrar en el video:**
  - Recargar la tienda inmediatamente: sigue respondiendo porque el ALB redirige a la otra EC2.
  - El carrito de compras **sigue intacto**: no se perdió porque la sesión se guarda en ElastiCache Redis, no en la memoria de la EC2.
  - En CloudWatch: la métrica `HealthyHostCount` baja temporalmente de 2 a 1, y Auto Scaling lanza una nueva EC2 para volver a 2.

### 1d. Cae una zona completa (Zona A)

**En AWS CloudShell (us-east-2):**
```bash
# Identificar la subred de la Zona A y su NACL original
SUBNET_A=$(aws ec2 describe-subnets --filters Name=vpc-id,Values=$VPC Name=cidr-block,Values=10.0.11.0/24 --query "Subnets[0].SubnetId" --output text)
ORIGINAL=$(aws ec2 describe-network-acls --filters Name=association.subnet-id,Values=$SUBNET_A --query "NetworkAcls[0].NetworkAclId" --output text)
ASOC=$(aws ec2 describe-network-acls --filters Name=association.subnet-id,Values=$SUBNET_A \
  --query "NetworkAcls[0].Associations[?SubnetId=='$SUBNET_A'].NetworkAclAssociationId" --output text)

# Crear una NACL vacía (bloquea todo el tráfico) y asociarla a la Subred A
CAIDA=$(aws ec2 create-network-acl --vpc-id $VPC --query NetworkAcl.NetworkAclId --output text)
NUEVA=$(aws ec2 replace-network-acl-association --association-id $ASOC --network-acl-id $CAIDA --query NewAssociationId --output text)

# Provocar failover manual en Redis
RG=$(aws elasticache describe-replication-groups --query "ReplicationGroups[?contains(ReplicationGroupId,'urp')].ReplicationGroupId | [0]" --output text)
aws elasticache test-failover --replication-group-id $RG --node-group-id 0001
```
- **Resultado a mostrar en el video:**
  - Navegar y realizar una compra en la tienda: la venta se completa exitosamente desde la Zona B.
  - Redis mantiene los carritos gracias al failover automático hacia el nodo réplica.

### 1e. Evidencia y restauración obligatoria
- **Evidencia en CloudWatch Logs Insights:**
  Ejecutar en el grupo `/urponline/URPOnlineLab/nginx-access`:
  ```sql
  stats count(*) as peticiones by bin(1m), @logStream | sort bin(1m) desc
  ```
  *Muestra cómo una EC2 dejó de recibir peticiones y la otra asumió el 100% del tráfico.*
- **VPC Reachability Analyzer:**
  - Crear ruta: Origen = Interfaz del ALB en Subred Pública A $\rightarrow$ Destino = EC2 de Zona A (Puerto 80). Resultado: **Unreachable** (bloqueado por la NACL).
  - Repetir hacia la EC2 de Zona B: **Reachable**.
- **Restaurar la Zona A (Obligatorio):**
  ```bash
  aws ec2 replace-network-acl-association --association-id $NUEVA --network-acl-id $ORIGINAL
  aws ec2 delete-network-acl --network-acl-id $CAIDA
  ```

---

## 4. BLOQUE 2: POSTGRESQL: CARGA Y BÚSQUEDA POR VECTORES (5 PUNTOS)

> ⚠️ **Regla de oro del Anexo C:** Primero se ejecuta la prueba con k6 **SIN** PgBouncer (línea base). Luego se activa PgBouncer y se repite la misma prueba para comparar.

### 2b. Prueba de carga base con k6 (Sin PgBouncer)
En **AWS CloudShell**:
```bash
# Instalar k6
curl -sL https://github.com/grafana/k6/releases/download/v0.54.0/k6-v0.54.0-linux-amd64.tar.gz | tar xz
mv k6-v0.54.0-linux-amd64/k6 ~/k6

# Descargar el script de carga desde el repositorio
curl -sLO https://raw.githubusercontent.com/Jesussv2/tiendaonline-grupo3/main/pruebas/carga.js

# Ejecutar la prueba base (5 minutos, hasta 200 usuarios)
date # Anotar hora de inicio
~/k6 run -e URL=https://d2hzwwqzi26wgr.cloudfront.net carga.js
date # Anotar hora de fin
```
- **Anotar métricas de k6:** `http_req_duration p(95)`, `% de errores`, `peticiones/segundo`.
- **En CloudWatch:** Mostrar la gráfica `RDS PostgreSQL: conexiones y CPU %`: las conexiones escalan hasta saturar el límite (~80 conexiones) y aparecen errores 5xx en el ALB.

### 2a. Despliegue de PgBouncer (Puerto 6432)
1. Explicar la arquitectura en el código:
   - [`Program.cs`](file:///c:/Users/Jesus/tiendaonline-grupo3/Program.cs): La app conecta a `ConnectionStrings:Postgres` (puerto 6432 a través de PgBouncer en modo `transaction`).
   - Las migraciones conectan a `ConnectionStrings:PostgresDirect` (puerto 5432 directo a RDS) porque usan `pg_advisory_lock(727001)`, incompatible con el pool de transacciones de PgBouncer.
2. Mostrar el servicio PgBouncer instalado en la EC2 escuchando en `127.0.0.1:6432`.

### 2c. Segunda prueba k6 e interpretación de resultados
1. Ejecutar nuevamente `~/k6 run -e URL=https://d2hzwwqzi26wgr.cloudfront.net carga.js`.
2. **Tabla Comparativa de Resultados:**

| Métrica | Sin PgBouncer (Línea Base) | Con PgBouncer (Optimizada) |
|---|---|---|
| **Usuarios Concurrentes** | 200 | 200 |
| **Conexiones Máximas RDS** | ~80 (Saturación al límite) | 15–20 (Fijo en el tamaño del pool) |
| **Errores HTTP 5xx (ALB)** | Errores por rechazo de conexiones | 0 errores (los clientes hacen fila) |
| **Latencia p95 (k6)** | Picos altos por timeout | Estable |
| **CPU de la Base de Datos** | Picos por sobrecarga de procesos | CPU controlada y estable |

- **Interpretación para el video:**
  - *Cuello de botella:* No era la CPU de las EC2 (estaba al <20%), sino el límite de conexiones concurrentes en PostgreSQL `db.t3.micro`.
  - *Qué hace PgBouncer:* Actúa como una ventanilla de banco; recibe a 200 clientes concurrentes en el puerto 6432 y los atiende ordenadamente con solo 20 conexiones reales hacia RDS (puerto 5432).

### 2d. 44 Productos y vectores verificados en RDS
1. Mostrar la migración EF Core [`Data/Migrations/20261021043916_MasProductos.cs`](file:///c:/Users/Jesus/tiendaonline-grupo3/Data/Migrations/20261021043916_MasProductos.cs) que inserta 35 productos adicionales (44 en total).
2. Conectar a PostgreSQL en RDS desde una EC2 o mostrar la consulta SQL:
   ```sql
   -- Verificar los 44 productos
   SELECT count(*) FROM "Products"; -- Retorna 44

   -- Verificar que todos tienen su vector de 384 dimensiones
   SELECT count(*) FROM "ProductEmbeddings"; -- Retorna 44

   -- Comprobar la búsqueda semántica por similitud coseno
   SELECT p."Name", pe."Embedding" <=> (SELECT "Embedding" FROM "ProductEmbeddings" WHERE "ProductId" = 1) AS distancia
   FROM "Products" p
   JOIN "ProductEmbeddings" pe ON p."Id" = pe."ProductId"
   ORDER BY distancia ASC LIMIT 5;
   ```

### 2e. Experimento de búsqueda semántica (0.3 vs 0.6)
Explicar el experimento realizado en `appsettings.json` (`Search:SemanticSimilarityThreshold`):

| Consulta Probada | Con Umbral 0.3 (Permisivo) | Con Umbral 0.6 (Óptimo) |
|---|---|---|
| *"algo para la lluvia"* | Traía paraguas, pero también productos no relacionados (Falsos positivos) | Solo devuelve Paraguas y Casacas impermeables |
| *"programar en python"* | Traía libros, cables y teclados | Solo Libros de programación y cursos de Python |
| *"escuchar musica"* | Traía audífonos, parlantes y fundas de celular | Trae únicamente Audífonos bluetooth y Parlantes |

- **Conclusión técnica:** Un umbral de `0.3` prioriza la *exhaustividad (recall)* pero introduce ruido; un umbral de `0.6` maximiza la *precisión* de negocio para Cyber Monday.

---

## 5. BLOQUE 3: CARRITO Y PAGO (5 PUNTOS)

### 3a. Métricas del carrito con AWS SDK
1. Mostrar en [`Services/BusinessMetrics.cs`](file:///c:/Users/Jesus/tiendaonline-grupo3/Services/BusinessMetrics.cs):
   - **Hash SHA-256:** `HashSessionId(sessionId)` anonimiza el identificador del usuario.
   - **Redis ZSET:** `urponline:active-carts` almacena la sesión con score del timestamp Unix.
   - **Ventana de 5 minutos:** `ZREMRANGEBYSCORE` purga entradas de más de 300 segundos.
   - **Diferencia clics vs personas:** `CartItemsAdded` sube con cada clic; `ActiveCarts` cuenta el número de personas distintas (`ZCARD`).
2. Demostración en vivo:
   - Abrir 2 navegadores distintos (ej. Chrome normal e Incógnito) y agregar productos.
   - Ir a CloudWatch $\rightarrow$ **Metrics** $\rightarrow$ **URPOnline**:
     - `CartItemsAdded` = número total de productos añadidos.
     - `ActiveCarts` = 2 (dos personas).

### 3b. Simulación de carritos abandonados (k6) y Alarma
1. Mostrar en [`infra/urponline-oneclick.yaml`](file:///c:/Users/Jesus/tiendaonline-grupo3/infra/urponline-oneclick.yaml):
   - Alarma `AbandonedCartsAlarm` con Metric Math: `IF(FILL(m1,0) >= 1 AND FILL(m2,0) == 0, FILL(m1,0), 0)`.
   - Widget de conversión en el Dashboard (`ActiveCarts` vs `OrdersCreated`).
2. En CloudShell o máquina local, ejecutar:
   ```bash
   k6 run -e TARGET_URL=https://d2hzwwqzi26wgr.cloudfront.net pruebas/carritos-abandonados.js
   ```
   - 30 usuarios virtuales añaden productos al carrito sin realizar checkout.
   - `ActiveCarts` sube a ~30 y `OrdersCreated` se queda en 0.
   - Tras 15 minutos (3 periodos de 5 min), `AbandonedCartsAlarm` pasa a **ALARM** (en rojo en el dashboard).
   - **Recuperación:** Entras al navegador, compras un producto y la alarma regresa a **OK** (en verde).

### 3c. Credenciales de pago seguras en Secrets Manager
1. Mostrar en CloudFormation que el secreto `URPOnlineLab/mercadopago-token` fue creado por la plantilla sin el valor privado.
2. Cargar el Access Token de prueba de Mercado Pago mediante AWS CLI:
   ```bash
   aws secretsmanager put-secret-value \
     --secret-id URPOnlineLab/mercadopago-token \
     --secret-string '{"access_token":"TEST-tu-token-aqui"}'
   ```
3. Mostrar en [`Services/PaymentSecret.cs`](file:///c:/Users/Jesus/tiendaonline-grupo3/Services/PaymentSecret.cs) que la aplicación lee el token desde Secrets Manager en tiempo de ejecución con el SDK de AWS, sin que el token exista en el código de GitHub ni en la EC2.

### 3d. Pago con tarjeta de prueba en Mercado Pago Sandbox
1. En la tienda, añadir productos e ir a **Checkout**.
2. Al presionar **Confirmar Pedido**:
   - [`CheckoutController.cs`](file:///c:/Users/Jesus/tiendaonline-grupo3/Controllers/CheckoutController.cs) guarda la orden en PostgreSQL con estado `PENDIENTE_PAGO`.
   - Llama a `MercadoPagoService.CreatePreferenceAsync` y redirige a la pasarela oficial de Mercado Pago.
3. Iniciar sesión con el **Comprador de prueba**.
4. Pagar usando las **Tarjetas de prueba de Perú (Anexo E)**:
   - **Prueba 1 (Aprobado):** Nombre del titular = `APRO`. Redirige a la tienda $\rightarrow$ la tienda consulta a Mercado Pago desde el servidor $\rightarrow$ el pedido pasa a **`PAGADO`** y muestra check verde con el ID de transacción.
   - **Prueba 2 (Rechazado):** Nombre del titular = `OTHE` o `FUND`. Redirige a la tienda $\rightarrow$ el pedido se mantiene en **`PENDIENTE_PAGO`** y muestra aviso rojo de rechazo.

### 3e. Confirmación segura al volver e idempotencia
1. **Regla de oro:** La tienda **nunca** marca como pagado solo porque el cliente llegó a `/Checkout/Success`.
2. Mostrar en el código que el servidor ejecuta:
   `GET https://api.mercadopago.com/v1/payments/{payment_id}` con el Bearer token de Secrets Manager, y valida que `external_reference == order.Id`.
3. **Prueba de Idempotencia:** Recargar la página `/Checkout/Success` varias veces con F5:
   - El estado no cambia.
   - No se duplican registros en PostgreSQL ni en las métricas de CloudWatch.
4. **Límite del diseño (Pregunta de examen):** Si el cliente paga en Mercado Pago y cierra la pestaña antes de volver a la tienda, la orden queda en `PENDIENTE_PAGO`. *Solución para producción:* Webhooks asíncronos (IPN) con firma HMAC verificada.

---

## 6. BLOQUE 4: SUSTENTACIÓN INDIVIDUAL Y REPARTO DEL VIDEO

### Reparto Sugerido para el Video (Grupos de 7 a 10 integrantes)

| Integrante | Rol en el Video | Tema que Explica |
|---|---|---|
| **Integrante 1** | Arquitectura y Pipeline (1a) | Repositorio GitHub, OIDC sin claves, workflow `deploy-aws.yml` y despliegue continuo con rolling update. |
| **Integrante 2** | Seguridad y AWS SDK (1b, 3c) | Rol IAM de la EC2 (`Instance Profile`), lectura de credenciales de RDS y Mercado Pago con Secrets Manager sin claves en código. |
| **Integrante 3** | Caos y Alta Disponibilidad (1c) | Demostración de caída de servidor (terminar EC2), persistencia de sesiones de carrito en Redis y auto-recuperación con Auto Scaling. |
| **Integrante 4** | Tolerancia a Fallas en Zona (1d, 1e) | Aislamiento de Zona A con NACL, conmutación a Zona B, failover de Redis, Logs Insights y Reachability Analyzer. |
| **Integrante 5** | Carga y PgBouncer (2a, 2b, 2c) | Prueba de carga base k6 (saturación de conexiones a RDS), instalación de PgBouncer en puerto 6432 e interpretación de gráficas. |
| **Integrante 6** | Base de Datos y Vectores (2d, 2e) | Migración de 44 productos, extensión pgvector, embeddings ONNX MiniLM y experimento de umbrales 0.3 vs 0.6. |
| **Integrante 7** | Métricas del Carrito (3a, 3b) | Hash SHA-256 de sesión, conteo de personas en ventana de 5 min con Redis ZSET, simulación k6 de 30 carritos y alarma de abandono. |
| **Integrante 8** | Pasarela y Pagos (3d, 3e) | Checkout Pro en Mercado Pago Sandbox, tarjetas de prueba `APRO` vs `OTHE`, confirmación servidor a servidor e idempotencia. |
| *(Si son 9)* | Worker Nocturno S3 | Explicar el worker de catálogo (`Worker/Program.cs`), importación CSV desde S3 con IAM Role y temporizador systemd. |
| *(Si son 10)* | Conclusiones y Dashboard | Recorrido final por el CloudWatch Dashboard integral con todas las métricas operativas y comerciales en verde. |

---

### Resumen de Preguntas Clave para la Sustentación Individual

1. **¿Por qué las migraciones no deben pasar por PgBouncer en modo transaction?**
   * *Respuesta:* EF Core utiliza `pg_advisory_lock(727001)` para evitar que dos instancias migren a la vez. PgBouncer en modo `transaction` cambia de conexión física entre sentencias, rompiendo los bloqueos a nivel de sesión. Por eso las migraciones conectan directo al puerto 5432.
2. **¿Por qué se usa un hash SHA-256 para `ActiveCarts` en vez del ID de sesión?**
   * *Respuesta:* Por privacidad y cumplimiento de seguridad (GDPR/normativas): las cookies o identificadores de sesión nunca deben registrarse en texto plano en herramientas de telemetría ni bases de datos intermedias.
3. **¿Cómo se asegura que un usuario no falsifique el estado de su pago al volver a la tienda?**
   * *Respuesta:* La tienda no confía en los parámetros de la URL; el backend toma el `payment_id` y consulta directamente a la API REST de Mercado Pago con el token seguro de Secrets Manager, validando además que el `external_reference` coincida con el número de pedido.
4. **¿Por qué la tienda sigue funcionando si se cae una zona de disponibilidad completa?**
   * *Respuesta:* Porque la arquitectura está desacoplada en dos zonas (us-east-2a y us-east-2b): ALB multi-AZ reparte a instancias EC2 en ambas subredes, RDS PostgreSQL opera en Multi-AZ con réplica síncrona en standby, y ElastiCache Redis cuenta con Automatic Failover.
