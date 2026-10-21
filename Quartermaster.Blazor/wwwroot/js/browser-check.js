// Starts Blazor only where the .NET runtime can run: it needs WebAssembly SIMD, which browsers leave out on processors
// without SSE4.1 (roughly 2010 and older). Elsewhere the runtime would fail on an assert and the loading spinner would never end.
(function () {
    const simdModule = new Uint8Array([0, 97, 115, 109, 1, 0, 0, 0, 1, 5, 1, 96, 0, 1, 123, 3, 2, 1, 0, 10, 10, 1, 8, 0, 65, 0, 253, 15, 253, 98, 11]);

    const texts = {
        de: {
            title: "Dieses Gerät wird nicht unterstützt",
            body: "Quartermaster braucht eine Browser-Funktion (WebAssembly SIMD), die hier nicht verfügbar ist. Bitte aktualisiere deinen Browser. Ist er schon aktuell, ist der Prozessor dieses Geräts zu alt; dann nutze bitte ein anderes Gerät."
        },
        en: {
            title: "This device is not supported",
            body: "Quartermaster needs a browser feature (WebAssembly SIMD) that is not available here. Please update your browser. If it is already up to date, this device's processor is too old; please use another device."
        }
    };

    function supportsWasmSimd() {
        try {
            return typeof WebAssembly === "object" && WebAssembly.validate(simdModule);
        } catch (e) {
            return false;
        }
    }

    function language() {
        let stored = null;
        try {
            stored = localStorage.getItem("qm_language");
        } catch (e) {
            stored = null;
        }
        if (stored === "de" || stored === "en") {
            return stored;
        }
        return (navigator.language || "de").toLowerCase().startsWith("de") ? "de" : "en";
    }

    function showUnsupported() {
        const text = texts[language()];
        const box = document.createElement("div");
        box.className = "container py-5";
        const title = document.createElement("h1");
        title.className = "h3";
        title.textContent = text.title;
        const body = document.createElement("p");
        body.textContent = text.body;
        box.append(title, body);
        document.getElementById("app").replaceChildren(box);
    }

    if (!supportsWasmSimd()) {
        showUnsupported();
        return;
    }

    Blazor.start();
})();
