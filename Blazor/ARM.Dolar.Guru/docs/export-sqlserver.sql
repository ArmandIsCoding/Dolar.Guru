-- Run against the original SQL Server database; this is read-only.
-- Save the complete single JSON value as UTF-8 (no headers, truncation or row-count messages).
SET NOCOUNT ON;
SELECT
    JSON_QUERY((SELECT FechaEjecucion, JsonData FROM CotizacionesDolarJson FOR JSON PATH)) AS CotizacionesDolarJson,
    JSON_QUERY((SELECT FechaEjecucion, JsonData FROM CotizacionesOtrosJson FOR JSON PATH)) AS CotizacionesOtrosJson,
    JSON_QUERY((SELECT FechaEjecucion, JsonData FROM FuturoRavaJson FOR JSON PATH)) AS FuturoRavaJson,
    JSON_QUERY((SELECT FechaEjecucion, JsonData FROM ProyeccionesDolarJson FOR JSON PATH)) AS ProyeccionesDolarJson,
    JSON_QUERY((SELECT Titulo, Resumen, Url, Fuente, FechaPublicacion FROM News WHERE Url IS NOT NULL FOR JSON PATH)) AS News
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
