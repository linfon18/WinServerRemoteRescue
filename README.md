# 远程救援系统 (Remote Rescue)

一个用于紧急远程救援的管理系统，合并为单一本地应用程序。

## 项目结构

```
RemoteRescue/
├── RemoteRescueApp/              # 合并后的单一应用程序 (.NET Framework 4.7.2)
│   ├── RemoteRescueApp.csproj
│   ├── App.config
│   ├── Program.cs                # 程序入口
│   ├── MainForm.cs               # 主窗口（带系统托盘）
│   ├── HttpServer.cs             # 内置HTTP服务器
│   ├── SystemManager.cs          # 系统管理功能
│   ├── ServiceManager.cs         # 系统服务管理（任务计划程序）
│   ├── Properties/
│   └── WebResource/              # Web资源文件夹
│       ├── index.html            # 登录页面
│       ├── main.html             # 主控制台
│       ├── css/
│       │   └── style.css
│       ├── js/
│       │   ├── auth.js           # 登录逻辑
│       │   ├── auth-check.js     # 登录验证（防绕过）
│       │   ├── bg-random.js      # 随机背景
│       │   └── main.js           # 主页面逻辑
│       └── bg/                   # 背景图片文件夹
│           ├── bg-1.jpg
│           ├── bg-2.jpg
│           ├── bg-3.jpg
│           ├── bg-4.mp4
│           └── bg-5.mp4
│
├── ClientService/                # Windows服务模式（备用）
│   ├── RemoteRescueService.csproj
│   ├── App.config
│   ├── Program.cs
│   ├── RescueService.cs          # Windows服务主类
│   ├── HttpServer.cs
│   ├── SystemManager.cs
│   └── Properties/
│
└── publish/                      # 发布输出目录
    ├── RemoteRescueApp.exe
    ├── WebResource/
    └── README.txt
```

## 功能特性

### 单一应用程序
- **WinForms界面**：带系统托盘图标，可最小化到托盘
- **内置HTTP服务器**：监听端口 **8889**，无需额外部署
- **自动启动**：程序启动时自动启动HTTP服务
- **系统服务集成**：支持注册为系统任务计划程序，开机自启（无需登录）

### Web端功能
- **身份认证**：统一密码 `12345`，防绕过验证
- **随机背景**：每次刷新从 `bg` 文件夹随机选择背景图片
- **实时信息**：显示计算机名、用户名、操作系统、处理器、运行时间等
- **Explorer监控**：实时显示 Explorer.exe 的 PID、内存占用和 CPU 占用
- **三大救援功能**：
  - 重启RDP服务
  - 重启Explorer
  - 重启服务器

### 系统服务管理
- **注册服务**：将程序添加到系统任务计划程序，开机自动运行
- **卸载服务**：从任务计划程序中移除
- **启动文件夹**：同时添加到用户启动文件夹
- **状态监控**：实时显示服务注册状态和运行状态

## 使用说明

### 1. 编译运行

#### 使用Visual Studio
1. 打开 `RemoteRescueApp/RemoteRescueApp.csproj`
2. 编译并运行

#### 命令行编译
```cmd
cd RemoteRescueApp
msbuild RemoteRescueApp.csproj /p:Configuration=Release
```

### 2. 运行程序

1. 运行 `RemoteRescueApp.exe`
2. 程序会自动启动HTTP服务（端口 **8889**）
3. 点击"打开网页"或浏览器访问 `http://localhost:8889`
4. 输入密码 `12345` 登录

### 3. 系统服务注册（可选）

点击"注册服务"按钮：
- 将程序注册到系统任务计划程序
- 设置为系统启动时自动运行（无论用户是否登录）
- 使用 SYSTEM 账户运行，具有最高权限
- 同时添加到用户启动文件夹

### 4. 自定义背景图片

将图片放入 `WebResource/bg/` 文件夹：
- 支持格式：jpg, jpeg, png, mp4（视频背景）
- 建议尺寸：1920x1080 或更大
- 命名格式：`bg-1.jpg`, `bg-2.jpg`, `bg-3.jpg`...

修改 `WebResource/js/bg-random.js` 中的 `BG_IMAGES` 数组添加新图片：
```javascript
const BG_IMAGES = [
    'bg/bg-1.jpg',
    'bg/bg-2.jpg',
    'bg/bg-3.jpg',
    'bg/你的新图片.jpg'
];
```

### 5. 系统托盘

- 关闭窗口时程序最小化到系统托盘
- 双击托盘图标打开窗口
- 右键托盘图标可操作：打开/打开网页/退出

### 6. 查看日志

点击"查看日志"按钮可打开 `self.log` 文件，查看程序运行日志。

## API接口

### 获取系统状态
```
GET http://localhost:8889/api/status
```

### 重启RDP服务
```
POST http://localhost:8889/api/restart/rdp
```

### 重启Explorer
```
POST http://localhost:8889/api/restart/explorer
```

### 重启服务器
```
POST http://localhost:8889/api/restart/server
```

## 安全说明

⚠️ **重要提示**：
- 默认密码为 `12345`，生产环境请修改 `HttpServer.cs` 中的 `CORRECT_PASSWORD`
- 登录验证采用 sessionStorage + 后端验证，防止直接访问main.html绕过登录
- 每次页面刷新会重新随机选择背景图片
- 服务运行在本地端口 **8889**，请确保网络安全

## 防火墙配置

如需外部访问，开放端口8889：
```cmd
netsh advfirewall firewall add rule name="RemoteRescue" dir=in action=allow protocol=tcp localport=8889
```

## 技术栈

- **应用程序**：.NET Framework 4.7.2, C#, WinForms
- **Web端**：HTML5, CSS3 (毛玻璃效果), Vanilla JavaScript
- **通信**：内置HttpListener
- **系统服务**：Windows Task Scheduler (schtasks.exe)

## 浏览器兼容性

- Chrome 80+
- Firefox 75+
- Edge 80+
- Safari 13+

## GitHub

https://github.com/linfon18/WinServerRemoteRescue.git

## 许可证

MIT License
