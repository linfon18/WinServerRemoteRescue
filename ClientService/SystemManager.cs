using System;
using System.Diagnostics;
using System.Management;
using System.ServiceProcess;

namespace RemoteRescueService
{
    public class SystemManager
    {
        public static string GetSystemInfo()
        {
            try
            {
                var osInfo = Environment.OSVersion;
                var machineName = Environment.MachineName;
                var userName = Environment.UserName;
                var processorCount = Environment.ProcessorCount;
                var uptime = GetSystemUptime();
                var memoryInfo = GetMemoryInfo();

                return $"计算机名: {machineName} | 用户名: {userName} | 操作系统: {osInfo.VersionString} | 处理器: {processorCount}核 | 运行时间: {uptime} | 内存: {memoryInfo}";
            }
            catch (Exception ex)
            {
                return $"获取系统信息失败: {ex.Message}";
            }
        }

        private static string GetSystemUptime()
        {
            try
            {
                using (var uptime = new PerformanceCounter("System", "System Up Time"))
                {
                    uptime.NextValue();
                    var timeSpan = TimeSpan.FromSeconds(uptime.NextValue());
                    return $"{timeSpan.Days}天 {timeSpan.Hours}小时 {timeSpan.Minutes}分钟";
                }
            }
            catch
            {
                return "未知";
            }
        }

        private static string GetMemoryInfo()
        {
            try
            {
                var gcMemory = GC.GetTotalMemory(false) / 1024 / 1024;
                return $"GC内存: {gcMemory}MB";
            }
            catch
            {
                return "未知";
            }
        }

        public static string RestartRDPService()
        {
            try
            {
                var service = new ServiceController("TermService");
                if (service.Status == ServiceControllerStatus.Running)
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                return "RDP服务重启成功";
            }
            catch (Exception ex)
            {
                return $"RDP服务重启失败: {ex.Message}";
            }
        }

        public static string RestartExplorer()
        {
            try
            {
                var processes = Process.GetProcessesByName("explorer");
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                    catch { }
                }

                System.Threading.Thread.Sleep(1000);

                Process.Start("explorer.exe");
                return "Explorer重启成功";
            }
            catch (Exception ex)
            {
                return $"Explorer重启失败: {ex.Message}";
            }
        }

        public static string RestartServer()
        {
            try
            {
                var psi = new ProcessStartInfo("shutdown", "/r /t 10 /c \"远程救援系统重启服务器\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
                return "服务器将在10秒后重启";
            }
            catch (Exception ex)
            {
                return $"服务器重启命令执行失败: {ex.Message}";
            }
        }
    }
}
