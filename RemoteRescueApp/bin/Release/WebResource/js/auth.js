document.addEventListener('DOMContentLoaded', function() {
    const authForm = document.getElementById('authForm');
    const errorMsg = document.getElementById('errorMsg');
    const passwordInput = document.getElementById('password');

    // 检查是否已登录（使用localStorage支持CDN回源）
    if (AuthCheck && AuthCheck.isAuthenticated()) {
        window.location.replace('main.html');
        return;
    }

    authForm.addEventListener('submit', function(e) {
        e.preventDefault();
        
        const password = passwordInput.value.trim();
        
        if (!password) {
            showError('请输入密码');
            return;
        }
        
        // 发送到后端验证
        verifyPassword(password);
    });

    passwordInput.addEventListener('input', function() {
        errorMsg.classList.remove('show');
    });

    passwordInput.addEventListener('keypress', function(e) {
        if (e.key === 'Enter') {
            authForm.dispatchEvent(new Event('submit'));
        }
    });

    function verifyPassword(password) {
        const btn = authForm.querySelector('.auth-btn');
        const originalText = btn.innerHTML;
        btn.innerHTML = '<span>验证中...</span>';
        btn.disabled = true;

        fetch('/api/login', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded'
            },
            body: 'password=' + encodeURIComponent(password)
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                // 登录成功
                if (AuthCheck) {
                    AuthCheck.login();
                    // 生成token用于URL传递（支持FRP场景）
                    var token = AuthCheck.generateAuthToken();
                    btn.innerHTML = `
                        <span>验证成功</span>
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <polyline points="20 6 9 17 4 12"></polyline>
                        </svg>
                    `;
                    btn.style.background = 'linear-gradient(135deg, #10b981, #059669)';

                    setTimeout(() => {
                        window.location.href = 'main.html?_token=' + encodeURIComponent(token);
                    }, 500);
                } else {
                    btn.innerHTML = `
                        <span>验证成功</span>
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <polyline points="20 6 9 17 4 12"></polyline>
                        </svg>
                    `;
                    btn.style.background = 'linear-gradient(135deg, #10b981, #059669)';

                    setTimeout(() => {
                        window.location.href = 'main.html';
                    }, 500);
                }
            } else {
                // 登录失败
                showError(data.message || '密码错误，请重试');
                passwordInput.value = '';
                passwordInput.focus();
                btn.innerHTML = originalText;
                btn.disabled = false;
            }
        })
        .catch(error => {
            showError('验证失败，请重试');
            btn.innerHTML = originalText;
            btn.disabled = false;
        });
    }

    function showError(message) {
        errorMsg.textContent = message;
        errorMsg.classList.add('show');
        
        setTimeout(() => {
            errorMsg.classList.remove('show');
        }, 3000);
    }
});
