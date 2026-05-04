using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace RemoteRescueApp
{
    public static class ServiceManager
    {
        private const string TASK_NAME = "RemoteRescueTask";
        private const string TASK_DISPLAY_NAME = "远程救援系统";

        public static bool IsServiceInstalled()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = "/Query /TN " + TASK_NAME,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsServiceRunning()
        {
            // 对于任务计划程序，检查进程是否在运行
            try
            {
                var processes = Process.GetProcessesByName("RemoteRescueApp");
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public static string GetServiceStatus()
        {
            try
            {
                if (!IsServiceInstalled())
                {
                    return "未注册";
                }

                // 检查程序是否在运行
                if (IsServiceRunning())
                {
                    return "运行中";
                }
                else
                {
                    return "已停止";
                }
            }
            catch
            {
                return "未知";
            }
        }

        public static string InstallAndStartService()
        {
            try
            {
                if (IsServiceInstalled())
                {
                    // 任务已存在，尝试启动程序
                    if (!IsServiceRunning())
                    {
                        return StartProgramInternal();
                    }
                    return "程序已在运行";
                }

                string exePath = Application.ExecutablePath;

                // 使用 schtasks.exe 创建任务计划程序
                // 设置为系统启动时运行，无论用户是否登录，失败后重试3次
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = string.Format("/Create /TN {0} /TR \"{1}\" /SC ONSTART /RL HIGHEST /RU SYSTEM /NP /F", TASK_NAME, exePath),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        Verb = "runas"
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    return "注册任务失败: " + error;
                }

                // 立即运行任务
                return StartProgramInternal();
            }
            catch (Exception ex)
            {
                return "注册任务失败: " + ex.Message;
            }
        }

        public static string StopAndUninstallService()
        {
            try
            {
                if (!IsServiceInstalled())
                {
                    return "任务未安装";
                }

                // 先停止程序
                StopProgramInternal();

                // 删除任务
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = "/Delete /TN " + TASK_NAME + " /F",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        Verb = "runas"
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    return "卸载任务失败: " + error;
                }

                return "任务已卸载";
            }
            catch (Exception ex)
            {
                return "卸载任务失败: " + ex.Message;
            }
        }

        private static string StartProgramInternal()
        {
            try
            {
                if (IsServiceRunning())
                {
                    return "程序已在运行";
                }

                // 使用任务计划程序运行任务
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = "/Run /TN " + TASK_NAME,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        Verb = "runas"
                    }
                };

                process.Start();
                process.WaitForExit();

                return "程序已启动";
            }
            catch (Exception ex)
            {
                return "启动程序失败: " + ex.Message;
            }
        }

        private static string StopProgramInternal()
        {
            try
            {
                if (!IsServiceRunning())
                {
                    return "程序已停止";
                }

                // 结束进程（排除当前进程）
                var currentProcessId = Process.GetCurrentProcess().Id;
                var processes = Process.GetProcessesByName("RemoteRescueApp");
                foreach (var proc in processes)
                {
                    try
                    {
                        // 跳过当前进程，避免自杀
                        if (proc.Id == currentProcessId)
                        {
                            continue;
                        }
                        proc.Kill();
                        proc.WaitForExit(5000);
                    }
                    catch { }
                }

                return "程序已停止";
            }
            catch (Exception ex)
            {
                return "停止程序失败: " + ex.Message;
            }
        }

        public static void AddToStartupFolder()
        {
            try
            {
                string startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string shortcutPath = Path.Combine(startupPath, "远程救援系统.lnk");
                string exePath = Application.ExecutablePath;

                // 创建快捷方式
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                var shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                shortcut.Description = "远程救援系统";
                shortcut.Save();
            }
            catch { }
        }

        public static void RemoveFromStartupFolder()
        {
            try
            {
                string startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string shortcutPath = Path.Combine(startupPath, "远程救援系统.lnk");
                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }
            }
            catch { }
        }
    }
}
