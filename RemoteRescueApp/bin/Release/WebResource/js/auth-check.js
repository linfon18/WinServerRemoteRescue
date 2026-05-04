(function() {
    const AUTH_KEY = 'remote_rescue_auth';
    const AUTH_TIMESTAMP = 'remote_rescue_auth_time';
    const SESSION_TIMEOUT = 30 * 60 * 1000;
    const TOKEN_PARAM = '_token';
    const DEBUG = true;

    function log(msg) {
        if (DEBUG) console.log('[AuthCheck] ' + msg);
    }

    // 生成简单token
    function generateToken() {
        var token = Date.now().toString(36) + Math.random().toString(36).substr(2);
        log('生成Token: ' + token);
        return token;
    }

    // 获取URL参数
    function getUrlParam(name) {
        var url = window.location.href;
        name = name.replace(/[\[\]]/g, '\\$&');
        var regex = new RegExp('[?&]' + name + '(=([^&#]*)|&|#|$)');
        var results = regex.exec(url);
        if (!results) return null;
        if (!results[2]) return '';
        return decodeURIComponent(results[2].replace(/\+/g, ' '));
    }

    // 设置存储（localStorage + sessionStorage 双重保障）
    function setStorage(name, value) {
        log('设置存储: ' + name + ' = ' + value);
        try {
            localStorage.setItem(name, value);
            log('localStorage设置成功');
        } catch(e) {
            log('localStorage设置失败: ' + e.message);
        }
        try {
            sessionStorage.setItem(name, value);
            log('sessionStorage设置成功');
        } catch(e) {
            log('sessionStorage设置失败: ' + e.message);
        }
    }

    function getStorage(name) {
        var val = null;
        try {
            val = localStorage.getItem(name);
            if (val) log('从localStorage获取: ' + name + ' = ' + val);
        } catch(e) {}
        if (!val) {
            try {
                val = sessionStorage.getItem(name);
                if (val) log('从sessionStorage获取: ' + name + ' = ' + val);
            } catch(e) {}
        }
        return val;
    }

    function deleteStorage(name) {
        log('删除存储: ' + name);
        try {
            localStorage.removeItem(name);
        } catch(e) {}
        try {
            sessionStorage.removeItem(name);
        } catch(e) {}
    }

    window.AuthCheck = {
        // 生成登录token（用于URL传递）
        generateAuthToken: function() {
            var token = generateToken();
            setStorage('auth_token', token);
            setStorage(AUTH_TIMESTAMP, Date.now().toString());
            return token;
        },

        isAuthenticated: function() {
            log('=== 开始认证检查 ===');
            log('当前URL: ' + window.location.href);

            // 先检查URL参数（FRP场景）
            var urlToken = getUrlParam(TOKEN_PARAM);
            log('URL Token: ' + urlToken);

            if (urlToken) {
                var storedToken = getStorage('auth_token');
                log('存储的Token: ' + storedToken);
                log('比较: URL[' + urlToken + '] vs 存储[' + storedToken + ']');

                if (urlToken === storedToken || decodeURIComponent(urlToken) === storedToken) {
                    log('Token验证通过！');
                    // 验证通过，设置认证状态
                    setStorage(AUTH_KEY, 'true');
                    setStorage(AUTH_TIMESTAMP, Date.now().toString());
                    // 清除URL参数
                    var url = new URL(window.location.href);
                    url.searchParams.delete(TOKEN_PARAM);
                    window.history.replaceState({}, '', url.toString());
                    log('URL参数已清除');
                    return true;
                } else {
                    log('Token不匹配！');
                }
            }

            // 常规检查
            log('进行常规认证检查...');
            var auth = getStorage(AUTH_KEY);
            var timestamp = getStorage(AUTH_TIMESTAMP);
            log('认证状态: ' + auth);
            log('时间戳: ' + timestamp);

            if (!auth || auth !== 'true') {
                log('认证失败: 未设置认证状态');
                return false;
            }

            if (timestamp) {
                var elapsed = Date.now() - parseInt(timestamp);
                log('已过时间: ' + elapsed + 'ms');
                if (elapsed > SESSION_TIMEOUT) {
                    log('认证超时！');
                    this.logout();
                    return false;
                }
            }

            setStorage(AUTH_TIMESTAMP, Date.now().toString());
            log('认证通过！');
            return true;
        },

        login: function() {
            log('执行登录...');
            setStorage(AUTH_KEY, 'true');
            setStorage(AUTH_TIMESTAMP, Date.now().toString());
        },

        logout: function() {
            log('执行登出...');
            deleteStorage(AUTH_KEY);
            deleteStorage(AUTH_TIMESTAMP);
            deleteStorage('auth_token');
            window.location.href = 'index.html';
        },

        checkAuth: function() {
            log('=== checkAuth 被调用 ===');
            if (!this.isAuthenticated()) {
                log('认证失败，跳转登录页');
                window.location.replace('index.html');
                return false;
            }
            return true;
        }
    };
})();
