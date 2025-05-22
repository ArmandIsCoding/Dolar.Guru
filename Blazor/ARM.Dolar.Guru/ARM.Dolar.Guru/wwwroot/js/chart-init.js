document.addEventListener("DOMContentLoaded", function () {
    var ctx = document.getElementById("chart").getContext("2d");

    var chart = new Chart(ctx, {
        type: "line",
        data: {
            labels: ["Hace 2 días", "Hace 1 día", "Hoy"],
            datasets: [
                {
                    label: "Dólar Oficial",
                    data: [880.10, 895.30, 900.50],
                    borderColor: "#007bff",
                    backgroundColor: "rgba(0, 123, 255, 0.2)",
                    tension: 0.4
                },
                {
                    label: "Dólar Blue",
                    data: [1130.90, 1145.20, 1150.75],
                    borderColor: "#28a745",
                    backgroundColor: "rgba(40, 167, 69, 0.2)",
                    tension: 0.4
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false
        }
    });
});