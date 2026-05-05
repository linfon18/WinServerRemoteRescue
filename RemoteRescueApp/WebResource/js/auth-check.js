(function() {
    const SESSION_TIMEOUT = 30 * 60 * 1000;
    const TOKEN_PARAM = '_token';
    const DEBUG = false;

    function log(msg) {
        if (DEBUG) console.log('[AuthCheck] ' + msg);
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

    window.AuthCheck = {
        isAuthenticated: function() {
            log('=== 开始认证检查 ===');
            log('当前URL: ' + window.location.href);

            // 检查URL参数（FRP场景）
            var urlToken = getUrlParam(TOKEN_PARAM);
            log('URL Token: ' + urlToken);

            if (urlToken) {
                log('URL参数已清除');
                var url = new URL(window.location.href);
                url.searchParams.delete(TOKEN_PARAM);
                window.history.replaceState({}, '', url.toString());
            }

            // 认证状态由服务端Cookie管理，前端不做判断
            // 服务端会在访问main.html时验证Session
            log('认证状态由服务端管理');
            return false;
        },

        login: function() {
            log('执行登录...');
        },

        logout: function() {
            log('执行登出...');
            fetch('/api/logout', {
                method: 'POST',
                credentials: 'same-origin'
            }).finally(function() {
                window.location.href = 'index.html';
            });
        },

        checkAuth: function() {
            log('=== checkAuth 被调用 ===');
            // 认证由服务端在main.html请求时验证
            // 如果未认证，服务端会302重定向到index.html
            // 这里仅做辅助检查
            return true;
        }
    };
})();
