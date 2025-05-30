const tipos = [
    { id: "oficial", nombre: "oficial" },
    { id: "blue", nombre: "blue" },
    { id: "bolsa", nombre: "bolsa" },
    { id: "contadoconliqui", nombre: "contadoconliqui" },
    { id: "tarjeta", nombre: "tarjeta" },
    { id: "mayorista", nombre: "mayorista" },
    { id: "cripto", nombre: "cripto" },
];

function tiempoRelativo(fecha) {
    const ahora = new Date();
    const diffMs = ahora - fecha;
    const diffMin = Math.floor(diffMs / 60000);
    const diffHs = Math.floor(diffMin / 60);

    if (diffMin < 1) return "< 1 minuto";
    if (diffMin < 60) return `Hace ${diffMin} minuto${diffMin === 1 ? "" : "s"}`;
    return `Hace ${diffHs} hora${diffHs === 1 ? "" : "s"}`;
}

function actualizarCotizaciones() {
    fetch("cotizaciones.php")
        .then(res => res.json())
        .then(data => {
            tipos.forEach(({ id, nombre }) => {
                const cotizacion = data.find(item => item.casa === nombre);
                if (!cotizacion) return;

                const compraEl = document.getElementById(`${id}-c`);
                const ventaEl = document.getElementById(`${id}-v`);
                const tiempoEl = document.getElementById(`${id}-t`);

                if (compraEl) {
                    compraEl.textContent = `$${cotizacion.compra.toLocaleString("es-AR", {
                        minimumFractionDigits: 1,
                        maximumFractionDigits: 1,
                    })}`;
                    compraEl.classList.remove("highlight");
                    void compraEl.offsetWidth;
                    compraEl.classList.add("highlight");
                }

                if (ventaEl) {
                    ventaEl.textContent = `$${cotizacion.venta.toLocaleString("es-AR", {
                        minimumFractionDigits: 1,
                        maximumFractionDigits: 1,
                    })}`;
                    ventaEl.classList.remove("highlight");
                    void ventaEl.offsetWidth;
                    ventaEl.classList.add("highlight");
                }

                if (tiempoEl) {
                    const fecha = new Date(cotizacion.fechaActualizacion);
                    tiempoEl.dataset.timestamp = fecha.getTime();
                    tiempoEl.textContent = tiempoRelativo(fecha);
                }
            });
        })
        .catch(err => {
            console.error("Error al obtener las cotizaciones:", err);
        });
}

// ⏱ Actualizar los textos de tiempo periódicamente
setInterval(() => {
    const tiempoEls = document.querySelectorAll("[id$='-t']");
    tiempoEls.forEach(el => {
        const timestamp = el.dataset.timestamp;
        if (timestamp) {
            const fecha = new Date(parseInt(timestamp));
            el.textContent = tiempoRelativo(fecha);
        }
    });
}, 60000);

// Ejecutar al cargar la página
actualizarCotizaciones();