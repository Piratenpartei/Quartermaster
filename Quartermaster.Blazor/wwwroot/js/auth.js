window.authStorage = {
    getToken: function() { return localStorage.getItem('auth_token'); },
    setToken: function(token) { localStorage.setItem('auth_token', token); },
    removeToken: function() { localStorage.removeItem('auth_token'); },
    getReturnUrl: function() { return localStorage.getItem('return_url'); },
    setReturnUrl: function(url) { localStorage.setItem('return_url', url); },
    removeReturnUrl: function() { localStorage.removeItem('return_url'); }
};
