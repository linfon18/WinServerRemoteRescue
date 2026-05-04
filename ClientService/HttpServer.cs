using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RemoteRescueService
{
    public class HttpServer
    {
        private HttpListener _listener;
        private Thread _listenerThread;
        private bool _isRunning;
        private int _port = 8877;

        public void Start()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://*:{_port}/");
            _listener.Start();
            _isRunning = true;

            _listenerThread = new Thread(new ThreadStart(Listen));
            _listenerThread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
            _listener?.Close();
        }

        private void Listen()
        {
            while (_isRunning)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(new WaitCallback(ProcessRequest), context);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.EventLog.WriteEntry("RemoteRescueService", $"HTTP监听错误: {ex.Message}");
                }
            }
        }

        private void ProcessRequest(object state)
        {
            var context = (HttpListenerContext)state;
            var request = context.Request;
            var response = context.Response;

            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            try
            {
                string path = request.Url.AbsolutePath;
                string method = request.HttpMethod;

                switch (path)
                {
                    case "/api/status":
                        HandleStatusRequest(response);
                        break;
                    case "/api/restart/rdp":
                        HandleRestartRDP(response);
                        break;
                    case "/api/restart/explorer":
                        HandleRestartExplorer(response);
                        break;
                    case "/api/restart/server":
                        HandleRestartServer(response);
                        break;
                    default:
                        response.StatusCode = 404;
                        WriteResponse(response, "{\"error\": \"Not Found\"}");
                        break;
                }
            }
            catch (Exception ex)
            {
                response.StatusCode = 500;
                WriteResponse(response, $"{{\"error\": \"{ex.Message}\"}}");
            }
        }

        private void HandleStatusRequest(HttpListenerResponse response)
        {
            var info = SystemManager.GetSystemInfo();
            var result = new JObject
            {
                ["success"] = true,
                ["message"] = "获取成功",
                ["data"] = new JObject
                {
                    ["systemInfo"] = info,
                    ["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["status"] = "online"
                }
            };
            WriteResponse(response, result.ToString());
        }

        private void HandleRestartRDP(HttpListenerResponse response)
        {
            var result = SystemManager.RestartRDPService();
            var json = new JObject
            {
                ["success"] = true,
                ["message"] = result
            };
            WriteResponse(response, json.ToString());
        }

        private void HandleRestartExplorer(HttpListenerResponse response)
        {
            var result = SystemManager.RestartExplorer();
            var json = new JObject
            {
                ["success"] = true,
                ["message"] = result
            };
            WriteResponse(response, json.ToString());
        }

        private void HandleRestartServer(HttpListenerResponse response)
        {
            var result = SystemManager.RestartServer();
            var json = new JObject
            {
                ["success"] = true,
                ["message"] = result
            };
            WriteResponse(response, json.ToString());
        }

        private void WriteResponse(HttpListenerResponse response, string content)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }
    }
}
