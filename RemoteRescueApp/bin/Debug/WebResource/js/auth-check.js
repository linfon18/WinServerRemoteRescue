(function() {
    const AUTH_KEY = 'remote_rescue_auth';
    const AUTH_TIMESTAMP = 'remote_rescue_auth_time';
    const SESSION_TIMEOUT = 30 * 60 * 1000;

    window.AuthCheck = {
        isAuthenticated: function() {
            const auth = sessionStorage.getItem(AUTH_KEY);
            const timestamp = sessionStorage.getItem(AUTH_TIMESTAMP);
            
            if (!auth || auth !== 'true') {
                return false;
            }
            
            if (timestamp) {
                const elapsed = Date.now() - parseInt(timestamp);
                if (elapsed > SESSION_TIMEOUT) {
                    this.logout();
                    return false;
                }
            }
            
            sessionStorage.setItem(AUTH_TIMESTAMP, Date.now().toString());
            return true;
        },

        login: function() {
            sessionStorage.setItem(AUTH_KEY, 'true');
            sessionStorage.setItem(AUTH_TIMESTAMP, Date.now().toString());
        },

        logout: function() {
            sessionStorage.removeItem(AUTH_KEY);
            sessionStorage.removeItem(AUTH_TIMESTAMP);
            window.location.href = 'index.html';
        },

        checkAuth: function() {
            if (!this.isAuthenticated()) {
                window.location.replace('index.html');
                return false;
            }
            return true;
        }
    };
})();
