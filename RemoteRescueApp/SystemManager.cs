using System;
using System.Diagnostics;
using System.Management;
using System.ServiceProcess;
using System.Threading;

namespace RemoteRescueApp
{
    public class SystemManager
    {
        public static string GetSystemInfo()
        {
            try
            {
                var machineName = Environment.MachineName;
                var userName = Environment.UserName;
                var osVersion = GetOSVersion();
                var processorCount = Environment.ProcessorCount;
                var uptime = GetSystemUptime();

                return string.Format("计算机名: {0} | 用户名: {1} | 操作系统: {2} | 处理器: {3}核 | 运行时间: {4}",
                    machineName, userName, osVersion, processorCount, uptime);
            }
            catch (Exception ex)
            {
                return "获取系统信息失败: " + ex.Message;
            }
        }

        private static string GetOSVersion()
        {
            try
            {
                string osName = "Unknown";
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject os in searcher.Get())
                    {
                        osName = os["Caption"] != null ? os["Caption"].ToString() : "Unknown";
                        string version = os["Version"] != null ? os["Version"].ToString() : "";
                        if (!string.IsNullOrEmpty(version))
                        {
                            osName += " " + version;
                        }
                    }
                }
                return osName.Trim();
            }
            catch
            {
                return Environment.OSVersion.VersionString;
            }
        }

        private static string GetSystemUptime()
        {
            try
            {
                using (var uptime = new PerformanceCounter("System", "System Up Time"))
                {
                    uptime.NextValue();
                    Thread.Sleep(100);
                    var timeSpan = TimeSpan.FromSeconds(uptime.NextValue());
                    return string.Format("{0}天 {1}小时 {2}分钟", timeSpan.Days, timeSpan.Hours, timeSpan.Minutes);
                }
            }
            catch
            {
                try
                {
                    var tickCount = Environment.TickCount;
                    var timeSpan = TimeSpan.FromMilliseconds(tickCount);
                    return string.Format("{0}天 {1}小时 {2}分钟", timeSpan.Days, timeSpan.Hours, timeSpan.Minutes);
                }
                catch
                {
                    return "未知";
                }
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
                return "RDP服务重启失败: " + ex.Message;
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

                Thread.Sleep(1000);
                Process.Start("explorer.exe");
                return "Explorer重启成功";
            }
            catch (Exception ex)
            {
                return "Explorer重启失败: " + ex.Message;
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
                return "服务器重启命令执行失败: " + ex.Message;
            }
        }

        public static string GetExplorerInfo()
        {
            try
            {
                var processes = Process.GetProcessesByName("explorer");
                if (processes.Length == 0)
                {
                    return "Explorer.exe: 未运行";
                }

                var process = processes[0];
                long memoryBytes = process.WorkingSet64;
                double memoryMB = memoryBytes / (1024.0 * 1024.0);

                // 获取CPU占用（需要两次采样）
                var startTime = DateTime.UtcNow;
                var startCpuUsage = process.TotalProcessorTime;
                Thread.Sleep(500);
                var endTime = DateTime.UtcNow;
                var endCpuUsage = process.TotalProcessorTime;

                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                var cpuUsagePercent = cpuUsageTotal * 100;

                return string.Format("Explorer.exe | PID: {0} | 内存: {1:F1} MB | CPU: {2:F1}%",
                    process.Id, memoryMB, cpuUsagePercent);
            }
            catch (Exception ex)
            {
                return "Explorer.exe: 获取信息失败 - " + ex.Message;
            }
        }
    }
}
