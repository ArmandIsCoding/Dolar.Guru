<html lang="es-AR">

<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Cotizaciones | Dolar Guru</title>
    <script src="scripts/tailwind.js"></script>
    <link rel="icon" href="data:image/svg+xml,<svg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 100 100%22><text y=%22.9em%22 font-size=%2290%22>💸</text></svg>">
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css">
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
    <script src="" crossorigin="anonymous"></script>
    <link rel="stylesheet" href="styles.css">
</head>

<!-- <style>
.carousel-container {
    overflow: hidden;
    position: relative;
    width: 100%;
}

.carousel-track {
    display: flex;
    width: max-content;
    animation: scroll 20s linear infinite;
}

.card {
    width: 200px;
    padding: 20px;
    margin: 0 10px;
    background-color: #f4f4f4;
    text-align: center;
    border-radius: 10px;
    flex-shrink: 0;
}

@keyframes scroll {
    0% {
        transform: translateX(0);
    }
    100% {
        transform: translateX(-50%);
    }
}

</style> -->

<!-- <script>
document.addEventListener("DOMContentLoaded", () => {
    const container = document.getElementById("carousel");
    const track = document.getElementById("track");

    container.addEventListener("mouseenter", () => {
        track.style.animationPlayState = "paused";
    });

    container.addEventListener("mouseleave", () => {
        track.style.animationPlayState = "running";
    });
});
</script> -->


<body class="bg-gray-50" style="
    background-color: #e4eaf1;
    background-image: repeating-linear-gradient(45deg,rgba(28, 58, 92,0.05) 0,rgba(28, 100, 92,0.05) 1px,transparent 50px,transparent 20px);
    background-attachment: fixed">

    <!-- Left Sidebar Ad -->
    <div class="ad-sidebar left">
        <div class="ad-placeholder h-64 w-full rounded-lg mb-4">
            Ads Placeholder
        </div>
        <div class="ad-placeholder h-64 w-full rounded-lg">
            Ads Placeholder
        </div>
    </div>

    <!-- Right Sidebar Ad -->
    <div class="ad-sidebar right">
        <div class="ad-placeholder h-64 w-full rounded-lg mb-4">
            Ads Placeholder
        </div>
        <div class="ad-placeholder h-64 w-full rounded-lg">
            Ads Placeholder
        </div>
    </div>

    <div class="transform translate-y-[37px]">
        <!-- Contenedor para toda la página -->
        <!-- Carousel Cards con ids sincronizados -->
        <!-- <div class="carousel-track" id="track">
            <div class="card">💰 Dólar<br><span id="oficial-v2" class="value">...</span><br></div>
            <div class="card">🪙 Dólar Blue<br><span id="blue-v2" class="value">...</span><br></div>
            <div class="card">📈 Dólar Bolsa<br><span id="bolsa-v2" class="value">...</span><br></div>
            <div class="card">🏦 CCL<br><span id="contadoconliqui-v2" class="value">...</span><br></div>
            <div class="card">💳 Tarjeta<br><span id="tarjeta-v2" class="value">...</span><br></div>
            <div class="card">🏭 Mayorista<br><span id="mayorista-v2" class="value">...</span><br></div>
            <div class="card">🔒 Cripto<br><span id="cripto-v2" class="value">...</span><br></div>
        </div> -->
        

        <div class="max-w-5xl mx-auto px-4">
            <!-- Contenedor para el resto de la página -->
            <div class="container mx-auto px-4 py-8 ">

                <!-- Horizontal Ad Banner -->
                <div class="ad-placeholder w-full h-32 rounded-lg mb-8 flex items-center justify-center">
                    Ads Placeholder
                </div>

                <div class="mb-10">
                    <div class="bg-white rounded-xl flex flex-col md:flex-row items-center justify-center gap-10 p-8">
                        <!--Imagen del gurise -->
                        <div class="guru-image flex flex-col items-center transition duration-300 ease-in-out">
                            <div class="relative w-72 h-72 rounded-lg overflow-hidden shadow-xl">
                                <img src="https://dolar.guru/dg-resources/dolar-guru.png" alt="El Gurú del Dólar" class="object-cover w-full h-full object-[center_-50px]">
                            </div>
                        </div>
                        <!--Texto -->
                        <div class="flex flex-col max-w-xl text-lg ">
                            <h1 class="text-[2rem] font-bold text-green-800" style="margin-bottom: 12px;"><i>🧙🏼‍♂️⛈️⚡ El Gurú del dólar 💸💸</i></h1>
                            <p class="text-gray-600 mb-4">
                                Tené esta página siempre a mano, el Mago de los dólares basado en IA te mantendrá siempre la cotización más fresca posible.
                            </p>
                            <p class="text-gray-600 mb-4">
                                La diferencia entre Dolar.Gurú y otras páginas es que nuestro Mago basado en IA (ver imagen adjunta, del mago) está siempre atento a las fluctuaciones del mercado y actualiza las cotizaciones en tiempo real, para que puedas tomar decisiones informadas al instante.
                            </p>
                            <!-- <p class="text-gray-600 mb-4">
                                Además, si te gusta el sitio, ¡no dudes en compartirlo con tus amigos y familiares! Cuantos más seamos, mejor será la comunidad del Gurú del Dólar.
                            </p> -->
                            <p style="text-align: right;">
                                <!-- Boton para compartir por whatsapp -->
                                <a href="https://api.whatsapp.com/send?text=¡Hola! Te comparto esta página que me ayuda a seguir las cotizaciones del dólar en tiempo real y además trae suerte monetaria: https://dolar.guru" target="_blank" style="float: right;">
                                    <img src="dg-resources/c_whatsapp.png" alt="WhatsApp" style="max-height: 80px; vertical-align: middle;">
                                </a>
                            </p>
                        </div>
                        <script>
                        function addToFavorites(e) {
                            e.preventDefault();
                            var url = window.location.href;
                            var title = document.title;
                            if (window.sidebar && window.sidebar.addPanel) {
                                // Firefox <=22
                                window.sidebar.addPanel(title, url, "");
                            } else if (window.external && ('AddFavorite' in window.external)) {
                                // IE
                                window.external.AddFavorite(url, title);
                            } else {
                                // Other browsers (Chrome, Firefox 23+, Edge, etc.)
                                alert('Presioná Ctrl+D (o Cmd+D en Mac) para agregar esta página a tus favoritos.');
                            }
                        }
                        </script>
                    </div>
                </div>

                <!-- /////////////////////////////////////////ZONA PRINCIPAL///////////////////////////////////////// -->
                <!-- Información -->
                <div class="bg-gray-100 rounded-[0.2rem] shadow-md p-6 mb-4 relative" style="z-index:2;">
                    <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-2 mb-6">
                        <!-- Currency Cards -->
                        <!-- DÓLAR OFICIAL -->
                        <div class="currency-card bg-white rounded-[0.2rem] shadow-md p-6 transition duration-300 ease-in-out">
                            <div class="flex items-center mb-4">
                                <div class="rounded-xl bg-green-100 flex items-center justify-center mr-2 p-1">
                                    <i class="text-green-500 fas fa-caret-down mr-1"></i>
                                    <span class="text-green-700">0,12%</span>
                                </div>
                                <div>
                                    <p class="text-[1.1rem] md:text-[1.0rem] lg:text-[1.1rem] font-semibold">Dólar Oficial</p>
                                    <p class="text-[0.7rem] text-gray-500" id="oficial-t">...</p>
                                </div>
                            </div>
                            <div class="flex justify-between items-end">
                                <div>
                                    <p class="text-gray-500 text-sm">Compra</p>
                                    <p class="text-[1.2rem] font-bold" id="oficial-c">...</p>
                                </div>
                                <div>
                                    <p class="text-gray-500 text-sm">Venta</p>
                                    <p class="text-[1.2rem] font-bold" id="oficial-v">...</p>
                                </div>
                            </div>
                        </div>
                        <!-- DÓLAR BLUE -->
                        <div class="currency-card bg-white rounded-[0.2rem] shadow-md p-6 transition duration-300 ease-in-out">
                            <div class="flex items-center mb-4">
                                <div class="rounded-xl bg-red-100 flex items-center justify-center mr-2 p-1">
                                    <i class="text-red-500 fas fa-caret-up mr-1"></i>
                                    <span class="text-red-700">0,17%</span>
                                </div>
                                <div>
                                    <p class="text-[1.1rem] md:text-[1.0rem] lg:text-[1.1rem] font-semibold">Dólar Blue</p>
                                    <p class="text-[0.7rem] text-gray-500" id="blue-t">...</p>
                                </div>
                            </div>
                            <div class="flex justify-between items-end">
                                <div>
                                    <p class="text-gray-500 text-sm">Compra</p>
                                    <p class="text-[1.2rem] font-bold" id="blue-c">...</p>
                                </div>
                                <div>
                                    <p class="text-gray-500 text-sm">Venta</p>
                                    <p class="text-[1.2rem] font-bold" id="blue-v">...</p>
                                </div>
                            </div>
                        </div>
                        <!-- DÓLAR BOLSA -->
                        <div class="currency-card bg-white rounded-[0.2rem] shadow-md p-6 transition duration-300 ease-in-out">
                            <div class="flex items-center mb-4">
                                <div class="rounded-xl bg-green-100 flex items-center justify-center mr-2 p-1">
                                    <i class="text-green-500 fas fa-caret-down mr-1"></i>
                                    <span class="text-green-700">0,12%</span>
                                </div>
                                <div>
                                    <p class="text-[1.1rem] md:text-[1.0rem] lg:text-[1.1rem] font-semibold">Dólar Bolsa</p>
                                    <p class="text-[0.7rem] text-gray-500" id="bolsa-t">...</p>
                                </div>
                            </div>
                            <div class="flex justify-between items-end">
                                <div>
                                    <p class="text-gray-500 text-sm">Compra</p>
                                    <p class="text-[1.2rem] font-bold" id="bolsa-c">...</p>
                                </div>
                                <div>
                                    <p class="text-gray-500 text-sm">Venta</p>
                                    <p class="text-[1.2rem] font-bold" id="bolsa-v">...</p>
                                </div>
                            </div>
                        </div>
                        <!-- DÓLAR CONTADO CON LIQUIDACIÓN -->
                        <div class="currency-card bg-white rounded-[0.2rem] shadow-md p-6 transition duration-300 ease-in-out">
                            <div class="flex items-center mb-4">
                                <div class="rounded-xl bg-green-100 flex items-center justify-center mr-2 p-1">
                                    <i class="text-green-500 fas fa-caret-down mr-1"></i>
                                    <span class="text-green-700">0,12%</span>
                                </div>
                                <div>
                                    <p class="text-[1.1rem] md:text-[1.0rem] lg:text-[1.1rem] font-semibold">Dólar CCL</p>
                                    <p class="text-[0.7rem] text-gray-500" id="contadoconliqui-t">...</p>
                                </div>
                            </div>
                            <div class="flex justify-between items-end">
                                <div>
                                    <p class="text-gray-500 text-sm">Compra</p>
                                    <p class="text-[1.2rem] font-bold" id="contadoconliqui-c">...</p>
                                </div>
                                <div>
                                    <p class="text-gray-500 text-sm">Venta</p>
                                    <p class="text-[1.2rem] font-bold" id="contadoconliqui-v">...</p>
                                </div>
                            </div>
                        </div>
                        <!-- DÓLAR TARJETA -->
                        <div class="currency-card bg-white rounded-[0.2rem] shadow-md p-6 transition duration-300 ease-in-out">
                            <div class="flex items-center mb-4">
                                <div class="rounded-xl bg-green-100 flex items-center justify-center mr-2 p-1">
                                    <i class="text-green-500 fas fa-caret-down mr-1"></i>
                                    <span class="text-green-700">0,12%</span>
                                </div>
                                <div>
                                    <p class="text-[1.1rem] md:text-[1.0rem] lg:text-[1.1rem] font-semibold">Dólar Tarjeta</p>
                                    <p class="text-[0.7rem] text-gray-500" id="tarjeta-t">...</p>
                                </div>
                            </div>
                            <div class="flex justify-between items-end">
                                <div>
                                    <p class="text-gray-500 text-sm">Compra</p>
                                    <p class="text-[1.2rem] font-bold" id="tarjeta-c">...</p>
                                </div>
                                <div>
                                    <p class="text-gray-500 text-sm">Venta</p>
                                    <p class="text-[1.2rem] font-bold" id="tarjeta-v">...</p>
                                </div>
                            </div>
                        </div>
                        <!-- DÓLAR MAYORISTA -->
                        <div class="currency-card bg-white rounded-[0.2rem] shadow-md p-6 transition duration-300 ease-in-out">
                            <div class="flex items-center mb-4">
                                <div class="rounded-xl bg-green-100 flex items-center justify-center mr-2 p-1">
                                    <i class="text-green-500 fas fa-caret-down mr-1"></i>
                                    <span class="text-green-700">0,12%</span>
                                </div>
                                <div>
                                    <p class="text-[1.1rem] md:text-[1.0rem] lg:text-[1.1rem] font-semibold">Dólar Mayorista</p>
                                    <p class="text-[0.7rem] text-gray-500" id="mayorista-t">...</p>
                                </div>
                            </div>
                            <div class="flex justify-between items-end">
                                <div>
                                    <p class="text-gray-500 text-sm">Compra</p>
                                    <p class="text-[1.2rem] font-bold" id="mayorista-c">...</p>
                                </div>
                                <div>
                                    <p class="text-gray-500 text-sm">Venta</p>
                                    <p class="text-[1.2rem] font-bold" id="mayorista-v">...</p>
                                </div>
                            </div>
                        </div>
                        <!-- DÓLAR CRIPTO -->
                        <div class="currency-card bg-white rounded-[0.2rem] shadow-md p-6 transition duration-300 ease-in-out">
                            <div class="flex items-center mb-4">
                                <div class="rounded-xl bg-green-100 flex items-center justify-center mr-2 p-1">
                                    <i class="text-green-500 fas fa-caret-down mr-1"></i>
                                    <span class="text-green-700">0,12%</span>
                                </div>
                                <div>
                                    <p class="text-[1.1rem] md:text-[1.0rem] lg:text-[1.1rem] font-semibold">Dólar Cripto</p>
                                    <p class="text-[0.7rem] text-gray-500" id="cripto-t">...</p>
                                </div>
                            </div>
                            <div class="flex justify-between items-end">
                                <div>
                                    <p class="text-gray-500 text-sm">Compra</p>
                                    <p class="text-[1.2rem] font-bold" id="cripto-c">...</p>
                                </div>
                                <div>
                                    <p class="text-gray-500 text-sm">Venta</p>
                                    <p class="text-[1.2rem] font-bold" id="cripto-v">...</p>
                                </div>
                            </div>
                        </div>
                        <!-- DÓLAR FUTURO -->
                        <div class="currency-card bg-white rounded-[0.2rem] shadow-md p-6 transition duration-300 ease-in-out">
                            <div class="flex items-center mb-4">
                                <div class="rounded-xl bg-green-100 flex items-center justify-center mr-2 p-1">
                                    <i class="text-green-500 fas fa-caret-down mr-1"></i>
                                    <span class="text-green-700">0,12%</span>
                                </div>
                                <div>
                                    <p class="text-[1.1rem] md:text-[1.0rem] lg:text-[1.1rem] font-semibold">Dólar Futuro</p>
                                    <p class="text-[0.7rem] text-gray-500" id="futuro-t">...</p>
                                </div>
                            </div>
                            <div class="flex justify-between items-end">
                                <div>
                                    <p class="text-gray-500 text-sm">Compra</p>
                                    <p class="text-[1.2rem] font-bold" id="futuro-c">...</p>
                                </div>
                                <div>
                                    <p class="text-gray-500 text-sm">Venta</p>
                                    <p class="text-[1.2rem] font-bold" id="futuro-v">...</p>
                                </div>
                            </div>
                        </div>

                    </div>

                    <!-- Barrita de carga -->
                    <div class="w-full max-w-xl mb-4 mx-auto">
                        <div class="w-full h-4 bg-gray-700 rounded overflow-hidden relative">
                            <div id="progress-bar" class="h-full animate-stripes" style="width: 0%;background-color: rgba(255,255,255,0.2);background-image: repeating-linear-gradient(45deg,rgba(255,255,255,0.3) 0,rgba(255,255,255,0.3) 10px,transparent 10px,transparent 20px);background-size: 28.28px 28.28px; /* 20px / sin(45°) to match the 45deg angle */background-repeat: repeat;animation: stripes 1s linear infinite;transition: width 10s linear;"></div>
                        </div>
                        <p class="text-center text-gray-300">Próxima actualización en unos segundos</p>
                    </div>
                </div>




                <!-- ///////////////////////////////////////////////////////////////////////////////////// -->
                <div class="bg-green-900 rounded-xl shadow-md p-6 mb-8" style="background-image: repeating-linear-gradient(45deg,rgba(255,255,255,0.05) 0,rgba(255,255,255,0.05) 1px,transparent 3px,transparent 10px);">
                    <div class="p-4 bg-green-600 rounded-lg shadow-xl">
                        <div class="flex justify-between items-center">
                            <div>
                                <p class="text-sm text-white">Visitante número 1.000:</p>
                                <p class="text-2xl font-bold">CLICK PARA GANAR 1.000 USD!!! 🤑 -----------> </p>
                            </div>
                            <!--<a href="https://www.youtube.com/watch?v=dQw4w9WgXcQ" target="_blank"
                            rel="noopener noreferrer">-->
                            <button onclick="openModal()" class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition duration-300">
                                Éxito 💸
                            </button>
                            <!--</a>-->
                        </div>
                    </div>
                </div>

                <!-- MODAL -->
                <div class="modal-backdrop" id="modal-backdrop">
                    <div class="modal" style="background-color: rgba(20, 70, 50, 1); background-image: url('data:image /svg+xml;utf8,<svg xmlns=%22http://www.w3.org/2000/svg%22 width=%2232%22 height=%2232%22><text y=%2224%22 font-size=%2224%22>💵</text></svg>');background-repeat: repeat;background-size: 40px 40px;">
                        <button class="modal-close" onclick="closeModal()">×</button>
                        <iframe id="youtube-frame" src="" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture" allowfullscreen></iframe>
                    </div>
                </div>

                <div id="emoji-container" style="position: fixed; top: 0; left: 0; width: 100%; height: 100%; pointer-events: none; z-index: 9999;"></div>

                <!-- ///////////////////////////////////////////////////////////////////////////////////// -->
                <!-- Horizontal Ad Banner -->
                <div class="ad-placeholder w-full h-32 rounded-lg mb-8 flex items-center justify-center">
                    Ads Placeholder
                </div>

                <!-- News Section -->
                <!-- <div class="bg-white rounded-xl shadow-md p-6">
                    <div class="flex justify-between items-center mb-6">
                        <h2 class="text-2xl font-semibold text-gray-800">La posta</h2>
                    </div>

                    <iframe src="https://dolar.guru/blog" width="100%" height="315" frameborder="0" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture" allowfullscreen></iframe>

                    <div class="mt-8 text-center">
                        <a href="https://dolar.guru/blog" class="px-6 py-2 border border-blue-600 text-blue-600 rounded-lg hover:bg-blue-50 transition duration-300">
                            Ver todo
                        </a>
                    </div>
                </div> -->
            </div>
        </div>
    </div>

    <script src="scripts/carousel.js"></script>
    <script src="scripts/rickroll.js"></script>
    <script src="scripts/cotizaciones.js"></script>
    <script src="scripts/loadingbar.js"></script>

</body>

</html>