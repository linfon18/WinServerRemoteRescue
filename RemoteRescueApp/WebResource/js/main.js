const API_BASE_URL = '';

let currentAction = null;
let confirmCallback = null;
let statusUpdateInterval = null;
let csrfToken = '';

const systemInfo = {
    machineName: '--',
    userName: '--',
    osVersion: '--',
    processorCount: '--',
    uptime: '--',
    lastUpdate: '--',
    explorerInfo: '--'
};

document.addEventListener('DOMContentLoaded', function() {
    init();
});

function init() {
    // 获取CSRF Token
    fetchCsrfToken().then(() => {
        updateSystemInfo();
        startStatusUpdate();
    });

    document.addEventListener('visibilitychange', function() {
        if (document.hidden) {
            stopStatusUpdate();
        } else {
            startStatusUpdate();
            updateSystemInfo();
        }
    });
}

async function fetchCsrfToken() {
    try {
        const response = await fetch(`${API_BASE_URL}/api/csrf-token`, {
            method: 'GET',
            credentials: 'same-origin',
            headers: {
                'Accept': 'application/json'
            }
        });
        const data = await response.json();
        if (data.success && data.token) {
            csrfToken = data.token;
        }
    } catch (error) {
        console.error('获取CSRF Token失败:', error);
    }
}

function startStatusUpdate() {
    if (statusUpdateInterval) {
        clearInterval(statusUpdateInterval);
    }

    updateSystemInfo();

    statusUpdateInterval = setInterval(() => {
        updateSystemInfo();
    }, 5000);
}

function stopStatusUpdate() {
    if (statusUpdateInterval) {
        clearInterval(statusUpdateInterval);
        statusUpdateInterval = null;
    }
}

async function updateSystemInfo() {
    try {
        const response = await fetch(`${API_BASE_URL}/api/status`, {
            method: 'GET',
            headers: {
                'Accept': 'application/json'
            },
            credentials: 'same-origin'
        });

        if (response.status === 401) {
            window.location.href = 'index.html';
            return;
        }

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();

        if (data.success && data.data) {
            parseSystemInfo(data.data.systemInfo);
            systemInfo.explorerInfo = data.data.explorerInfo || '--';
            updateUI();
            updateConnectionStatus(true);
        }
    } catch (error) {
        console.error('获取系统信息失败:', error);
        updateConnectionStatus(false);

        systemInfo.lastUpdate = new Date().toLocaleString('zh-CN');
        document.getElementById('lastUpdate').textContent = systemInfo.lastUpdate;
    }
}

function parseSystemInfo(infoString) {
    if (!infoString) return;

    const parts = infoString.split(' | ');

    parts.forEach(part => {
        const [key, value] = part.split(': ');
        if (!value) return;

        switch(key.trim()) {
            case '计算机名':
                systemInfo.machineName = value.trim();
                break;
            case '用户名':
                systemInfo.userName = value.trim();
                break;
            case '操作系统':
                systemInfo.osVersion = value.trim();
                break;
            case '处理器':
                systemInfo.processorCount = value.trim();
                break;
            case '运行时间':
                systemInfo.uptime = value.trim();
                break;
        }
    });

    systemInfo.lastUpdate = new Date().toLocaleString('zh-CN');
}

function updateUI() {
    document.getElementById('machineName').textContent = systemInfo.machineName;
    document.getElementById('userName').textContent = systemInfo.userName;
    document.getElementById('osVersion').textContent = systemInfo.osVersion;
    document.getElementById('processorCount').textContent = systemInfo.processorCount;
    document.getElementById('uptime').textContent = systemInfo.uptime;
    document.getElementById('lastUpdate').textContent = systemInfo.lastUpdate;

    const explorerEl = document.getElementById('explorerInfo');
    if (explorerEl) {
        explorerEl.textContent = systemInfo.explorerInfo;
    }
}

function updateConnectionStatus(connected) {
    const statusEl = document.getElementById('connectionStatus');
    const statusText = document.getElementById('statusText');

    if (connected) {
        statusEl.classList.remove('offline');
        statusText.textContent = '已连接';
    } else {
        statusEl.classList.add('offline');
        statusText.textContent = '未连接';
    }
}

function restartRDP() {
    showConfirm(
        'warning',
        '重启RDP服务',
        '确定要重启远程桌面服务吗？这将暂时中断所有远程连接。',
        async () => {
            const btn = document.getElementById('btnRdp');
            const statusEl = document.getElementById('statusRdp');

            setButtonLoading(btn, true);
            showStatus(statusEl, '正在重启RDP服务...', 'info');

            try {
                const response = await fetch(`${API_BASE_URL}/api/restart/rdp`, {
                    method: 'POST',
                    headers: {
                        'Accept': 'application/json',
                        'X-CSRF-Token': csrfToken
                    },
                    credentials: 'same-origin'
                });

                if (response.status === 401) {
                    window.location.href = 'index.html';
                    return;
                }

                const data = await response.json();

                if (data.success) {
                    showStatus(statusEl, '\u2713 ' + data.message, 'success');
                    showToast('success', '操作成功', data.message);
                } else {
                    showStatus(statusEl, '\u2717 操作失败', 'error');
                    showToast('error', '操作失败', data.message || '\u672a\u77e5\u9519\u8bef');
                }
            } catch (error) {
                showStatus(statusEl, '\u2717 连接失败', 'error');
                showToast('error', '连接失败', '无法连接到本地服务，请检查服务是否运行');
            } finally {
                setButtonLoading(btn, false);
                setTimeout(() => {
                    hideStatus(statusEl);
                }, 5000);
            }
        }
    );
}

function restartExplorer() {
    showConfirm(
        'warning',
        '重启Explorer',
        '确定要重启资源管理器吗？桌面可能会短暂闪烁。',
        async () => {
            const btn = document.getElementById('btnExplorer');
            const statusEl = document.getElementById('statusExplorer');

            setButtonLoading(btn, true);
            showStatus(statusEl, '正在重启Explorer...', 'info');

            try {
                const response = await fetch(`${API_BASE_URL}/api/restart/explorer`, {
                    method: 'POST',
                    headers: {
                        'Accept': 'application/json',
                        'X-CSRF-Token': csrfToken
                    },
                    credentials: 'same-origin'
                });

                if (response.status === 401) {
                    window.location.href = 'index.html';
                    return;
                }

                const data = await response.json();

                if (data.success) {
                    showStatus(statusEl, '\u2713 ' + data.message, 'success');
                    showToast('success', '操作成功', data.message);
                } else {
                    showStatus(statusEl, '\u2717 操作失败', 'error');
                    showToast('error', '操作失败', data.message || '\u672a\u77e5\u9519\u8bef');
                }
            } catch (error) {
                showStatus(statusEl, '\u2717 连接失败', 'error');
                showToast('error', '连接失败', '无法连接到本地服务，请检查服务是否运行');
            } finally {
                setButtonLoading(btn, false);
                setTimeout(() => {
                    hideStatus(statusEl);
                }, 5000);
            }
        }
    );
}

function restartServer() {
    showConfirm(
        'danger',
        '重启服务器',
        '\u26a0\ufe0f 警告：确定要重启服务器吗？此操作将导致服务器在10秒后重新启动，所有未保存的数据将丢失！',
        async () => {
            const btn = document.getElementById('btnServer');
            const statusEl = document.getElementById('statusServer');

            setButtonLoading(btn, true);
            showStatus(statusEl, '正在执行重启命令...', 'info');

            try {
                const response = await fetch(`${API_BASE_URL}/api/restart/server`, {
                    method: 'POST',
                    headers: {
                        'Accept': 'application/json',
                        'X-CSRF-Token': csrfToken
                    },
                    credentials: 'same-origin'
                });

                if (response.status === 401) {
                    window.location.href = 'index.html';
                    return;
                }

                const data = await response.json();

                if (data.success) {
                    showStatus(statusEl, '\u2713 ' + data.message, 'success');
                    showToast('success', '重启命令已发送', data.message);
                } else {
                    showStatus(statusEl, '\u2717 操作失败', 'error');
                    showToast('error', '操作失败', data.message || '\u672a\u77e5\u9519\u8bef');
                }
            } catch (error) {
                showStatus(statusEl, '\u2717 连接失败', 'error');
                showToast('error', '连接失败', '无法连接到本地服务，请检查服务是否运行');
            } finally {
                setButtonLoading(btn, false);
                setTimeout(() => {
                    hideStatus(statusEl);
                }, 5000);
            }
        }
    );
}

function setButtonLoading(btn, loading) {
    if (loading) {
        btn.classList.add('loading');
        btn.style.pointerEvents = 'none';
    } else {
        btn.classList.remove('loading');
        btn.style.pointerEvents = 'auto';
    }
}

function showStatus(el, message, type) {
    el.textContent = message;
    el.className = 'action-status ' + type;
    el.parentElement.classList.add('show-status');
}

function hideStatus(el) {
    el.parentElement.classList.remove('show-status');
}

function showConfirm(type, title, message, callback) {
    const modal = document.getElementById('confirmModal');
    const modalIcon = document.getElementById('modalIcon');
    const modalTitle = document.getElementById('modalTitle');
    const modalMessage = document.getElementById('modalMessage');
    const confirmBtn = document.getElementById('modalConfirmBtn');

    modalIcon.className = 'modal-icon ' + type;
    modalTitle.textContent = title;
    modalMessage.textContent = message;

    confirmBtn.className = 'modal-btn ' + (type === 'danger' ? 'modal-btn-danger' : 'modal-btn-primary');

    confirmCallback = callback;

    modal.classList.add('show');
}

function closeModal() {
    const modal = document.getElementById('confirmModal');
    modal.classList.remove('show');
    confirmCallback = null;
}

function confirmAction() {
    if (confirmCallback) {
        confirmCallback();
        confirmCallback = null;
    }
    closeModal();
}

function showToast(type, title, message) {
    const container = document.getElementById('toastContainer');

    const toast = document.createElement('div');
    toast.className = 'toast ' + type;

    const iconSvg = type === 'success'
        ? '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>'
        : type === 'error'
        ? '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>'
        : '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>';

    toast.innerHTML = `
        <div class="toast-icon">${iconSvg}</div>
        <div class="toast-content">
            <div class="toast-title">${title}</div>
            <div class="toast-message">${message}</div>
        </div>
        <button class="toast-close" onclick="this.parentElement.remove()">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <line x1="18" y1="6" x2="6" y2="18"></line>
                <line x1="6" y1="6" x2="18" y2="18"></line>
            </svg>
        </button>
    `;

    container.appendChild(toast);

    setTimeout(() => {
        toast.classList.add('hiding');
        setTimeout(() => {
            toast.remove();
        }, 400);
    }, 5000);
}

function logout() {
    if (AuthCheck) {
        AuthCheck.logout();
    } else {
        window.location.href = 'index.html';
    }
}

document.getElementById('confirmModal').addEventListener('click', function(e) {
    if (e.target === this) {
        closeModal();
    }
});

document.addEventListener('keydown', function(e) {
    if (e.key === 'Escape') {
        closeModal();
    }
});
