<script>
    const container = document.getElementById('carousel');
    const track = document.getElementById('track');

    // Copia las tarjetas para efecto infinito
    track.innerHTML += track.innerHTML;

    let position = 0;
    let speed = 0.5;
    let paused = false;

    function animate() {
            if (!paused) {
        position -= speed;
                if (Math.abs(position) >= track.scrollWidth / 2) {
        position = 0;
                }
    track.style.transform = `translateX(${position}px)`;
            }
    requestAnimationFrame(animate);
        }

        container.addEventListener('mouseenter', () => paused = true);
        container.addEventListener('mouseleave', () => paused = false);

    // Drag manual
    let isDragging = false;
    let startX;
    let dragStartPos;

        container.addEventListener('mousedown', (e) => {
        isDragging = true;
    startX = e.clientX;
    dragStartPos = position;
    container.classList.add('dragging');
        });

        window.addEventListener('mouseup', () => {
        isDragging = false;
    container.classList.remove('dragging');
        });

        window.addEventListener('mousemove', (e) => {
            if (!isDragging) return;
    const dx = e.clientX - startX;
    position = dragStartPos + dx;
    track.style.transform = `translateX(${position}px)`; // ACTUALIZA EN VIVO
        });

        // --- SOPORTE PARA TOUCH ---
        container.addEventListener('touchstart', (e) => {
        isDragging = true;
    startX = e.touches[0].clientX;
    dragStartPos = position;
    container.classList.add('dragging');
        }, {passive: true });

        container.addEventListener('touchmove', (e) => {
            if (!isDragging) return;
    const dx = e.touches[0].clientX - startX;
    position = dragStartPos + dx;
    track.style.transform = `translateX(${position}px)`;
        }, {passive: true });

        container.addEventListener('touchend', () => {
        isDragging = false;
    container.classList.remove('dragging');
        });
    animate();
</script>