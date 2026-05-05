document.addEventListener('DOMContentLoaded', function() {
    const authForm = document.getElementById('authForm');
    const errorMsg = document.getElementById('errorMsg');
    const passwordInput = document.getElementById('password');

    authForm.addEventListener('submit', function(e) {
        e.preventDefault();

        const password = passwordInput.value.trim();

        if (!password) {
            showError('请输入密码');
            return;
        }

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
            body: 'password=' + encodeURIComponent(password),
            credentials: 'same-origin'
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                btn.innerHTML = `
                    <span>验证成功</span>
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="20 6 9 17 4 12"></polyline>
                    </svg>
                `;
                btn.style.background = 'linear-gradient(135deg, #10b981, #059669)';

                setTimeout(() => {
                    // 使用服务端返回的token（FRP场景）
                    if (data.token) {
                        window.location.href = 'main.html?_token=' + encodeURIComponent(data.token);
                    } else {
                        window.location.href = 'main.html';
                    }
                }, 500);
            } else {
                if (data.code === 'RATE_LIMITED') {
                    showError(data.message || '尝试次数过多，请稍后再试');
                } else {
                    showError(data.message || '密码错误，请重试');
                }
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
