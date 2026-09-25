# Dólar Gurú

Panel de mercado argentino en Blazor Server / .NET 10, con SQLite local.

## Desarrollo

```sh
dotnet build ARM.Dolar.Guru.sln
dotnet run --project ARM.Dolar.Guru.Sync -- --quotes-only
dotnet run --project ARM.Dolar.Guru.BaseBlazor
```

Sin argumentos, Sync descarga también futuros y noticias. Cada fuente falla por separado;
el código de salida es 1 si alguna falla y 0 si todas completan. Se conservan los últimos
datos válidos. El programa termina después de una pasada; no es un servicio residente.

La base se crea automáticamente en LocalApplicationData/DolarGuru/market.db. En macOS:
`~/Library/Application Support/DolarGuru/market.db`. Ambos ejecutables comparten esa ruta
cuando se ejecutan con el mismo usuario. Para usar una ubicación específica, definir
`DOLAR_GURU_DB_PATH` con una ruta absoluta. En producción es obligatorio configurarla
explícitamente, ya que IIS y la tarea programada pueden ejecutar con usuarios distintos.
La variable prevalece sobre `Database:Path` (web) y `--database` (Sync).

## Arquitectura

- Models: contratos y conversión JSON.
- Services: inicialización SQLite y consultas; conexiones cortas por operación.
- Sync: orquestación de fuentes, validación, persistencia e importación de históricos.
- BaseBlazor: presentación, conversor y lectura periódica de la base.

SQLite usa WAL, timeout de 30 segundos, índices de fecha y consultas parametrizadas.
El sitio no escribe cotizaciones ni realiza solicitudes a proveedores por visitante.
La actualización del panel (60 s) es independiente de la frecuencia de Sync.
Las cotizaciones y fechas mostradas provienen de las fuentes, sin datos de demostración.
Los escenarios son extrapolaciones de promedios diarios, no IA ni probabilidades de acierto.

## IIS en Windows

1. Instalar IIS, WebSocket Protocol y el **Hosting Bundle .NET 10**. Reiniciar IIS después de instalarlo.
2. Publicar desde la solución (ajustar RID si el servidor no es x64):

```sh
dotnet publish ARM.Dolar.Guru.BaseBlazor -c Release -r win-x64 --self-contained false -o artifacts/web
dotnet publish ARM.Dolar.Guru.Sync -c Release -r win-x64 --self-contained false -o artifacts/sync
```

3. Copiar los directorios publicados a carpetas separadas. El SDK genera web.config.
   Crear un pool dedicado, No Managed Code, 64 bits y **un solo worker**.
4. Crear una carpeta persistente fuera del sitio, por ejemplo `C:\DolarGuru\data`.
   Dar permiso **Modificar** sobre esa carpeta al usuario de la tarea y a
   `IIS AppPool\DolarGuru` (reemplazar con el nombre real del pool).
   SQLite necesita crear archivos `-wal` y `-shm` junto a la base.
5. Configurar en el proceso IIS `DOLAR_GURU_DB_PATH=C:\DolarGuru\data\market.db`.
   Alternativa: `Database:Path` en appsettings. Para la tarea, usar la misma variable o
   `--database C:\DolarGuru\data\market.db`. Reciclar el pool tras cambiar variables.
6. Programar `ARM.Dolar.Guru.Sync.exe --database C:\DolarGuru\data\market.db`
   cada 5 minutos, con un usuario que tenga acceso a la carpeta y a Internet.
   Configurar **No iniciar una nueva instancia** si la anterior sigue activa.
   Capturar stdout/stderr en logs y revisar los códigos de salida.
7. Configurar HTTPS, el host real en AllowedHosts y WebSockets para los circuitos Blazor.
   Verificar portada, conversor, noticias y futuros después de publicar.

La base debe residir en disco local, no en una carpeta de red. Esta configuración sirve
para una instancia de IIS con lecturas concurrentes y un escritor periódico. Si se
necesita escalar a varios servidores, evaluar PostgreSQL en lugar de compartir el archivo.
No publicar la base bajo wwwroot ni sobrescribirla en un despliegue.

### Copias de seguridad

Usar la API de backup de SQLite o la orden `.backup` de sqlite3 sobre la base activa.
No copiar solamente market.db mientras existen escrituras en WAL. Alternativamente,
detener web y Sync antes de copiar todos los archivos de la base. Probar restauraciones
en una ruta alternativa. La retención de históricos es indefinida; supervisar tamaño.

## Migrar los datos anteriores

La aplicación nueva no necesita SQL Server, pero **los datos antiguos no se transfieren solos**.
No se incluyen credenciales ni se altera la base anterior.

1. Hacer una copia de seguridad de SQL Server.
2. Ejecutar `docs/export-sqlserver.sql` en la base original y guardar el JSON completo
   como UTF-8 en `export.json`. Asegurarse de que el cliente no trunque la columna.
3. Antes de habilitar la tarea programada, ejecutar:

```sh
dotnet run --project ARM.Dolar.Guru.Sync -- --database /ruta/market.db --import /ruta/export.json
```

La importación es transaccional: un archivo inválido no deja datos parciales. Repetir
el mismo export no duplica snapshots; las noticias se deduplican por URL. Comparar
recuentos e inspeccionar fechas tras importar. Los IDs de noticias se regeneran.
Las fechas SQL sin zona se conservan y se interpretan como UTC: verificar la zona
que usaba la base original antes de importar y normalizar el export si corresponde.
Los escenarios heredados conservan su texto original hasta la próxima sincronización.

## Verificación

```sh
dotnet run --project ARM.Dolar.Guru.Tests
dotnet build ARM.Dolar.Guru.sln -c Release
```

Las pruebas de integración usan SQLite real en un directorio temporal y un proveedor HTTP
simulado, sin tocar la base de desarrollo ni depender de Internet.

Referencias: [SQLite y concurrencia](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors),
[publicación en IIS](https://learn.microsoft.com/en-us/aspnet/core/tutorials/publish-to-iis?view=aspnetcore-10.0).
Dirección visual inspirada en la jerarquía de mercados de [Finanzas Argy](https://www.finanzasargy.com/),
con una identidad propia para Dólar Gurú.
