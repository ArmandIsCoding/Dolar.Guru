<?php
// archivo: cotizaciones.php

header("Content-Type: application/json");

$archivo = "datos_dolar.json";

if (file_exists($archivo)) {
    echo file_get_contents($archivo);
} else {
    echo json_encode(["error" => "Datos no disponibles"]);
}
?>