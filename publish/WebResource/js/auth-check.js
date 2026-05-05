(function() {
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
            // 认证状态由服务端管理，前端无法直接判断
            // 返回true让页面继续加载，实际认证由服务端在API层面控制
            return true;
        },

        // 异步验证会话
        validateSession: function() {
            return fetch('/api/csrf-token', {
                method: 'GET',
                credentials: 'same-origin',
                headers: { 'Accept': 'application/json' }
            })
            .then(function(response) {
                if (response.status === 401) {
                    log('会话验证失败: 未认证');
                    return false;
                }
                return response.json().then(function(data) {
                    var valid = data.success === true;
                    log('会话验证结果: ' + valid);
                    return valid;
                });
            })
            .catch(function(error) {
                log('会话验证错误: ' + error);
                return false;
            });
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
            // HttpOnly Cookie 前端JS无法读取，不能通过document.cookie判断
            // 服务端已在返回main.html时做了认证检查（未认证会302跳转）
            // 前端这里直接返回true，让页面正常加载
            // 后续的API调用会由服务端验证Cookie有效性
            return true;
        }
    };
})();
