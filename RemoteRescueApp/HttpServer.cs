using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

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

        public bool IsRunning { get { return _isRunning; } }

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

            string clientIP = request.RemoteEndPoint.Address.ToString();
            // 将IPv6回环地址转换为IPv4，避免localStorage不共享问题
            if (clientIP == "::1")
            {
                clientIP = "127.0.0.1";
            }
            else if (clientIP.StartsWith("::ffff:"))
            {
                // IPv4映射的IPv6地址，转换为IPv4
                clientIP = clientIP.Substring(7);
            }
            string method = request.HttpMethod;
            string path = request.Url.AbsolutePath;

            if (_mainForm != null)
            {
                _mainForm.IncrementRequestCount();
                if (!path.StartsWith("/bg/") && !path.StartsWith("/css/") && !path.StartsWith("/js/"))
                {
                    _mainForm.LogMessage("[" + clientIP + "] " + method + " " + path);
                }
            }

            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

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
                    HandleStaticFileRequest(path, response, clientIP);
                }
            }
            catch (Exception ex)
            {
                if (_mainForm != null)
                    _mainForm.LogMessage("处理请求错误 [" + path + "]: " + ex.Message);
                response.StatusCode = 500;
                WriteResponse(response, "Error: " + ex.Message, "text/plain");
            }
        }

        private void HandleApiRequest(HttpListenerRequest request, HttpListenerResponse response, string clientIP)
        {
            string path = request.Url.AbsolutePath;
            string json = "";

            switch (path)
            {
                case "/api/login":
                    json = HandleLogin(request, clientIP);
                    break;
                case "/api/status":
                    var info = SystemManager.GetSystemInfo();
                    var explorerInfo = SystemManager.GetExplorerInfo();
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    json = "{\"success\": true, \"message\": \"获取成功\", \"data\": {\"systemInfo\": \"" + info + "\", \"explorerInfo\": \"" + explorerInfo + "\", \"timestamp\": \"" + timestamp + "\", \"status\": \"online\"}}";
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ 返回系统状态信息");
                    break;
                case "/api/restart/rdp":
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ 执行RDP服务重启操作...");
                    var rdpResult = SystemManager.RestartRDPService();
                    json = "{\"success\": true, \"message\": \"" + rdpResult + "\"}";
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ RDP操作结果: " + rdpResult);
                    break;
                case "/api/restart/explorer":
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ 执行Explorer重启操作...");
                    var expResult = SystemManager.RestartExplorer();
                    json = "{\"success\": true, \"message\": \"" + expResult + "\"}";
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ Explorer操作结果: " + expResult);
                    break;
                case "/api/restart/server":
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ 执行服务器重启操作...");
                    var srvResult = SystemManager.RestartServer();
                    json = "{\"success\": true, \"message\": \"" + srvResult + "\"}";
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ 服务器操作结果: " + srvResult);
                    break;
                default:
                    response.StatusCode = 404;
                    json = "{\"error\": \"Not Found\"}";
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ API未找到: " + path);
                    break;
            }

            WriteResponse(response, json, "application/json");
        }

        private string HandleLogin(HttpListenerRequest request, string clientIP)
        {
            try
            {
                string password = "";
                string body = "";
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    body = reader.ReadToEnd();
                }

                if (_mainForm != null)
                {
                    _mainForm.LogMessage("→ 收到登录请求 [" + clientIP + "]");
                    _mainForm.LogMessage("→ 请求体: [" + body + "]");
                    _mainForm.LogMessage("→ Content-Type: [" + request.ContentType + "]");
                    _mainForm.LogMessage("→ Content-Encoding: [" + request.ContentEncoding + "]");
                    _mainForm.LogMessage("→ 请求体长度: " + body.Length);
                    _mainForm.LogMessage("→ 请求体字节: " + BitConverter.ToString(System.Text.Encoding.UTF8.GetBytes(body)).Replace("-", ""));
                }

                // 支持表单格式: password=xxx
                if (body.StartsWith("password="))
                {
                    password = body.Substring(9);
                    password = System.Uri.UnescapeDataString(password);
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ 表单解析密码: [" + password + "]");
                }
                else
                {
                    // 健壮的JSON密码解析
                    password = ExtractPasswordFromJson(body);
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ JSON解析密码: [" + password + "]");
                }

                if (password == CORRECT_PASSWORD)
                {
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ 登录成功 [" + clientIP + "]");
                    return "{\"success\": true, \"message\": \"登录成功\"}";
                }
                else
                {
                    if (_mainForm != null)
                        _mainForm.LogMessage("→ 登录失败 [" + clientIP + "]: 密码不匹配, 期望长度:" + CORRECT_PASSWORD.Length + ", 实际长度:" + password.Length);
                    return "{\"success\": false, \"message\": \"密码错误\"}";
                }
            }
            catch (Exception ex)
            {
                if (_mainForm != null)
                    _mainForm.LogMessage("→ 登录处理错误: " + ex.Message);
                return "{\"success\": false, \"message\": \"验证失败\"}";
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

        private void HandleStaticFileRequest(string path, HttpListenerResponse response, string clientIP)
        {
            if (path == "/" || string.IsNullOrEmpty(path))
            {
                path = "/index.html";
            }

            string filePath = Path.Combine(_webRoot, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(filePath))
            {
                if (_mainForm != null && !path.StartsWith("/favicon"))
                    _mainForm.LogMessage("→ 文件未找到: " + path);
                response.StatusCode = 404;
                WriteResponse(response, "File not found", "text/plain");
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
