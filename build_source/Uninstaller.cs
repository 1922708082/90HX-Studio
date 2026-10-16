using System.Security.Principal;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CMP90HXUninstaller
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = Process.GetCurrentProcess().MainModule.FileName,
                        Arguments = string.Join(" ", args),
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(startInfo);
                    return;
                }
            }
            catch (Exception)
            {
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            string mainExe = Path.Combine(appDir, "CMP 90HX 控制中心.exe");
            string altExe = Path.Combine(appDir, "CMP90HX_Toolbox.exe");

            bool hasRegistry = false;
            try
            {
                using (var k1 = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\CMP90HX_Toolbox"))
                    if (k1 != null) hasRegistry = true;
                using (var k2 = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\CMP90HX_Toolbox"))
                    if (k2 != null) hasRegistry = true;
            }
            catch { }

            bool hasFiles = File.Exists(mainExe) || File.Exists(altExe);

            if (!hasRegistry && !hasFiles)
            {
                MessageBox.Show(
                    "【防重复卸载检测】\n未检测到有效的 CMP 90HX 控制中心安装文件或注册表项！\n\n系统当前已处于干净状态，无需重复执行卸载。",
                    "无需卸载",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var res = MessageBox.Show(
                "确定要完全卸载【CMP 90HX 控制中心】及其所有功能组件吗？\n\n卸载程序将自动清理所有快捷方式、注册表项及安装文件。",
                "确认卸载 - CMP 90HX 控制中心",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res != DialogResult.Yes) return;

            try
            {
                // 使用已解析的安装根目录 appDir

                // 1. 检查并清理 BCD 引导，防止小白残留
                try
                {
                    var psiBcd = new ProcessStartInfo
                    {
                        FileName = "bcdedit.exe",
                        Arguments = "/set {bootmgr} path \\EFI\\Microsoft\\Boot\\bootmgfw.efi",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using (var p = Process.Start(psiBcd)) p.WaitForExit(2000);
                }
                catch { }

                // 2. 清理桌面快捷方式
                try
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    string[] deskShortcuts = new string[] {
                        Path.Combine(desktop, "CMP 90HX 控制中心.lnk"),
                        Path.Combine(desktop, "CMP 90HX 极速调优控制中心.lnk")
                    };
                    foreach (var s in deskShortcuts)
                    {
                        if (File.Exists(s)) File.Delete(s);
                    }
                }
                catch { }

                // 3. 清理开始菜单
                try
                {
                    string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "CMP 90HX 控制中心");
                    if (Directory.Exists(startMenu)) Directory.Delete(startMenu, true);

                    string userStartMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "CMP 90HX 控制中心");
                    if (Directory.Exists(userStartMenu)) Directory.Delete(userStartMenu, true);
                }
                catch { }

                // 4. 清理 Windows 控制面板卸载注册表
                try
                {
                    Registry.LocalMachine.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\CMP90HX_Toolbox", false);
                }
                catch { }
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\CMP90HX_Toolbox", false);
                }
                catch { }

                // 4.5 清理统一数据目录 (%ProgramData%\CMP90HX_Toolbox 与 %LocalAppData%\CMP90HX_Toolbox)
                try
                {
                    string commonData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CMP90HX_Toolbox");
                    if (Directory.Exists(commonData)) Directory.Delete(commonData, true);
                }
                catch { }
                try
                {
                    string localData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CMP90HX_Toolbox");
                    if (Directory.Exists(localData)) Directory.Delete(localData, true);
                }
                catch { }

                MessageBox.Show(
                    "CMP 90HX 控制中心 已成功从您的电脑中卸载！\n\n系统引导与快捷方式已恢复官方原始状态。",
                    "卸载成功",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // 5. 延迟自删安装目录
                string selfExe = Process.GetCurrentProcess().MainModule.FileName;
                string batCmd = "ping 127.0.0.1 -n 2 >nul & rmdir /s /q \"" + appDir + "\"";
                var psiSelfDelete = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c " + batCmd,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true
                };
                Process.Start(psiSelfDelete);
            }
            catch (Exception ex)
            {
                MessageBox.Show("卸载过程中发生错误: " + ex.Message, "卸载提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
