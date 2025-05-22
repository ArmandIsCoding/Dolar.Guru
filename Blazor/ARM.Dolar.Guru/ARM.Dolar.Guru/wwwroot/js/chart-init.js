document.addEventListener("DOMContentLoaded", function () {
    var ctx = document.getElementById("chart").getContext("2d");

    // Datos de los diferentes tipos de dólar
    var dolarData = [
        { "nombre": "Oficial", "compra": 1110, "venta": 1160 },
        { "nombre": "Blue", "compra": 1155, "venta": 1175 },
        { "nombre": "Bolsa", "compra": 1147.7, "venta": 1149.4 },
        { "nombre": "Contado con liquidación", "compra": 1162, "venta": 1168.9 },
        { "nombre": "Mayorista", "compra": 1137, "venta": 1146 },
        { "nombre": "Cripto", "compra": 1175.31, "venta": 1179.9 },
        { "nombre": "Tarjeta", "compra": 1443, "venta": 1508 }
    ];

    var labels = ["Hace 3 días", "Hace 2 días", "Ayer", "Hoy"];
    var datasets = [];

    var colores = [
        "#FF5733", "#33FF57", "#3357FF", "#F4D03F", "#AF7AC5", "#2ECC71", "#E74C3C"
    ];

    var estilosLinea = ["solid", "dashed", "dotted", "dashdot", "longdash", "shortdash", "dash"];

    dolarData.forEach((tipo, index) => {
        var data = [];
        for (var i = 0; i < labels.length; i++) {
            data.push(tipo.compra + Math.random() * 30);
        }

        datasets.push({
            label: tipo.nombre,
            data: data,
            borderColor: colores[index % colores.length],
            backgroundColor: colores[index % colores.length] + "80",
            borderDash: index % 2 === 0 ? [5, 5] : [], // Alternar líneas sólidas y punteadas
            tension: 0.4
        });
    });

    var chart = new Chart(ctx, {
        type: "line",
        data: {
            labels: labels,
            datasets: datasets
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                y: {
                    beginAtZero: false,
                    title: {
                        display: true,
                        text: "Cotización en ARS"
                    }
                },
                x: {
                    title: {
                        display: true,
                        text: "Fecha"
                    }
                }
            }
        }
    });
});