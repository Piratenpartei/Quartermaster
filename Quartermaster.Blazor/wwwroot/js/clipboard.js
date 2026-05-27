async function CopyToClipboard(text) {
    if (navigator.clipboard && window.isSecureContext) {
        await navigator.clipboard.writeText(text);
        return true;
    }
    const ta = document.createElement("textarea");
    ta.value = text;
    ta.style.position = "fixed";
    ta.style.opacity = "0";
    document.body.appendChild(ta);
    ta.focus();
    ta.select();
    let ok = false;
    try {
        ok = document.execCommand("copy");
    } catch {
        ok = false;
    }
    document.body.removeChild(ta);
    return ok;
}
