window.languageStorage = {
    getLanguage: function() { return localStorage.getItem('qm_language'); },
    setLanguage: function(lang) { localStorage.setItem('qm_language', lang); },
    detectBrowser: function() {
        var navLang = (navigator.language || navigator.userLanguage || 'de').toLowerCase();
        return navLang.startsWith('de') ? 'de' : 'en';
    }
};
