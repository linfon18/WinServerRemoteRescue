using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Linq;

namespace RemoteRescueApp
{
    public class HttpServer
    {
        private HttpListener _listener;
        private Thread _listenerThread;
        private bool _isRunning;
        private string _webRoot;
        private MainForm _mainForm;
        private const string CORRECT_PASSWORD = "12345";

        // Session管理：token -> (创建时间, 最后活动时间, CSRF Token)
        private Dictionary<string, SessionInfo> _sessions = new Dictionary<string, SessionInfo>();
        private readonly object _sessionLock = new object();
        private const int SESSION_TIMEOUT_MINUTES = 30;
        private const int SESSION_TIMEOUT_MS = SESSION_TIMEOUT_MINUTES * 60 * 1000;

        // 速率限制：IP -> (失败次数, 首次失败时间, 锁定到期时间)
        private Dictionary<string, RateLimitInfo> _rateLimits = new Dictionary<string, RateLimitInfo>();
        private readonly object _rateLock = new object();
        private const int MAX_LOGIN_ATTEMPTS = 5;
        private const int RATE_LIMIT_WINDOW_MINUTES = 15;
        private const int RATE_LIMIT_LOCKOUT_MINUTES = 15;

        public bool IsRunning { get { return _isRunning; } }

        private class SessionInfo
        {
            public DateTime CreatedAt { get; set; }
            public DateTime LastActivity { get; set; }
            public string CsrfToken { get; set; }
        }

        private class RateLimitInfo
        {
            public int FailedAttempts { get; set; }
            public DateTime FirstFailure { get; set; }
            public DateTime? LockoutUntil { get; set; }
        }

        public HttpServer(MainForm mainForm)
        {
            _mainForm = mainForm;
            _webRoot = Path.Combine(Application.StartupPath, "WebResource");
        }

        public void Start()
        {
            if (_isRunning) return;

            if (!Directory.Exists(_webRoot))
            {
                throw new Exception("WebResource目录不存在: " + _webRoot);
            }

            _listener = new HttpListener();
            _listener.Prefixes.Add("http://+:8889/");
            _listener.Start();
            _isRunning = true;

            _listenerThread = new Thread(new ThreadStart(Listen));
            _listenerThread.IsBackground = true;
            _listenerThread.Start();

            if (_mainForm != null)
            {
                _mainForm.UpdateStatus("HTTP服务已启动", true);
                _mainForm.LogMessage("HTTP监听器已创建");
                _mainForm.LogMessage("Web资源目录: " + _webRoot);
            }
        }

        public void Stop()
        {
            _isRunning = false;
            if (_listener != null)
            {
                _listener.Stop();
                _listener.Close();
                _listener = null;
            }
            if (_mainForm != null)
                _mainForm.UpdateStatus("HTTP服务已停止", false);
        }

        private void Listen()
        {
            if (_mainForm != null)
                _mainForm.LogMessage("HTTP监听线程已启动");

            while (_isRunning)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(new WaitCallback(ProcessRequest), context);
                }
                catch (Exception ex)
                {
                    if (_isRunning && _mainForm != null)
                    {
                        _mainForm.LogMessage("HTTP监听错误: " + ex.Message);
                    }
                }
            }

            if (_mainForm != null)
                _mainForm.LogMessage("HTTP监听线程已停止");
        }

        private void ProcessRequest(object state)
        {
            var context = (HttpListenerContext)state;
            var request = context.Request;
            var response = context.Response;
            string path = "";

            try
            {
                string clientIP = request.RemoteEndPoint.Address.ToString();
                // 将IPv6回环地址转换为IPv4
                if (clientIP == "::1")
                {
                    clientIP = "127.0.0.1";
                }
                else if (clientIP.StartsWith("::ffff:"))
                {
                    clientIP = clientIP.Substring(7);
                }
                string method = request.HttpMethod;
                path = request.Url.AbsolutePath;

                if (_mainForm != null)
                {
                    _mainForm.IncrementRequestCount();
                    if (!path.StartsWith("/bg/") && !path.StartsWith("/css/") && !path.StartsWith("/js/"))
                    {
                        _mainForm.LogMessage("[" + clientIP + "] " + method + " " + path);
                    }
                }

                // 添加安全响应头
                AddSecurityHeaders(response);

                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, X-CSRF-Token");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                try
                {
                    if (path.StartsWith("/api/"))
                    {
                        HandleApiRequest(request, response, clientIP);
                    }
                    else
                    {
                        HandleStaticFileRequest(request, response, path, clientIP);
                    }
                }
                catch (Exception ex)
                {
                    if (_mainForm != null)
                        _mainForm.LogMessage("处理请求错误 [" + path + "]: " + ex.Message);
                    try
                    {
                        if (response.OutputStream.CanWrite)
                        {
                            response.StatusCode = 500;
                            byte[] buffer = Encoding.UTF8.GetBytes("{\"success\":false,\"message\":\"服务器内部错误\"}");
                            response.ContentType = "application/json";
                            response.ContentLength64 = buffer.Length;
                            response.OutputStream.Write(buffer, 0, buffer.Length);
                            response.Close();
                        }
                    }
                    catch (Exception innerEx)
                    {
                        if (_mainForm != null)
                            _mainForm.LogMessage("错误响应发送失败 [" + path + "]: " + innerEx.Message);
                        try { response.Close(); } catch { }
                    }
                }
            }
            catch (Exception fatalEx)
            {
                // 最外层保护：任何未捕获异常都不应导致线程崩溃
                if (_mainForm != null)
                    _mainForm.LogMessage("请求处理致命错误 [" + path + "]: " + fatalEx.Message);
                try { response.Close(); } catch { }
            }
        }

        private void AddSecurityHeaders(HttpListenerResponse response)
        {
            response.Headers.Add("X-Content-Type-Options", "nosniff");
            response.Headers.Add("X-Frame-Options", "DENY");
            response.Headers.Add("X-XSS-Protection", "1; mode=block");
            response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
            response.Headers.Add("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            response.Headers.Add("Cache-Control", "no-store, no-cache, must-revalidate, private");
            response.Headers.Add("Pragma", "no-cache");
            response.Headers.Add("Expires", "0");
            // 如果通过HTTPS访问，添加HSTS
            // response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        }

        private string GenerateSecureToken()
        {
            byte[] tokenBytes = new byte[32];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(tokenBytes);
            }
            return BitConverter.ToString(tokenBytes).Replace("-", "").ToLower();
        }

        private string GetSessionToken(HttpListenerRequest request)
        {
            // 优先从Cookie获取Session Token
            Cookie sessionCookie = request.Cookies["session_token"];
            if (sessionCookie != null && !string.IsNullOrEmpty(sessionCookie.Value))
            {
                return sessionCookie.Value;
            }
            // 兼容：从URL参数获取（FRP场景）
            string urlToken = request.QueryString["_token"];
            if (!string.IsNullOrEmpty(urlToken))
            {
                return urlToken;
            }
            return null;
        }

        private bool IsSessionValid(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            lock (_sessionLock)
            {
                SessionInfo session;
                if (_sessions.TryGetValue(token, out session))
                {
                    if (DateTime.Now.Subtract(session.LastActivity).TotalMilliseconds > SESSION_TIMEOUT_MS)
                    {
                        _sessions.Remove(token);
                        return false;
                    }
                    session.LastActivity = DateTime.Now;
                    return true;
                }
            }
            return false;
        }

        private void SetSessionCookie(HttpListenerResponse response, string token)
        {
            // HttpOnly + Secure + SameSite=Strict Cookie
            // Secure属性要求HTTPS，如果当前是HTTP环境则不加Secure
            bool isHttps = response.Headers.AllKeys.Contains("X-Forwarded-Proto") &&
                           response.Headers["X-Forwarded-Proto"] == "https";

            string cookieValue = string.Format("session_token={0}; Path=/; HttpOnly; SameSite=Strict; Max-Age={1}{2}",
                token, SESSION_TIMEOUT_MINUTES * 60, isHttps ? "; Secure" : "");
            response.Headers.Add("Set-Cookie", cookieValue);
        }

        private void ClearSessionCookie(HttpListenerResponse response)
        {
            bool isHttps = response.Headers.AllKeys.Contains("X-Forwarded-Proto") &&
                           response.Headers["X-Forwarded-Proto"] == "https";
            string cookieValue = string.Format("session_token=; Path=/; HttpOnly; SameSite=Strict; Max-Age=0{0}",
                isHttps ? "; Secure" : "");
            response.Headers.Add("Set-Cookie", cookieValue);
        }

        private bool CheckRateLimit(string clientIP)
        {
            lock (_rateLock)
            {
                DateTime now = DateTime.Now;
                RateLimitInfo info;
                if (!_rateLimits.TryGetValue(clientIP, out info))
                {
                    info = new RateLimitInfo { FailedAttempts = 0, FirstFailure = now };
                    _rateLimits[clientIP] = info;
                }

                // 检查是否被锁定
                if (info.LockoutUntil.HasValue && now < info.LockoutUntil.Value)
                {
                    return false;
                }

                // 如果超过窗口期，重置计数
                if (now.Subtract(info.FirstFailure).TotalMinutes > RATE_LIMIT_WINDOW_MINUTES)
                {
                    info.FailedAttempts = 0;
                    info.FirstFailure = now;
                    info.LockoutUntil = null;
                }

                return true;
            }
        }

        private void RecordFailedAttempt(string clientIP)
        {
            lock (_rateLock)
            {
                RateLimitInfo info;
                if (!_rateLimits.TryGetValue(clientIP, out info))
                {
                    info = new RateLimitInfo { FailedAttempts = 0, FirstFailure = DateTime.Now };
                    _rateLimits[clientIP] = info;
                }
                info.FailedAttempts++;
                if (info.FailedAttempts >= MAX_LOGIN_ATTEMPTS)
                {
                    info.LockoutUntil = DateTime.Now.AddMinutes(RATE_LIMIT_LOCKOUT_MINUTES);
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ IP[" + clientIP + "] 登录失败过多，锁定" + RATE_LIMIT_LOCKOUT_MINUTES + "分钟");
                }
            }
        }

        private void RecordSuccessfulAttempt(string clientIP)
        {
            lock (_rateLock)
            {
                _rateLimits.Remove(clientIP);
            }
        }

        private void HandleApiRequest(HttpListenerRequest request, HttpListenerResponse response, string clientIP)
        {
            string path = request.Url.AbsolutePath;
            string json = "";
            string sessionToken = GetSessionToken(request);
            bool isAuthenticated = IsSessionValid(sessionToken);

            switch (path)
            {
                case "/api/login":
                    json = HandleLogin(request, response, clientIP);
                    break;
                case "/api/logout":
                    json = HandleLogout(response, sessionToken);
                    break;
                case "/api/csrf-token":
                    json = HandleCsrfToken(sessionToken);
                    break;
                case "/api/status":
                    if (!isAuthenticated)
                    {
                        response.StatusCode = 401;
                        json = "{\"success\": false, \"message\": \"未认证\", \"code\": \"UNAUTHORIZED\"}";
                        break;
                    }
                    var info = SystemManager.GetSystemInfo();
                    var explorerInfo = SystemManager.GetExplorerInfo();
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    json = "{\"success\": true, \"message\": \"获取成功\", \"data\": {\"systemInfo\": \"" + info + "\", \"explorerInfo\": \"" + explorerInfo + "\", \"timestamp\": \"" + timestamp + "\", \"status\": \"online\"}}";
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ 返回系统状态信息");
                    break;
                case "/api/restart/rdp":
                    if (!isAuthenticated)
                    {
                        response.StatusCode = 401;
                        json = "{\"success\": false, \"message\": \"未认证\", \"code\": \"UNAUTHORIZED\"}";
                        break;
                    }
                    if (!VerifyCsrfToken(request, sessionToken))
                    {
                        response.StatusCode = 403;
                        json = "{\"success\": false, \"message\": \"CSRF验证失败\", \"code\": \"CSRF_ERROR\"}";
                        if (_mainForm != null)
                            _mainForm.LogMessage("→ CSRF验证失败 [" + clientIP + "]");
                        break;
                    }
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ 执行RDP服务重启操作...");
                    var rdpResult = SystemManager.RestartRDPService();
                    json = "{\"success\": true, \"message\": \"" + rdpResult + "\"}";
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ RDP操作结果: " + rdpResult);
                    break;
                case "/api/restart/explorer":
                    if (!isAuthenticated)
                    {
                        response.StatusCode = 401;
                        json = "{\"success\": false, \"message\": \"未认证\", \"code\": \"UNAUTHORIZED\"}";
                        break;
                    }
                    if (!VerifyCsrfToken(request, sessionToken))
                    {
                        response.StatusCode = 403;
                        json = "{\"success\": false, \"message\": \"CSRF验证失败\", \"code\": \"CSRF_ERROR\"}";
                        if (_mainForm != null)
                            _mainForm.LogMessage("→ CSRF验证失败 [" + clientIP + "]");
                        break;
                    }
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ 执行Explorer重启操作...");
                    var expResult = SystemManager.RestartExplorer();
                    json = "{\"success\": true, \"message\": \"" + expResult + "\"}";
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ Explorer操作结果: " + expResult);
                    break;
                case "/api/restart/server":
                    if (!isAuthenticated)
                    {
                        response.StatusCode = 401;
                        json = "{\"success\": false, \"message\": \"未认证\", \"code\": \"UNAUTHORIZED\"}";
                        break;
                    }
                    if (!VerifyCsrfToken(request, sessionToken))
                    {
                        response.StatusCode = 403;
                        json = "{\"success\": false, \"message\": \"CSRF验证失败\", \"code\": \"CSRF_ERROR\"}";
                        if (_mainForm != null)
                            _mainForm.LogMessage("→ CSRF验证失败 [" + clientIP + "]");
                        break;
                    }
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ 执行服务器重启操作...");
                    var srvResult = SystemManager.RestartServer();
                    json = "{\"success\": true, \"message\": \"" + srvResult + "\"}";
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ 服务器操作结果: " + srvResult);
                    break;
                default:
                    response.StatusCode = 404;
                    json = "{\"success\": false, \"message\": \"接口不存在\"}";
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ API未找到: " + path);
                    break;
            }

            WriteResponse(response, json, "application/json");
        }

        private bool VerifyCsrfToken(HttpListenerRequest request, string sessionToken)
        {
            if (string.IsNullOrEmpty(sessionToken)) return false;
            string csrfHeader = request.Headers["X-CSRF-Token"];
            if (string.IsNullOrEmpty(csrfHeader)) return false;
            lock (_sessionLock)
            {
                SessionInfo session;
                if (_sessions.TryGetValue(sessionToken, out session))
                {
                    return session.CsrfToken == csrfHeader;
                }
            }
            return false;
        }

        private string HandleCsrfToken(string sessionToken)
        {
            if (!IsSessionValid(sessionToken))
            {
                return "{\"success\": false, \"message\": \"未认证\"}";
            }
            lock (_sessionLock)
            {
                SessionInfo session;
                if (_sessions.TryGetValue(sessionToken, out session))
                {
                    return "{\"success\": true, \"token\": \"" + session.CsrfToken + "\"}";
                }
            }
            return "{\"success\": false, \"message\": \"会话无效\"}";
        }

        private string HandleLogout(HttpListenerResponse response, string sessionToken)
        {
            if (!string.IsNullOrEmpty(sessionToken))
            {
                lock (_sessionLock)
                {
                    _sessions.Remove(sessionToken);
                }
            }
            ClearSessionCookie(response);
            return "{\"success\": true, \"message\": \"已退出登录\"}";
        }

        private string HandleLogin(HttpListenerRequest request, HttpListenerResponse response, string clientIP)
        {
            try
            {
                // 速率限制检查
                if (!CheckRateLimit(clientIP))
                {
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ 登录被拒绝 [" + clientIP + "]: 请求过于频繁");
                    response.StatusCode = 429;
                    return "{\"success\": false, \"message\": \"尝试次数过多，请" + RATE_LIMIT_LOCKOUT_MINUTES + "分钟后再试\", \"code\": \"RATE_LIMITED\"}";
                }

                string password = "";
                string body = "";
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    body = reader.ReadToEnd();
                }

                if (_mainForm != null)
                {
                    _mainForm.LogMessage("→ 收到登录请求 [" + clientIP + "]");
                }

                // 支持表单格式: password=xxx
                if (body.StartsWith("password="))
                {
                    password = body.Substring(9);
                    password = System.Uri.UnescapeDataString(password);
                }
                else
                {
                    password = ExtractPasswordFromJson(body);
                }

                if (password == CORRECT_PASSWORD)
                {
                    RecordSuccessfulAttempt(clientIP);

                    // 创建安全Session
                    string sessionToken = GenerateSecureToken();
                    string csrfToken = GenerateSecureToken();
                    lock (_sessionLock)
                    {
                        _sessions[sessionToken] = new SessionInfo
                        {
                            CreatedAt = DateTime.Now,
                            LastActivity = DateTime.Now,
                            CsrfToken = csrfToken
                        };
                    }

                    // 设置HttpOnly Cookie
                    SetSessionCookie(response, sessionToken);

                    if (_mainForm != null)
                        _mainForm.LogMessage("→ 登录成功 [" + clientIP + "]");
                    return "{\"success\": true, \"message\": \"登录成功\", \"token\": \"" + sessionToken + "\", \"csrfToken\": \"" + csrfToken + "\"}";
                }
                else
                {
                    RecordFailedAttempt(clientIP);
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ 登录失败 [" + clientIP + "]: 密码错误");
                    return "{\"success\": false, \"message\": \"密码错误\", \"code\": \"INVALID_PASSWORD\"}";
                }
            }
            catch (Exception ex)
            {
                if (_mainForm != null)
                    _mainForm.LogMessage("→ 登录处理错误: " + ex.Message);
                return "{\"success\": false, \"message\": \"验证失败\", \"code\": \"ERROR\"}";
            }
        }

        private string ExtractPasswordFromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return "";

            try
            {
                // 方法1: 查找 "password":"value" 格式
                int passwordIndex = json.IndexOf("\"password\"");
                if (passwordIndex >= 0)
                {
                    int colonIndex = json.IndexOf(':', passwordIndex);
                    if (colonIndex > 0)
                    {
                        int valueStart = colonIndex + 1;
                        // 跳过空白字符
                        while (valueStart < json.Length && (json[valueStart] == ' ' || json[valueStart] == '\t' || json[valueStart] == '\n' || json[valueStart] == '\r'))
                            valueStart++;

                        if (valueStart < json.Length && json[valueStart] == '"')
                        {
                            valueStart++; // 跳过开头的引号
                            int valueEnd = json.IndexOf('"', valueStart);
                            if (valueEnd > valueStart)
                            {
                                return json.Substring(valueStart, valueEnd - valueStart);
                            }
                        }
                    }
                }

                // 方法2: 正则式匹配
                var match = System.Text.RegularExpressions.Regex.Match(json, "\"password\"\\s*:\\s*\"([^\"]*)\"");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            catch { }

            return "";
        }

        private void HandleStaticFileRequest(HttpListenerRequest request, HttpListenerResponse response, string path, string clientIP)
        {
            if (path == "/" || string.IsNullOrEmpty(path))
            {
                path = "/index.html";
            }

            // 保护main.html：需要有效Session
            if (path.Equals("/main.html", StringComparison.OrdinalIgnoreCase))
            {
                string sessionToken = GetSessionToken(request);
                if (!IsSessionValid(sessionToken))
                {
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ 未认证访问main.html，重定向到登录页 [" + clientIP + "]");
                    response.StatusCode = 302;
                    response.Headers.Add("Location", "/index.html");
                    response.Close();
                    return;
                }
            }

            string filePath = Path.Combine(_webRoot, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(filePath))
            {
                if (_mainForm != null && !path.StartsWith("/favicon"))
                    _mainForm.LogMessage("→ 文件未找到: " + path);
                response.StatusCode = 404;
                WriteResponse(response, "<!DOCTYPE html><html><head><title>404</title></head><body><h1>404</h1><p>The requested page was not found.</p></body></html>", "text/html");
                return;
            }

            string contentType = GetContentType(Path.GetExtension(filePath));
            byte[] content = File.ReadAllBytes(filePath);

            response.ContentType = contentType;
            response.ContentLength64 = content.Length;
            response.OutputStream.Write(content, 0, content.Length);
            response.Close();
        }

        private string GetContentType(string extension)
        {
            switch (extension.ToLower())
            {
                case ".html": return "text/html";
                case ".css": return "text/css";
                case ".js": return "application/javascript";
                case ".json": return "application/json";
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".gif": return "image/gif";
                case ".svg": return "image/svg+xml";
                case ".ico": return "image/x-icon";
                case ".mp4": return "video/mp4";
                default: return "application/octet-stream";
            }
        }

        private void WriteResponse(HttpListenerResponse response, string content, string contentType)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            response.ContentType = contentType;
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }
    }
}
