using System;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Windows.Forms;

namespace RemoteRescueApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // 检查是否以管理员身份运行
            if (!IsRunAsAdmin())
            {
                // 重新以管理员身份启动
                var processInfo = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location)
                {
                    Verb = "runas",
                    UseShellExecute = true
                };

                try
                {
                    Process.Start(processInfo);
                }
                catch
                {
                    MessageBox.Show("请以管理员身份运行此程序", "需要管理员权限", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static bool IsRunAsAdmin()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
