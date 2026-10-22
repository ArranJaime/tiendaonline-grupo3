# GUÍA DE INSTALACIÓN RÁPIDA PARA COMPAÑEROS DE EQUIPO
**Proyecto:** URPOnline · TiendaOnline (.NET 8 + AWS)  
**Repositorio del Grupo:** `https://github.com/Jesussv2/tiendaonline-grupo3`

Esta guía está diseñada para que cualquier integrante del grupo pueda clonar el repositorio, instalar todas las dependencias y tener la tienda funcionando en su propia computadora en **menos de 15 minutos**.

---

## ÍNDICE RÁPIDO
1. [Paso 0: Acceso al Repositorio en GitHub](#paso-0-acceso-al-repositorio-en-github)
2. [Paso 1: Instalar los Programas Necesarios (Windows)](#paso-1-instalar-los-programas-necesarios-windows)
3. [Paso 2: Iniciar Docker (PostgreSQL con pgvector + Redis)](#paso-2-iniciar-docker-postgresql-con-pgvector--redis)
4. [Paso 3: Descargar el Modelo de Búsqueda Semántica](#paso-3-descargar-el-modelo-de-búsqueda-semántica)
5. [Paso 4: Compilar y Ejecutar la Tienda por Primera Vez](#paso-4-compilar-y-ejecutar-la-tienda-por-primera-vez)
6. [Paso 5: Probar el Worker de Catálogo](#paso-5-probar-el-worker-de-catálogo)
7. [Paso 6: Extensiones de VS Code Recomendadas](#paso-6-extensiones-de-vs-code-recomendadas)
8. [Solución de Errores Frecuentes](#solución-de-errores-frecuentes)

---

## PASO 0: Acceso al Repositorio en GitHub

1. **Acepta la invitación:** Jesús debe haberte agregado como colaborador en GitHub (`Settings` $\rightarrow$ `Collaborators`). Revisa tu correo o entra a [https://github.com/Jesussv2/tiendaonline-grupo3](https://github.com/Jesussv2/tiendaonline-grupo3) y acepta la invitación.
2. Abre **PowerShell** en tu computadora y navega a la carpeta donde guardas tus proyectos:
   ```powershell
   cd C:\MisProyectos  # o la carpeta que prefieras
   ```
3. Clona el repositorio:
   ```powershell
   git clone https://github.com/Jesussv2/tiendaonline-grupo3.git
   cd tiendaonline-grupo3
   ```

---

## PASO 1: Instalar los Programas Necesarios (Windows)

Abre **PowerShell como Administrador** (clic derecho en el botón de Inicio $\rightarrow$ *Terminal / PowerShell como Administrador*) y copia y pega este comando para instalar todo en un solo paso:

```powershell
winget install Git.Git Microsoft.DotNet.SDK.8 Docker.DockerDesktop Amazon.AWSCLI Microsoft.VisualStudioCode
```

> ⚠️ **IMPORTANTE:**
> 1. Una vez terminada la instalación, **cierra y vuelve a abrir PowerShell** para que se reconozcan los comandos.
> 2. Si Docker Desktop te pide reiniciar la computadora y habilitar WSL 2, **reinicia la PC**.

### Comprobar que todo quedó bien instalado:
```powershell
git --version       # Debe mostrar git version 2.x
dotnet --version    # Debe empezar en 8.x
docker --version    # Debe mostrar Docker version 2x.x
aws --version       # Debe mostrar aws-cli/2.x
code --version      # Debe mostrar versión de VS Code
```

---

## PASO 2: Iniciar Docker (PostgreSQL con pgvector + Redis)

La tienda necesita dos bases de datos locales:
- **PostgreSQL 16 con extensión `pgvector`:** Para productos, pedidos y búsqueda vectorial por similitud coseno.
- **Redis 7:** Para guardar las sesiones y los carritos activos entre servidores.

1. **Abre Docker Desktop** desde el menú Inicio de Windows y espera a que el icono de la ballena en la esquina inferior izquierda esté en **verde** (*Engine running*).
2. En tu terminal de PowerShell, ejecuta estos dos comandos para crear y levantar los contenedores:

```powershell
# 1. Base de datos PostgreSQL con soporte vectorial (puerto 5432)
docker run -d --name urp-postgres -e POSTGRES_USER=urponline -e POSTGRES_PASSWORD=localonly -e POSTGRES_DB=urponline -p 5432:5432 pgvector/pgvector:pg16

# 2. Redis para sesiones y carritos (puerto 6379)
docker run -d --name urp-redis -p 6379:6379 redis:7
```

3. Comprueba que ambos contenedores estén corriendo:
```powershell
docker ps
```
*(Debes ver `urp-postgres` y `urp-redis` en estado `Up`)*.

> 💡 **Nota para los siguientes días:** Cuando apagues tu PC y la vuelvas a encender, solo debes iniciar Docker Desktop y ejecutar:
> ```powershell
> docker start urp-postgres urp-redis
> ```

---

## PASO 3: Descargar el Modelo de Búsqueda Semántica

La búsqueda de la tienda utiliza inteligencia artificial local (modelo ONNX multilingüe) para encontrar productos por su significado (por ejemplo, buscar *"algo para el frío"* y que encuentre casacas sin que coincida exactamente la palabra). 

Este modelo pesa ~120 MB y no se sube a GitHub por su tamaño. Para descargarlo automáticamente con un script seguro con verificación de hash SHA-256:

Desde la raíz de la carpeta `tiendaonline-grupo3`, ejecuta:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/descargar-modelo.ps1
```

Esperar a que descargue y diga:  
`OK: sentencepiece.bpe.model`  
`OK: model.onnx`  
`Modelo listo en ...\ml-models\paraphrase-multilingual-MiniLM-L12-v2`

---

## PASO 4: Compilar y Ejecutar la Tienda por Primera Vez

La primera vez que corres la aplicación en tu computadora, debes indicarle que cree las tablas en PostgreSQL, aplique las migraciones y cargue los 44 productos del catálogo con sus vectores de búsqueda.

Ejecuta en PowerShell:

```powershell
# 1. Restaurar paquetes NuGet y compilar
dotnet restore
dotnet build

# 2. Variables de entorno para inicializar la base de datos (solo la primera vez)
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:InitializeDatabase = "true"

# 3. Iniciar la tienda en el puerto 8080
dotnet run --urls http://localhost:8080
```

### Probar en tu navegador:
1. Abre tu navegador y entra a:  
   👉 **`http://localhost:8080`**  
   *(Verás la tienda con los 44 productos de tecnología, ropa, hogar, libros, etc.)*
2. Prueba el endpoint de salud del balanceador:  
   👉 **`http://localhost:8080/health`**  
   *(Debe responder: `{"status":"ok"}`)*
3. Prueba la búsqueda semántica: escribe en el buscador *"musica"* o *"lluvia"* y verifica que aparezcan productos relacionados con su porcentaje de similitud coseno.

> 💡 **Para las siguientes veces:** Ya no necesitas `$env:InitializeDatabase = "true"`. Simplemente ejecutas:
> ```powershell
> dotnet run --urls http://localhost:8080
> ```
> Para detener la tienda en la terminal, presiona `Ctrl + C`.

---

## PASO 5: Probar el Worker de Catálogo

El proyecto incluye un Worker en consola que simula la importación nocturna de productos desde CSV (requerimiento de la empresa).

Para probarlo en tu máquina:
```powershell
dotnet run --project Worker -- --source local --file examples/products.csv
```
Verás en consola cómo lee el CSV, valida los datos e inserta/actualiza los productos en PostgreSQL.

---

## PASO 6: Extensiones de VS Code Recomendadas

Abre el proyecto en Visual Studio Code:
```powershell
code .
```

Cuando se abra VS Code, te aparecerá una ventana abajo a la derecha diciendo *"This repository has recommended extensions"*. Haz clic en **Install All** (Instalar todas).

O instálalas directamente desde la terminal:
```powershell
code --install-extension ms-dotnettools.csdevkit
code --install-extension amazonwebservices.aws-toolkit-vscode
code --install-extension redhat.vscode-yaml
```

---

## SOLUCIÓN DE ERRORES FRECUENTES

### 1. Error al levantar Docker: `Ports are not available: listen tcp 0.0.0.0:5432: bind: An attempt was made to access a socket...`
* **Causa:** Tienes instalado PostgreSQL directamente en Windows y su servicio está ocupando el puerto 5432 antes que Docker.
* **Solución rápida en PowerShell (como Administrador):**
  ```powershell
  Get-Service -Name postgresql* | Stop-Service -Force
  Get-Service -Name postgresql* | Set-Service -StartupType Manual
  docker start urp-postgres
  ```

### 2. Error: `docker: error during connect: This error may indicate that the docker daemon is not running.`
* **Causa:** Docker Desktop está cerrado o aún se está iniciando.
* **Solución:** Abre Docker Desktop desde el menú Inicio, espera 30 segundos a que el icono de la ballena esté en verde y vuelve a ejecutar el comando.

### 3. Error al compilar: `The process cannot access the file ... TiendaOnline.exe because it is being used by another process.`
* **Causa:** Dejaste una instancia anterior de la tienda corriendo en otra ventana de terminal o en segundo plano.
* **Solución en PowerShell:**
  ```powershell
  Stop-Process -Name TiendaOnline -Force -ErrorAction SilentlyContinue
  dotnet build
  ```

### 4. Error al clonar: `Permission denied (publickey)` o `Authentication failed`
* **Causa:** El repositorio `tiendaonline-grupo3` es privado y no has iniciado sesión en GitHub o no has aceptado la invitación de colaborador.
* **Solución:**
  1. Asegúrate de haber aceptado la invitación que te llegó por correo de GitHub.
  2. Si usas HTTPS, al clonar te pedirá iniciar sesión con tu cuenta de GitHub en el navegador (*Sign in with browser*).

---

## RESUMEN DE COMANDOS DIARIOS

| Acción | Comando (desde la carpeta `tiendaonline-grupo3`) |
|---|---|
| **Iniciar BD y Redis** | `docker start urp-postgres urp-redis` |
| **Bajar últimos cambios de GitHub** | `git pull origin main` |
| **Ejecutar la tienda** | `dotnet run --urls http://localhost:8080` |
| **Detener la tienda** | `Ctrl + C` en la terminal |
| **Ver estado de los contenedores** | `docker ps` |
