function CanShare() {
    return typeof navigator.share === "function";
}

async function ShareLink(title, url) {
    try {
        await navigator.share({ title: title, url: url });
    } catch (e) {
        if (e.name !== "AbortError") {
            throw e;
        }
    }
}
