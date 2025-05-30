const progressBar = document.getElementById("progress-bar");
const duration = 10000; // 10.000 (10 segundos)

function startProgressBar() {
    // Reiniciar barra
    progressBar.style.transition = "none";
    progressBar.style.width = "0%";

    // Forzar reflow para reiniciar animación
    void progressBar.offsetWidth;

    // Aplicar animación
    progressBar.style.transition = `width ${duration}ms linear`;
    progressBar.style.width = "100%";
}

// Iniciar por primera vez
startProgressBar();

//Reiniciar barrita
setInterval(() => {
    startProgressBar();
    actualizarCotizaciones();
}, duration
);