window.qmStorage = {
    save: function (key, value) {
        try {
            window.localStorage.setItem(key, value);
            return true;
        } catch (e) {
            console.warn("qmStorage.save failed:", e);
            return false;
        }
    },
    load: function (key) {
        try {
            return window.localStorage.getItem(key);
        } catch (e) {
            console.warn("qmStorage.load failed:", e);
            return null;
        }
    },
    clear: function (key) {
        try {
            window.localStorage.removeItem(key);
            return true;
        } catch (e) {
            console.warn("qmStorage.clear failed:", e);
            return false;
        }
    },
    listKeys: function (prefix) {
        try {
            const result = [];
            for (let i = 0; i < window.localStorage.length; i++) {
                const key = window.localStorage.key(i);
                if (key && key.startsWith(prefix)) {
                    result.push(key);
                }
            }
            return result;
        } catch (e) {
            console.warn("qmStorage.listKeys failed:", e);
            return [];
        }
    }
};
