//<!-- ///////////////////////////////////////// PARA EL GRÁFICO, LO HIZO LA IA JUJÚ ///////////////////////////////////////// -->
<script>
    let exchangeRateChart;
    let historicalData = [];
    let selectedCurrencyType = 'blue';

    // Fetch historical data from JSON file
    fetch('historical_datos_dolar.json')
            .then(response => response.json())
            .then(data => {
        historicalData = data;
    updateChart();
            });

    function changeCurrencyType(currencyType) {
        selectedCurrencyType = currencyType;
    document.getElementById('selectedCurrency').innerText = `USD ${currencyType.charAt(0).toUpperCase() + currencyType.slice(1)}`;
    updateChart();
        }

    function changeTimeframe(timeframe) {
        // Update the chart based on the selected timeframe
        document.querySelectorAll('.timeframe-btn').forEach(btn => btn.classList.remove('active'));
    document.querySelector(`.timeframe-btn[onclick="changeTimeframe('${timeframe}')"]`).classList.add('active');
    updateChart();
        }

    function updateChart() {
            const labels = [];
    const data = [];

            historicalData.forEach(entry => {
                const date = new Date(entry.fecha);
                const rate = entry.datos.find(d => d.casa === selectedCurrencyType);
    if (rate) {
        labels.push(date.toLocaleString());
    data.push(rate.venta);
                }
            });

    const ctx = document.getElementById('exchangeRateChart').getContext('2d');
    if (exchangeRateChart) {
        exchangeRateChart.destroy();
            }

    exchangeRateChart = new Chart(ctx, {
        type: 'line',
    data: {
        labels: labels,
    datasets: [{
        label: `USD ${selectedCurrencyType.charAt(0).toUpperCase() + selectedCurrencyType.slice(1)}`,
    data: data,
    borderColor: 'rgba(75, 192, 192, 1)',
    backgroundColor: 'rgba(75, 192, 192, 0.2)',
    borderWidth: 1
                    }]
                },
    options: {
        scales: {
        x: {
        type: 'time',
    time: {
        unit: 'day'
                            }
                        },
    y: {
        beginAtZero: false
                        }
                    }
                }
            });
        }

</script>