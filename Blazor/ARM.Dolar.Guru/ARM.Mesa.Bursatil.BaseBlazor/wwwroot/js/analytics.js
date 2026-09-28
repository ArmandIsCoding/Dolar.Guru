(() => {
    "use strict";

    // Keep development, IP-address access and preview hosts out of the live property.
    if (!["mesabursatil.ar", "www.mesabursatil.ar"].includes(window.location.hostname)) return;

    const measurementId = document.currentScript?.dataset.measurementId;
    if (!/^G-[A-Z0-9]+$/.test(measurementId || "") || window.mesaAnalyticsInitialized) return;
    window.mesaAnalyticsInitialized = true;

    window.dataLayer = window.dataLayer || [];
    window.gtag = window.gtag || function () { window.dataLayer.push(arguments); };
    window.gtag("js", new Date());
    // GA4 enhanced measurement owns history-based page views: do not send a
    // second manual page_view from Blazor's enhancedload or render callbacks.
    window.gtag("config", measurementId, {
        allow_google_signals: false,
        allow_ad_personalization_signals: false
    });

    const script = document.createElement("script");
    script.async = true;
    script.src = "https://www.googletagmanager.com/gtag/js?id=" + encodeURIComponent(measurementId);
    document.head.appendChild(script);
})();
