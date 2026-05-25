// Auth tokens themselves live in an HttpOnly cookie set server-side. This file only
// persists the "return to this page after login" URL — non-sensitive UX state.
window.authStorage = {
    getReturnUrl: function() { return localStorage.getItem('return_url'); },
    setReturnUrl: function(url) { localStorage.setItem('return_url', url); },
    removeReturnUrl: function() { localStorage.removeItem('return_url'); }
};
