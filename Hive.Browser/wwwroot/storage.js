window.HiveStorage = {
    save: function(key, value) {
        try {
            localStorage.setItem(key, value);
            return true;
        } catch (e) {
            return false;
        }
    },
    
    load: function(key) {
        try {
            return localStorage.getItem(key);
        } catch (e) {
            return null;
        }
    },
    
    exists: function(key) {
        try {
            return localStorage.getItem(key) !== null;
        } catch (e) {
            return false;
        }
    },
    
    remove: function(key) {
        try {
            localStorage.removeItem(key);
            return true;
        } catch (e) {
            return false;
        }
    }
};

globalThis.HiveStorage = window.HiveStorage;
