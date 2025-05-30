function openModal() {
    const iframe = document.getElementById('youtube-frame');
    iframe.src = "https://www.youtube.com/embed/dQw4w9WgXcQ?autoplay=1&rel=0";
    document.getElementById('modal-backdrop').style.display = 'flex';
    startEmojiRain();
}

function closeModal() {
    const modal = document.getElementById('modal-backdrop');
    modal.style.display = 'none';
    document.getElementById('youtube-frame').src = "";
    stopEmojiRain();
}

// Cerrar modal al hacer clic fuera de la caja modal
document.getElementById('modal-backdrop').addEventListener('click', (e) => {
    // Solo cerrar si se hizo clic directamente sobre el fondo, no sobre el contenido
    if (e.target.id === 'modal-backdrop') {
        closeModal();
    }
});

// Cerrar modal al presionar Escape
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        closeModal();
    }
});

// Lista de emojis a usar
const emojiList = ['💸', '🪙', '💰', '🤑', '💵', '🪅', '🪄'];

// Configuraciones
const maxEmojis = 30; // Cantidad máxima de emojis simultáneos
const emojiLifetime = 10000; // Duración de cada emoji en milisegundos
const emojiFallDuration = 8000; // Tiempo que tarda en caer, en ms

let emojiInterval;
let emojiCount = 0;

function startEmojiRain() {
    const container = document.getElementById('emoji-container');

    emojiInterval = setInterval(() => {
        if (emojiCount >= maxEmojis) return;

        const emoji = document.createElement('div');
        emoji.textContent = emojiList[Math.floor(Math.random() * emojiList.length)];
        emoji.style.position = 'absolute';
        emoji.style.fontSize = `${Math.random() * 24 + 24}px`;
        emoji.style.left = `${Math.random() * 100}%`;
        emoji.style.top = '-40px';
        emoji.style.opacity = 0.9;
        emoji.style.transition = `transform ${emojiFallDuration}ms linear`;
        emoji.style.transform = 'translateY(0)';

        container.appendChild(emoji);
        emojiCount++;

        // Trigger the animation after appending
        requestAnimationFrame(() => {
            emoji.style.transform = `translateY(100vh)`;
        });

        // Eliminar después del tiempo de vida
        setTimeout(() => {
            emoji.remove();
            emojiCount--;
        }, emojiLifetime);
    }, 300); // Frecuencia con que caen los emojis (ajustable)
}

function stopEmojiRain() {
    clearInterval(emojiInterval);
    emojiInterval = null;
    emojiCount = 0;
    document.getElementById('emoji-container').innerHTML = '';
}