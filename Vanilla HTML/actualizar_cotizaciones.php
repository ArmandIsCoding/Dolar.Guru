<?php
// archivo: actualizar_cotizaciones.php

date_default_timezone_set('America/Argentina/Buenos_Aires'); // Set timezone to GMT-3

$apiUrl = "https://dolarapi.com/v1/dolares";
$historicalFile = "historical_datos_dolar.json";
$currentFile = "datos_dolar.json";

// Function to check if an update is needed
function needsUpdate($file) {
    echo "Checking if update is needed...\n";

    if (!file_exists($file)) {
        echo "Current data file does not exist. Update needed.\n";
        return true;
    }

    $data = json_decode(file_get_contents($file), true);
    if (empty($data)) {
        echo "Current data file is empty. Update needed.\n";
        return true;
    }

    $lastUpdate = new DateTime(end($data)['fechaActualizacion']);
    $now = new DateTime();
    $interval = $now->getTimestamp() - $lastUpdate->getTimestamp();

    $minutes = intdiv($interval, 60);
    $seconds = $interval % 60;

    echo "Last update was $minutes minutes and $seconds seconds ago.\n";

    return $interval > 600; // 600 seconds = 10 minutes
}

// Function to append new data to the historical file
function appendToHistorical($newData, $file) {
    echo "Appending new data to historical file...\n";

    $historicalData = [];

    if (file_exists($file)) {
        echo "Historical file exists.\n";
        echo "Loading existing data...\n";
        $historicalData = json_decode(file_get_contents($file), true);
    } else {
        echo "Historical file does not exist.\n";
        echo "Creating new file...\n";
    }

    // Add new entries with the current timestamp
    // $timestamp = (new DateTime())->format('Y-m-d\TH:i:s.000\Z');
    // foreach ($newData as $entry) {
    //     $historicalData[] = [
    //         "fecha" => $timestamp,
    //         "datos" => $entry
    //     ];
    // }

    $historicalData[] = [
        "fecha" => $timestamp,
        "datos" => $newData
    ];

    file_put_contents($file, json_encode($historicalData, JSON_PRETTY_PRINT));

    echo "Data appended successfully.\n";
}

// Update if needed
if (needsUpdate($currentFile)) {
    echo "Fetching data from API...\n";
    $response = file_get_contents($apiUrl);

    if ($response !== false) {
        echo "Data fetched successfully.\n";
        $data = json_decode($response, true);

        // Add current timestamp to each entry
        $timestamp = (new DateTime())->format('Y-m-d\TH:i:s.000\Z');
        foreach ($data as &$entry) {
            $entry['fechaActualizacion'] = $timestamp;
        }

        // Save current data
        echo "Saving current data...\n";
        file_put_contents($currentFile, json_encode($data, JSON_PRETTY_PRINT));
        echo "Current data saved successfully.\n";

        // Append to historical data
        appendToHistorical($data, $historicalFile);
    } else {
        echo "Error fetching data from API.\n";
        // Optional: log errors
        file_put_contents("error.log", date('c') . " - Error al obtener datos\n", FILE_APPEND);
    }
} else {
    echo "No update needed.\n";
    echo "Last data is recent.\n";
}

echo "Script execution completed.\n";
?>
