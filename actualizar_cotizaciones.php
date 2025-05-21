<?php
// archivo: actualizar_cotizaciones.php

$url = "https://dolarapi.com/v1/dolares";
$response = file_get_contents($url);

if ($response !== false) {
    file_put_contents("datos_dolar.json", $response);
} else {
    // Opcional: loguear errores
    file_put_contents("error.log", date('c') . " - Error al obtener datos\n", FILE_APPEND);
}
?>