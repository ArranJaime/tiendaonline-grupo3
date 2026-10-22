# GUÍA DE INSTALACIÓN COMPLETA PARA COMPAÑEROS DE EQUIPO
**Proyecto:** URPOnline · TiendaOnline (.NET 8 + AWS)  
**Repositorio del Grupo:** `https://github.com/Jesussv2/tiendaonline-grupo3`

Esta guía contiene la **lista definitiva y verificada** de todo lo que cada integrante del grupo debe tener instalado para trabajar con el código, levantar la tienda en local, probar el catálogo, correr las pruebas de carga (k6) y conectarse a AWS.

---

## TABLA MAESTRA DE PROGRAMAS Y HERRAMIENTAS

| # | Herramienta | Para qué sirve | Comando de Verificación |
|:---:|---|---|---|
| **1** | **Git** | Clonar el repositorio y sincronizar cambios | `git --version` |
| **2** | **.NET 8 SDK** | Compilar y ejecutar la app web y el Worker | `dotnet --version` (debe ser `8.x`) |
| **3** | **dotnet-ef (EF Core CLI)** | Administrar migraciones de base de datos | `dotnet ef --version` |
| **4** | **Docker Desktop** | Levantar PostgreSQL 16 (`pgvector`) y Redis 7 | `docker --version` |
| **5** | **AWS CLI v2** | Gestionar CloudFormation, S3 y Secrets Manager | `aws --version` |
| **6** | **Session Manager Plugin** | Conectarse por terminal a las EC2 privadas sin SSH | `session-manager-plugin` |
| **7** | **k6 (Grafana)** | Ejecutar pruebas de carga y carritos abandonados | `k6 version` |
| **8** | **Python 3 + cfn-lint** | Validar la plantilla de CloudFormation antes de desplegar | `cfn-lint --version` |
| **9** | **Visual Studio Code** | Editor de código recomendado con extensiones C# y AWS | `code --version` |
| **10** | **Modelo ONNX MiniLM** | Inteligencia artificial para búsqueda semántica vectorial | Existe `ml-models/.../model.onnx` |

---

## PASO 0: Acceso al Repositorio en GitHub

1. **Acepta la invitación:** Jesús debe haberte agregado como colaborador en GitHub (`Settings` $\rightarrow$ `Collaborators`). Revisa tu correo o entra a [https://github.com/Jesussv2/tiendaonline-grupo3](https://github.com/Jesussv2/tiendaonline-grupo3) y acepta la invitación.
2. Abre **PowerShell** en tu computadora y clona el proyecto:
   ```powershell
   cd C:\MisProyectos  # o la carpeta donde guardes tus trabajos
   git clone https://github.com/Jesussv2/tiendaonline-grupo3.git
   cd tiendaonline-grupo3
   ```

---

## PASO 1: Instalación de Todo el Software (Windows)

Abre **PowerShell como Administrador** (clic derecho en el botón de Inicio de Windows $\rightarrow$ *Terminal / PowerShell como Administrador*) y ejecuta este comando único para instalar **todos los programas oficiales**:

```powershell
winget install Git.Git Microsoft.DotNet.SDK.8 Docker.DockerDesktop Amazon.AWSCLI Amazon.SessionManagerPlugin GrafanaLabs.k6 Python.Python.3.12 Microsoft.VisualStudioCode
```

A continuación, instala el validador de CloudFormation (`cfn-lint`):
```powershell
pip install cfn-lint
```

> ⚠️ **REINICIO NECESARIO:**
> 1. Una vez terminadas las descargas, **cierra y vuelve a abrir PowerShell** para que se actualicen las rutas del sistema (PATH).
> 2. Si Docker Desktop solicita reiniciar la PC o habilitar WSL 2, **reinicia tu computadora**.

### Verificación rápida:
Ejecuta estos comandos para asegurarte de que todo responde:
```powershell
git --version
dotnet --version
docker --version
aws --version
session-manager-plugin
k6 version
cfn-lint --version
```
*(Todos deben responder mostrando su número de versión sin errores).*

---

## PASO 2: Iniciar Docker (PostgreSQL con pgvector + Redis)

La tienda requiere dos servicios de datos que corren en Docker:
- **PostgreSQL 16 con extensión `pgvector`:** Almacena los 44 productos, pedidos y vectores de búsqueda.
- **Redis 7:** Guarda las sesiones y calcula los carritos activos en tiempo real.

1. **Abre Docker Desktop** desde el menú Inicio de Windows y espera a que el icono de la ballena esté en **verde** (*Engine running*).
2. En tu terminal de PowerShell, ejecuta:

```powershell
# 1. Base de datos PostgreSQL con soporte vectorial (puerto 5432)
docker run -d --name urp-postgres -e POSTGRES_USER=urponline -e POSTGRES_PASSWORD=localonly -e POSTGRES_DB=urponline -p 5432:5432 pgvector/pgvector:pg16

# 2. Redis para sesiones y carritos (puerto 6379)
docker run -d --name urp-redis -p 6379:6379 redis:7
```

3. Comprueba que ambos estén corriendo:
```powershell
docker ps
```
*(Debes ver `urp-postgres` y `urp-redis` en estado `Up`)*.

> 💡 **Para los días siguientes:** Cuando vuelvas a encender tu PC, solo abres Docker Desktop y ejecutas:
> ```powershell
> docker start urp-postgres urp-redis
> ```

---

## PASO 3: Descargar el Modelo de Búsqueda Semántica

La búsqueda de la tienda utiliza inteligencia artificial local (modelo ONNX de HuggingFace) para encontrar productos por significado conceptual (búsqueda por vectores con similitud coseno). 

El modelo pesa ~120 MB y está ignorado en Git para no saturar el repositorio. Para descargarlo automáticamente con verificación de hash SHA-256:

Desde la carpeta raíz `tiendaonline-grupo3`, ejecuta:
```powershell
powershell -ExecutionPolicy Bypass -File scripts/descargar-modelo.ps1
```
Debe finalizar diciendo:  
`OK: sentencepiece.bpe.model`  
`OK: model.onnx`  
`Modelo listo en ...\ml-models\paraphrase-multilingual-MiniLM-L12-v2`

---

## PASO 4: Compilar y Ejecutar la Tienda por Primera Vez

La primera vez que ejecutas el proyecto, debes restaurar las herramientas locales (`dotnet-ef`), aplicar las migraciones a PostgreSQL y cargar los **44 productos del catálogo** con sus vectores:

```powershell
# 1. Restaurar paquetes NuGet y herramientas EF Core
dotnet restore
dotnet tool restore
dotnet build

# 2. Variables para inicializar la base de datos (solo la primera vez)
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:InitializeDatabase = "true"

# 3. Iniciar la tienda en el puerto 8080
dotnet run --urls http://localhost:8080
```

### Probar en tu navegador:
1. Abre tu navegador y entra a:  
   👉 **`http://localhost:8080`**  
   *(Verás el catálogo completo con los 44 productos divididos por categorías).*
2. Prueba el endpoint de salud del balanceador:  
   👉 **`http://localhost:8080/health`** $\rightarrow$ Debe responder `{"status":"ok"}`.
3. Prueba la búsqueda semántica: escribe en el buscador *"audifonos para viajar"* o *"para la lluvia"* y verifica cómo el modelo vectorial calcula el porcentaje de similitud coseno.

> 💡 **Para las siguientes veces:** Ya no necesitas `$env:InitializeDatabase = "true"`. Simplemente ejecutas:
> ```powershell
> dotnet run --urls http://localhost:8080
> ```
> Para detener la tienda en la terminal, presiona `Ctrl + C`.

---

## PASO 5: Probar el Worker Nocturno de Catálogo

El proyecto incluye un Worker en consola que simula la importación nocturna de productos desde CSV:

```powershell
dotnet run --project Worker -- --source local --file examples/products.csv
```
Verás en consola cómo valida el formato, actualiza el stock y los precios en PostgreSQL.

---

## PASO 6: Probar las Pruebas de Rendimiento (k6)

Gracias a que instalaste `k6`, puedes ejecutar las pruebas directamente desde tu computadora contra la tienda desplegada en AWS:

```powershell
# Prueba de carga de 5 minutos (hasta 200 usuarios concurrentes):
k6 run -e URL=https://d2hzwwqzi26wgr.cloudfront.net pruebas/carga.js

# Prueba de 30 carritos abandonados (para activar la alarma):
k6 run -e TARGET_URL=https://d2hzwwqzi26wgr.cloudfront.net pruebas/carritos-abandonados.js
```

---

## PASO 7: Extensiones de VS Code Oficiales

El repositorio ya tiene configuradas las extensiones recomendadas en `.vscode/extensions.json`.

### Opción A (Automática desde la ventana de VS Code):
Abre la carpeta del proyecto en VS Code:
```powershell
code .
```
Aparecerá una notificación en la esquina inferior derecha: **"Este repositorio recomienda extensiones para este proyecto"**. Haz clic en **Instalar todas** (*Install All*).

### Opción B (Comando directo en PowerShell):
Si prefieres instalarlas todas de una sola vez desde PowerShell, copia y pega este comando:

```powershell
code --install-extension ms-dotnettools.csdevkit `
     --install-extension ms-dotnettools.csharp `
     --install-extension amazonwebservices.aws-toolkit-vscode `
     --install-extension redhat.vscode-yaml `
     --install-extension kddejong.vscode-cfn-lint `
     --install-extension mtxr.sqltools `
     --install-extension mtxr.sqltools-driver-pg `
     --install-extension github.vscode-github-actions
```

### ¿Para qué sirve cada extensión en el proyecto?

| Extensión | Nombre | Utilidad en el Proyecto |
|---|---|---|
| **C# Dev Kit** + **C#** | `ms-dotnettools.csdevkit` | Autocompletado IntelliSense, depuración F5, compilación y solución de errores en C# .NET 8. |
| **AWS Toolkit** | `amazonwebservices.aws-toolkit-vscode` | Explorar CloudWatch Logs, buckets S3, CloudFormation y recursos de AWS directamente en el panel lateral. |
| **YAML** (Red Hat) | `redhat.vscode-yaml` | Resaltado y validación de sintaxis de CloudFormation (`!Ref`, `!Sub`, `!GetAtt` configurados en `.vscode/settings.json`). |
| **CloudFormation Linter** | `kddejong.vscode-cfn-lint` | Resalta en rojo en el editor cualquier error de sintaxis o política en `infra/urponline-oneclick.yaml`. |
| **SQLTools + PostgreSQL Driver** | `mtxr.sqltools` + `mtxr.sqltools-driver-pg` | Conectarse y ver las tablas (`Products`, `Orders`, `ProductEmbeddings`) de la base de datos `urp-postgres` sin salir de VS Code. |
| **GitHub Actions** | `github.vscode-github-actions` | Monitorear el progreso y estado de los workflows `deploy-aws.yml` y `destroy-aws.yml` desde el editor. |

---

## SOLUCIÓN DE PROBLEMAS COMUNES (CHEAT SHEET)

### 1. Error de puerto 5432 ocupado en Docker:
* **Mensaje:** `Ports are not available: listen tcp 0.0.0.0:5432: bind: An attempt was made to access a socket...`
* **Causa:** Tienes PostgreSQL instalado como servicio local de Windows y está acaparando el puerto 5432.
* **Solución en PowerShell (como Administrador):**
  ```powershell
  Get-Service -Name postgresql* | Stop-Service -Force
  Get-Service -Name postgresql* | Set-Service -StartupType Manual
  docker start urp-postgres
  ```

### 2. Error: `docker daemon is not running`:
* **Causa:** Docker Desktop no está abierto.
* **Solución:** Abre Docker Desktop desde el menú Inicio y espera a que el icono de la ballena esté en verde.

### 3. Error al compilar: `The process cannot access the file ... TiendaOnline.exe`:
* **Causa:** Hay una ejecución anterior de la tienda que no se cerró.
* **Solución en PowerShell:**
  ```powershell
  Stop-Process -Name TiendaOnline -Force -ErrorAction SilentlyContinue
  dotnet build
  ```

### 4. Error de permisos en Git: `Permission denied (publickey)`:
* **Causa:** No has aceptado la invitación de colaborador en GitHub.
* **Solución:** Entra a [https://github.com/Jesussv2/tiendaonline-grupo3](https://github.com/Jesussv2/tiendaonline-grupo3) y pulsa "Accept invitation".
