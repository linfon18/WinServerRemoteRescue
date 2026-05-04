using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RemoteRescueApp
{
    public partial class MainForm : Form
    {
        private HttpServer _httpServer;
        private NotifyIcon _notifyIcon;
        private bool _allowClose = false;
        private int _requestCount = 0;
        private DateTime _startTime;
        private Timer _serviceStatusTimer;
        private string _logFilePath;

        public MainForm()
        {
            _logFilePath = Path.Combine(Application.StartupPath, "self.log");
            InitializeComponent();
            InitializeNotifyIcon();
            InitializeServer();
            StartServiceStatusTimer();
        }

        private void InitializeComponent()
        {
            this.Text = "远程救援系统 v1.0";
            this.Size = new Size(700, 550);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormClosing += MainForm_FormClosing;
            this.Resize += MainForm_Resize;
            this.BackColor = Color.FromArgb(245, 246, 247);

            // 标题栏
            var titlePanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(0, 122, 204)
            };

            var titleLabel = new Label
            {
                Text = "远程救援系统",
                Font = new Font("Microsoft YaHei", 16, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 15)
            };
            titlePanel.Controls.Add(titleLabel);

            // HTTP服务状态面板
            var httpStatusPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                Padding = new Padding(15),
                BackColor = Color.White
            };

            var httpStatusGroup = new GroupBox
            {
                Text = "HTTP服务状态",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 9)
            };

            var statusLabel = new Label
            {
                Text = "● 停止",
                Font = new Font("Microsoft YaHei", 12, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(15, 25),
                ForeColor = Color.Red,
                Name = "statusLabel"
            };

            var urlLabel = new Label
            {
                Text = "访问地址: IP:8889",
                Font = new Font("Consolas", 10),
                AutoSize = true,
                Location = new Point(15, 55),
                ForeColor = Color.Gray
            };

            httpStatusGroup.Controls.AddRange(new Control[] { statusLabel, urlLabel });
            httpStatusPanel.Controls.Add(httpStatusGroup);

            // 系统服务状态面板
            var serviceStatusPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                Padding = new Padding(15),
                BackColor = Color.White
            };

            var serviceStatusGroup = new GroupBox
            {
                Text = "系统服务状态（开机自启/未登录启动）",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 9)
            };

            var serviceStatusLabel = new Label
            {
                Text = "● 未注册",
                Font = new Font("Microsoft YaHei", 12, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(15, 25),
                ForeColor = Color.Gray,
                Name = "serviceStatusLabel"
            };

            var serviceDescLabel = new Label
            {
                Text = "注册后系统服务将在开机时自动启动，无需登录",
                Font = new Font("Microsoft YaHei", 9),
                AutoSize = true,
                Location = new Point(15, 55),
                ForeColor = Color.Gray
            };

            serviceStatusGroup.Controls.AddRange(new Control[] { serviceStatusLabel, serviceDescLabel });
            serviceStatusPanel.Controls.Add(serviceStatusGroup);

            // 统计面板
            var statsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                Padding = new Padding(15),
                BackColor = Color.White
            };

            var statsGroup = new GroupBox
            {
                Text = "运行统计",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 9)
            };

            var requestLabel = new Label
            {
                Text = "请求次数: 0",
                Font = new Font("Microsoft YaHei", 10),
                AutoSize = true,
                Location = new Point(15, 25),
                Name = "requestLabel"
            };

            var uptimeLabel = new Label
            {
                Text = "运行时长: 00:00:00",
                Font = new Font("Microsoft YaHei", 10),
                AutoSize = true,
                Location = new Point(200, 25),
                Name = "uptimeLabel"
            };

            statsGroup.Controls.AddRange(new Control[] { requestLabel, uptimeLabel });
            statsPanel.Controls.Add(statsGroup);

            // 按钮面板
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 120,
                Padding = new Padding(15, 10, 15, 10),
                BackColor = Color.White
            };

            // HTTP服务按钮
            var btnStart = new Button
            {
                Text = "▶ 启动HTTP",
                Size = new Size(110, 35),
                Location = new Point(15, 15),
                Font = new Font("Microsoft YaHei", 9),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 150, 136),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.Click += (s, e) => StartServer();
            btnStart.Name = "btnStart";

            var btnStop = new Button
            {
                Text = "■ 停止HTTP",
                Size = new Size(110, 35),
                Location = new Point(135, 15),
                Font = new Font("Microsoft YaHei", 9),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(244, 67, 54),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnStop.FlatAppearance.BorderSize = 0;
            btnStop.Click += (s, e) => StopServer();
            btnStop.Name = "btnStop";

            var btnOpen = new Button
            {
                Text = "🌐 打开网页",
                Size = new Size(110, 35),
                Location = new Point(255, 15),
                Font = new Font("Microsoft YaHei", 9),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(33, 150, 243),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnOpen.FlatAppearance.BorderSize = 0;
            btnOpen.Click += (s, e) => OpenWebPage();

            var btnViewLog = new Button
            {
                Text = "� 查看日志",
                Size = new Size(110, 35),
                Location = new Point(375, 15),
                Font = new Font("Microsoft YaHei", 9),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(158, 158, 158),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnViewLog.FlatAppearance.BorderSize = 0;
            btnViewLog.Click += (s, e) => ViewLogFile();

            // 系统服务按钮（自动切换注册/卸载）
            var btnService = new Button
            {
                Text = "📦 注册服务",
                Size = new Size(150, 35),
                Location = new Point(15, 65),
                Font = new Font("Microsoft YaHei", 9),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(156, 39, 176),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Name = "btnService"
            };
            btnService.FlatAppearance.BorderSize = 0;
            btnService.Click += (s, e) => ToggleSystemService();

            buttonPanel.Controls.AddRange(new Control[] { 
                btnStart, btnStop, btnOpen, btnViewLog, btnService
            });

            // 状态栏
            var statusStrip = new StatusStrip
            {
                Dock = DockStyle.Bottom
            };
            var statusItem = new ToolStripStatusLabel
            {
                Text = "就绪",
                Name = "statusStripLabel"
            };
            statusStrip.Items.Add(statusItem);

            // 添加所有面板
            this.Controls.Add(buttonPanel);
            this.Controls.Add(statsPanel);
            this.Controls.Add(serviceStatusPanel);
            this.Controls.Add(httpStatusPanel);
            this.Controls.Add(titlePanel);
            this.Controls.Add(statusStrip);

            // 启动计时器
            var timer = new Timer();
            timer.Interval = 1000;
            timer.Tick += (s, e) => UpdateUptime();
            timer.Start();
        }

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Shield,
                Text = "远程救援系统",
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("打开主窗口", null, (s, e) => ShowForm());
            contextMenu.Items.Add("打开网页", null, (s, e) => OpenWebPage());
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("退出程序", null, (s, e) => ExitApplication());

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => ShowForm();
        }

        private void InitializeServer()
        {
            _httpServer = new HttpServer(this);
            LogMessage("系统初始化完成，等待启动服务...");
            LogMessage("Web资源路径: " + Path.Combine(Application.StartupPath, "WebResource"));
            
            // 自动启动HTTP服务
            AutoStartHttpServer();
        }

        private async void AutoStartHttpServer()
        {
            try
            {
                LogMessage("正在自动启动HTTP服务...");
                await Task.Delay(500); // 短暂延迟确保UI初始化完成
                
                if (!_httpServer.IsRunning)
                {
                    StartServer();
                }
            }
            catch (Exception ex)
            {
                LogMessage("自动启动HTTP服务失败: " + ex.Message);
            }
        }

        private void StartServiceStatusTimer()
        {
            _serviceStatusTimer = new Timer();
            _serviceStatusTimer.Interval = 2000;
            _serviceStatusTimer.Tick += (s, e) => UpdateServiceStatus();
            _serviceStatusTimer.Start();
            UpdateServiceStatus();
        }

        private void UpdateServiceStatus()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(UpdateServiceStatus));
                return;
            }

            var serviceStatusLabel = this.Controls.Find("serviceStatusLabel", true)[0] as Label;
            var btnService = this.Controls.Find("btnService", true)[0] as Button;
            
            if (serviceStatusLabel != null)
            {
                string status = ServiceManager.GetServiceStatus();
                serviceStatusLabel.Text = "● " + status;
                
                switch (status)
                {
                    case "运行中":
                        serviceStatusLabel.ForeColor = Color.Green;
                        break;
                    case "已停止":
                        serviceStatusLabel.ForeColor = Color.Orange;
                        break;
                    case "未注册":
                        serviceStatusLabel.ForeColor = Color.Gray;
                        break;
                    default:
                        serviceStatusLabel.ForeColor = Color.Red;
                        break;
                }
            }

            // 更新按钮文本和颜色
            if (btnService != null)
            {
                bool isInstalled = ServiceManager.IsServiceInstalled();
                if (isInstalled)
                {
                    btnService.Text = "🗑️ 卸载服务";
                    btnService.BackColor = Color.FromArgb(121, 85, 72);
                }
                else
                {
                    btnService.Text = "📦 注册服务";
                    btnService.BackColor = Color.FromArgb(156, 39, 176);
                }
            }
        }

        private async void ToggleSystemService()
        {
            var btnService = this.Controls.Find("btnService", true)[0] as Button;
            if (btnService != null)
            {
                btnService.Enabled = false;
            }

            bool isInstalled = ServiceManager.IsServiceInstalled();
            
            if (isInstalled)
            {
                // 卸载服务
                if (MessageBox.Show("确定要卸载系统服务吗？卸载后将不再开机自启。", "确认卸载", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    LogMessage("正在卸载系统服务...");
                    
                    string result = await Task.Run(() => ServiceManager.StopAndUninstallService());
                    LogMessage("→ " + result);
                    
                    // 同时从启动文件夹移除
                    ServiceManager.RemoveFromStartupFolder();
                    LogMessage("→ 已从启动文件夹移除");
                    
                    UpdateServiceStatus();
                }
            }
            else
            {
                // 注册服务
                LogMessage("正在注册系统服务...");
                
                string result = await Task.Run(() => ServiceManager.InstallAndStartService());
                LogMessage("→ " + result);
                
                // 同时添加到启动文件夹
                ServiceManager.AddToStartupFolder();
                LogMessage("→ 已添加到启动文件夹");
                
                UpdateServiceStatus();
            }

            if (btnService != null)
            {
                btnService.Enabled = true;
            }
        }

        private void StartServer()
        {
            try
            {
                LogMessage("正在启动HTTP服务...");
                _httpServer.Start();
                _startTime = DateTime.Now;
                _requestCount = 0;
                UpdateUI(true);
                LogMessage("✓ HTTP服务启动成功");
                LogMessage("✓ 监听地址: IP:8889 (所有网卡)");
                LogMessage("✓ 请使用浏览器访问上述地址");
                UpdateStatusStrip("服务运行中 - 端口 8889");
            }
            catch (Exception ex)
            {
                LogMessage("✗ 启动服务失败: " + ex.Message);
                UpdateStatusStrip("启动失败");
                MessageBox.Show("启动服务失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopServer()
        {
            LogMessage("正在停止HTTP服务...");
            _httpServer.Stop();
            UpdateUI(false);
            LogMessage("✓ HTTP服务已停止");
            UpdateStatusStrip("服务已停止");
        }

        private void ViewLogFile()
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    Process.Start(_logFilePath);
                }
                else
                {
                    MessageBox.Show("日志文件尚未创建", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法打开日志文件: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateUI(bool running)
        {
            var statusLabel = this.Controls.Find("statusLabel", true)[0] as Label;
            var btnStart = this.Controls.Find("btnStart", true)[0] as Button;
            var btnStop = this.Controls.Find("btnStop", true)[0] as Button;

            if (statusLabel != null)
            {
                statusLabel.Text = running ? "● 运行中" : "● 停止";
                statusLabel.ForeColor = running ? Color.Green : Color.Red;
            }

            if (btnStart != null) btnStart.Enabled = !running;
            if (btnStop != null) btnStop.Enabled = running;
        }

        private void UpdateUptime()
        {
            if (!_httpServer.IsRunning) return;

            var uptime = DateTime.Now - _startTime;
            var uptimeLabel = this.Controls.Find("uptimeLabel", true)[0] as Label;
            if (uptimeLabel != null)
            {
                uptimeLabel.Text = "运行时长: " + uptime.ToString("hh\\:mm\\:ss");
            }
        }

        public void IncrementRequestCount()
        {
            _requestCount++;
            var requestLabel = this.Controls.Find("requestLabel", true)[0] as Label;
            if (requestLabel != null)
            {
                requestLabel.Text = "请求次数: " + _requestCount;
            }
        }

        private void UpdateStatusStrip(string message)
        {
            var statusStrip = this.Controls.Find("statusStrip", true);
            if (statusStrip.Length > 0)
            {
                var strip = statusStrip[0] as StatusStrip;
                var label = strip.Items[0] as ToolStripStatusLabel;
                if (label != null) label.Text = message;
            }
        }

        public void UpdateStatus(string message, bool running)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string, bool>(UpdateStatus), message, running);
                return;
            }
            UpdateUI(running);
        }

        public void LogMessage(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string logLine = "[" + timestamp + "] " + message + Environment.NewLine;
                File.AppendAllText(_logFilePath, logLine);
            }
            catch { }
        }

        private void OpenWebPage()
        {
            LogMessage("正在打开浏览器...");
            Process.Start("http://localhost:8889");
        }

        private void ShowForm()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        }

        private void ExitApplication()
        {
            _allowClose = true;
            LogMessage("正在退出程序...");
            _httpServer.Stop();
            if (_serviceStatusTimer != null)
            {
                _serviceStatusTimer.Stop();
                _serviceStatusTimer.Dispose();
            }
            _notifyIcon.Dispose();
            Application.Exit();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                this.Hide();
                _notifyIcon.ShowBalloonTip(2000, "远程救援系统", "程序已最小化到系统托盘", ToolTipIcon.Info);
                LogMessage("程序已最小化到系统托盘");
            }
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
            }
        }
    }
}
