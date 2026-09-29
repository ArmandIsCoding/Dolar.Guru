# El país en contexto — primera versión

La home incluye una síntesis y hasta cinco temas; `/contexto` muestra la edición
completa con citas por afirmación. `/news` conserva los enlaces a publicaciones
originales y equilibra los medios dentro de la actualidad reciente.

**No hay contenido de demostración en producción, ni llamadas a IA por visitante.**
La instalación inicial muestra un estado vacío. Hace falta configurar una API,
autorizar fuentes para su procesamiento y aprobar una edición para verla publicada.
La web no recibe la clave API y no tiene rutas públicas de administración editorial.

## Qué hace el sync

1. Descarga los RSS configurados, guarda título, URL canónica, resumen, texto incluido
   en el propio feed, fecha, medio, grupo editorial, hash y fecha de consulta.
2. Actualiza correcciones de una URL existente sin cambiar su ID. Deduplica URLs y
   coincidencias exactas de contenido; no considera esto una verificación independiente
   de cables sindicados. La agrupación temática posterior es asistida por IA.
3. Selecciona noticias recientes con contenido suficiente y autorización explícita para
   IA. Balancea grupos editoriales y limita material internacional; no rellena con noticias viejas.
4. Si hay cobertura y cuota local (y presupuesto en modo pago), envía **solo esos textos** al proveedor configurado.
   No ofrece herramientas, navegación ni acceso a la base al modelo.
5. Valida estructura, fechas, URLs, IDs y que cada fragmento de evidencia exista en la
   fuente correspondiente. Exige 4–6 temas, al menos tres grupos editoriales citados
   y como máximo un tema internacional. Guarda exclusivamente un **borrador**.
6. Una persona revisa y publica explícitamente. Hasta entonces se sirve la última
   edición aprobada; al superar 12 horas aparece un aviso de antigüedad.

**La coincidencia de un fragmento no demuestra que respalde la interpretación**:
la revisión editorial sigue siendo obligatoria. Tampoco la cantidad de medios
garantiza pluralidad ni corroboración independiente. Revisar cifras, atribuciones,
acusaciones, fechas, contexto, discrepancias y titulares antes de aprobar.

En esta versión NO se descargan páginas completas de artículos ni se atraviesan
paywalls. Para ampliar la cobertura, contratar o habilitar fuentes con condiciones
compatibles (RSS de texto, APIs/licencias), y revisar permisos antes de procesarlas.
Los medios por defecto son Clarín, Perfil, Página/12, La Nación, Ámbito y Chequeado.
Se verificó la respuesta XML de los nuevos feeds durante la implementación;
su disponibilidad y frecuencia pueden cambiar. Chequeado puede actualizar con menor
frecuencia y no aparecer en una ventana diaria. No se incluyen aún fuentes
internacionales o comunicados oficiales: el registro admite agregarlas.

## Configuración (junto al ejecutable Sync)

`News:Sources` reemplaza la lista por defecto si está presente. Cada entrada tiene
`Id`, `Name`, `Url`, `PublisherGroup`, `Enabled`, `AllowAiUse` e `IsInternational`
(opcional, false). Usar el mismo `PublisherGroup` para medios del mismo grupo.

`AllowAiUse` está en **false** en todas las fuentes: descargarlas para el feed no
significa autorizar su transmisión a un proveedor de IA. Tras revisar condiciones
de reutilización/licencia, activar únicamente las fuentes autorizadas. Desactivar
o quitar una fuente revoca su elegibilidad en los siguientes sync/generación.
Se necesitan al menos cuatro noticias con texto suficiente de tres grupos.

`Briefing` viene desactivado:

| Campo | Uso |
|---|---|
| `Enabled` | `true` habilita llamadas a la API; dejar `false` hasta completar la configuración. |
| `Provider` | `OpenAI` (Responses) o `Gemini` (generateContent). |
| `Model` | ID vigente y habilitado en tu cuenta, compatible con JSON estructurado. Sin modelo predeterminado. |
| `ApiKey` | Clave del proveedor, solo en el archivo privado de Sync. No requiere variables de entorno. |
| `BillingMode` | `Paid` (por defecto, compatible con configuraciones anteriores) o `FreeTier` (solo Gemini). |
| `FreeTierConfirmed` | En `FreeTier`, debe ser `true` tras verificar proyecto/modelo gratuito en AI Studio. No verifica ni modifica Google. |
| `MonthlyBudgetUsd` | En `Paid`, presupuesto local mensual UTC positivo, por defecto 10 USD. En `FreeTier`, debe ser 0 explícitamente. |
| `InputUsdPerMillion`, `OutputUsdPerMillion` | En `Paid`, tarifas **máximas** vigentes positivas en USD/millón de tokens. En `FreeTier`, ambas deben ser 0. |
| `MaxRequestsPer24Hours` | Máximo local de intentos en una ventana móvil de 24 h; por defecto 4, rango 1–4. |
| `MaxRequestsPerMonth` | Máximo local de intentos por mes UTC; por defecto 124, rango 1–124. |
| `MaxInputTokensPerRequest` | Máximo de entrada estimada (bytes de petición + margen); por defecto 200000, rango 1000–250000. Si se excede, no llama a la API; reducir `MaxArticles`. |
| `MaxOutputTokens` | Límite de salida; por defecto 5000. Una respuesta truncada no se publica. |
| `ScheduleHours` | `[8,12,16,20]`, hora argentina UTC−03. Hasta cuatro franjas diarias. |
| `LookbackHours` | Ventana de noticias, por defecto 24 h. |
| `MaxArticles`, `MaxPerPublisher` | Por defecto 24 y 6. |
| `MinPublishers` | Por defecto 3; mínimo obligatorio 3. |
| `RequestTimeoutSeconds` | Por defecto 90 s. Sin reintentos automáticos. |

Para `Paid`, elegir el modelo y copiar sus tarifas actuales desde la documentación del proveedor;
usar el precio más alto aplicable a entrada/salida para ese contexto, incluido
razonamiento, sin asumir descuentos de caché. No usar cuotas del chat como presupuesto
de API. La app no activa facturación, no crea cuentas y no genera claves.

### Archivo privado, sin variables de entorno

Sync carga primero `appsettings.json` junto al ejecutable y después, si existe,
`appsettings.Production.json` en la misma carpeta. No necesita configurar ningún
nombre de entorno. El segundo archivo sobrescribe solo los valores que contiene.
Usar la plantilla `ARM.Mesa.Bursatil.Sync/appsettings.Production.example.json`
(sin credenciales) para crear el archivo privado directamente en el servidor:

```json
{
  "Briefing": {
    "Enabled": false,
    "Provider": "Gemini",
    "Model": "gemini-3.8-flash",
    "BillingMode": "FreeTier",
    "FreeTierConfirmed": false,
    "ApiKey": "PEGAR_LA_CLAVE_AQUI",
    "MonthlyBudgetUsd": 0,
    "InputUsdPerMillion": 0,
    "OutputUsdPerMillion": 0,
    "MaxRequestsPer24Hours": 4,
    "MaxRequestsPerMonth": 124,
    "MaxInputTokensPerRequest": 200000
  }
}
```

La plantilla está preparada para probar Gemini gratis, pero **desactivada y sin clave**.
Verificar que el proyecto de la clave siga en **Nivel gratuito**, y que el modelo
elegido tenga cuota y soporte JSON estructurado. Después poner `FreeTierConfirmed=true`.
`gemini-3.8-flash` es el ejemplo verificado en la documentación el 29/09/2026;
la disponibilidad y cuota exactas deben revisarse en el proyecto **Mesa Bursatil**, no
inferirse de otro proyecto ni de la suscripción del chat. No habilitar facturación
para este piloto. El nivel gratuito puede usar los textos enviados para mejorar
productos de Google: no enviar datos privados y revisar las condiciones de las fuentes.

Completar la clave y validar la configuración antes de poner `Enabled=true`. También se pueden
sobrescribir `Database:Path`, `Logging:Directory` y otras opciones en ese archivo.
La autorización de fuentes `AllowAiUse` sigue siendo necesaria.

Para que el archivo quede **fuera de la carpeta de despliegue**, recomendamos
guardarlo como `C:\MesaBursatil\config\appsettings.Production.json` y agregar
este argumento a la tarea de Windows (y a los comandos de revisión):

```powershell
.\ARM.Mesa.Bursatil.Sync.exe --settings 'C:\MesaBursatil\config\appsettings.Production.json'
```

Con `--settings`, ese archivo reemplaza al privado adyacente (siempre se hereda
el `appsettings.json` base). La ruta debe ser absoluta y el archivo debe existir;
un error no activa una configuración alternativa silenciosa. Funciona aunque la
tarea arranque en System32. No se leen variables de entorno para la clave de IA.

El archivo privado está excluido de Git y de la copia de compilación/publicación.
No guardar la clave en el `appsettings.json` base ni en la plantilla, y no incluirla
en argumentos, logs o conversaciones. Restringir permisos de lectura a la cuenta
de Sync y administradores: el JSON contiene la clave en texto plano. No colocar
el archivo bajo `wwwroot` ni copiarlo a la web. Si el despliegue borra la carpeta
destino completa, también borrará un privado adyacente: usar la ruta externa para
evitarlo. Las herramientas de despliegue propias deben respetar esta separación.

Para desarrollo también se puede usar `--settings` apuntando a un archivo privado
externo; el archivo de la carpeta del proyecto no se copia automáticamente a `bin`.

## Generación y revisión desde Windows

Publicar Web y Sync como siempre, respaldar SQLite y mantener la misma ruta. Las
migraciones son aditivas; conservan las noticias y sus IDs. Recomendado almacenar
SQLite fuera del directorio publicado. Ejecutar desde la carpeta publicada de Sync:

```powershell
# Validación LOCAL de configuración y presencia de clave. No consulta Google ni descarga noticias.
# Funciona con Enabled=false; FreeTierConfirmed debe estar en true para validar FreeTier.
.\ARM.Mesa.Bursatil.Sync.exe --settings 'C:\MesaBursatil\config\appsettings.Production.json' --briefing-check

# Descarga feeds y mercados. Si IA está habilitada, intenta la franja editorial actual.
.\ARM.Mesa.Bursatil.Sync.exe

# Solo intenta generar con noticias que YA estén descargadas; respeta horario y presupuesto.
.\ARM.Mesa.Bursatil.Sync.exe --briefing-generate

# Ver pendientes y la edición actualmente publicada.
.\ARM.Mesa.Bursatil.Sync.exe --briefing-list

# Muestra JSON legible con afirmaciones, evidencias y enlaces de la edición.
.\ARM.Mesa.Bursatil.Sync.exe --briefing-review 1

# Solo después de leer y contrastar la edición. Esta orden la hace pública.
.\ARM.Mesa.Bursatil.Sync.exe --briefing-publish 1 --reviewed-by 'Armando' --confirm-reviewed

# Rechaza un borrador sin borrar su historial ni reemplazar lo publicado.
.\ARM.Mesa.Bursatil.Sync.exe --briefing-reject 1
```

Si se usa archivo externo, agregar `--settings 'C:\MesaBursatil\config\appsettings.Production.json'`
a TODOS los comandos y a la tarea de Windows. Reemplazar `1` por el ID real. Todos aceptan `--database 'C:\ruta\market.db'`;
`MESA_BURSATIL_DB_PATH` sigue teniendo prioridad. Los comandos editoriales no descargan
mercados. No se publica ningún borrador automáticamente. Un borrador de más de 48 h
no puede aprobarse: generar uno nuevo. Por ahora no hay edición manual de borradores
ni panel web de administración: ante un error, rechazar y revisar entradas/prompt.

No hace falta una tarea adicional: Sync cada cinco minutos comprueba la franja actual
(8/12/16/20). No recupera ediciones de franjas perdidas, ni genera antes de la primera
hora del día. `--quotes-only` nunca llama a IA. Mantener “No iniciar una instancia
nueva” en el Programador de tareas. Un bloqueo transaccional en SQLite también evita
duplicar una franja entre procesos. Con fuentes idénticas a una edición generada no
se vuelve a llamar a la API, aunque haya otra franja disponible.

## Costes y fallos

`BriefingRuns` registra franja, proveedor/modelo, `BillingMode`, reserva USD, uso y coste calculado
con las tarifas configuradas. Las filas previas se conservan con `BillingMode=Paid`.
En `Paid`, antes de cada llamada se reserva una cota conservadora
de entrada (bytes de la petición más margen) y de salida. Las reservas pendientes o
fallidas cuentan contra el presupuesto: un timeout no prueba que la llamada fuese gratis.
Un proceso interrumpido no reintenta esa franja. Los errores no sobrescriben una edición.

Este límite es **local a esta base y a estas tarifas**: no es un tope contractual
del proveedor, no cubre otras aplicaciones/API keys, cambios de precios ni impuestos.
Configurar también límites/alertas en el proveedor y conciliar con su factura real.
Cambiar de base reinicia el registro local; no hacerlo para evadir el límite.

### Modo gratuito y límites de solicitudes

En `FreeTier` no se inventan tarifas: la reserva y el coste **locales** son cero y
se registra el uso de tokens. Esto **no certifica una factura de cero ni fuerza a
Google a usar un nivel gratuito**: generateContent no lleva un parámetro de "no cobrar".
Si alguien vincula facturación al proyecto o cambia la clave por una de un proyecto
pago, Google puede cobrar aunque este JSON diga `FreeTier`. Sync no activa facturación
ni hace fallback a otros modelos/proveedores. Mantener el proyecto sin facturación;
si se quiere pasar a pago, usar `Paid` y sus tarifas/presupuesto vigentes.

Los límites locales de solicitudes y entrada se aplican a ambos modos. Antes de
llamar se reserva una solicitud mediante una transacción SQLite. Todas las filas
cuentan (exitosas, fallidas y pendientes), sin importar modelo, proveedor o clave.
Un reinicio, una ejecución manual o un cambio de configuración no devuelve cuota.
Hay como máximo una reserva por minuto, además de los límites móviles de 24 h y
mensuales UTC. Estos controles no cuentan el consumo de otras aplicaciones ni de
otras bases de datos. La cuota de Google es por proyecto y su límite diario se
reinicia a medianoche del Pacífico; nuestra ventana móvil de 24 h es intencionalmente
conservadora y no intenta reproducir ese calendario.

Ante HTTP 429 se informa agotamiento de cuota/frecuencia sin exponer la respuesta del
proveedor ni la clave. No se reintenta la franja fallida; un sync futuro podrá intentar
una nueva franja si hay cobertura y cuota local. La última edición publicada se conserva.
No se recortan silenciosamente fuentes cuando la entrada supera el límite; se registra
la omisión y se debe reducir `MaxArticles` manteniendo la diversidad editorial.

Errores de mercado y de IA se registran por separado. Un error de IA puede producir
código de salida 1 pero no revierte las cotizaciones ya sincronizadas. Falta de noticias,
franja repetida, IA desactivada o presupuesto insuficiente son omisiones registradas,
no llamadas fallidas. Inspeccionar logs y `BriefingRuns` si no aparecen borradores.

## Pruebas

```sh
dotnet run --project ARM.Mesa.Bursatil.Tests
dotnet build ARM.Mesa.Bursatil.sln -c Release
```

Usan SQLite temporal y respuestas HTTP simuladas; no requieren clave, no gastan crédito
y no escriben datos ficticios en la base real. Incluyen migración, diversidad, revocación
de permisos IA, RSS malformado/XXE, ambos proveedores, negativas, truncado, citas
inexistentes, publicación manual, presupuesto, FreeTier con tarifas cero, límites
móviles/mensuales, HTTP 429, concurrencia y conservación de la edición.
La fidelidad semántica y el rendimiento real del modelo elegido requieren un piloto
supervisado con datos autorizados: no quedan certificados por estas pruebas.

Referencias: [OpenAI JSON estructurado](https://developers.openai.com/api/docs/guides/structured-outputs),
[Gemini JSON estructurado](https://ai.google.dev/gemini-api/docs/structured-output),
[precios OpenAI](https://developers.openai.com/api/docs/pricing),
[precios Gemini](https://ai.google.dev/gemini-api/docs/pricing),
[cuotas Gemini](https://ai.google.dev/gemini-api/docs/rate-limits),
[modelo Gemini 3.8 Flash](https://ai.google.dev/gemini-api/docs/models/gemini-3.8-flash).
