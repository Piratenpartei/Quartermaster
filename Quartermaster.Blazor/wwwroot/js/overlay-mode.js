// Runs in <head> before the page renders, so OBS never shows the loading indicator or a page background behind the overlay.
if (/^\/assembly\/[^/]+\/overlay\/?$/i.test(window.location.pathname))
    document.documentElement.classList.add('assembly-overlay-mode');
