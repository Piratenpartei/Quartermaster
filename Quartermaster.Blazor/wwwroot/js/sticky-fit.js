function StickyFitToViewport(element, bottomPaddingPx) {
    if (!element) {
        return;
    }
    const padding = bottomPaddingPx ?? 16;
    const update = () => {
        const top = element.getBoundingClientRect().top;
        const available = window.innerHeight - top - padding;
        element.style.maxHeight = available + "px";
    };
    update();
    window.addEventListener("resize", update);
    window.addEventListener("scroll", update, { passive: true });
}
