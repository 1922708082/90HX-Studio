using System.Security.Principal;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;

namespace CMP90HXInstaller
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            // 运行时提权检查：如果不是管理员，自动使用 runas 触发 UAC 提权
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = Process.GetCurrentProcess().MainModule.FileName,
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(startInfo);
                    return;
                }
            }
            catch (Exception)
            {
                // 如果用户拒绝了 UAC 弹窗，安全退出
                return;
            }
            var app = new Application();
            Window window = null;

            Stream inStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SetupWindow.xaml");
            if (inStream == null)
            {
                string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"build_source\src\SetupWindow.xaml");
                if (File.Exists(local)) inStream = File.OpenRead(local);
            }

            if (inStream == null)
            {
                MessageBox.Show("缺失安装向导资源 SetupWindow.xaml", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using (inStream)
            {
                window = (Window)XamlReader.Load(inStream);
            }

            var txtPath = (TextBox)window.FindName("TxtInstallPath");
            var btnBrowse = (Button)window.FindName("BtnBrowse");
            var chkDesktop = (CheckBox)window.FindName("ChkDesktopShortcut");
            var chkStartMenu = (CheckBox)window.FindName("ChkStartMenu");
            var chkLaunch = (CheckBox)window.FindName("ChkLaunchNow");
            var pBar = (ProgressBar)window.FindName("ProgressBarInstall");
            var txtStatus = (TextBlock)window.FindName("TxtStatus");
            var btnInstall = (Button)window.FindName("BtnInstall");
            var btnCancel = (Button)window.FindName("BtnCancel");

            btnCancel.Click += (s, e) => window.Close();

            btnBrowse.Click += (s, e) =>
            {
                using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                {
                    fbd.Description = "请选择 CMP 90HX 控制中心安装路径";
                    if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        txtPath.Text = fbd.SelectedPath;
                    }
                }
            };

            btnInstall.Click += (s, e) =>
            {
                string targetDir = txtPath.Text.Trim();
                if (string.IsNullOrEmpty(targetDir))
                {
                    MessageBox.Show("请指定有效的安装目录！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 防重复安装检测
                string existingExe = Path.Combine(targetDir, "CMP 90HX 控制中心.exe");
                string altExe = Path.Combine(targetDir, "CMP90HX_Toolbox.exe");
                if (File.Exists(existingExe) || File.Exists(altExe))
                {
                    var confirm = MessageBox.Show(
                        "【防重复安装检测】\n检测到目标路径中已安装有 CMP 90HX 控制中心！\n\n安装路径: " + targetDir + "\n\n是否确认覆盖升级并重新部署最新核心组件？",
                        "检测到已安装版本",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (confirm != MessageBoxResult.Yes) return;
                }

                btnInstall.IsEnabled = false;
                btnCancel.IsEnabled = false;
                btnBrowse.IsEnabled = false;
                txtPath.IsEnabled = false;
                chkDesktop.IsEnabled = false;
                chkStartMenu.IsEnabled = false;
                chkLaunch.IsEnabled = false;

                pBar.Visibility = Visibility.Visible;
                pBar.IsIndeterminate = true;
                txtStatus.Text = "正在解包并部署文件...";

                bool createDesktop = chkDesktop.IsChecked == true;
                bool createStart = chkStartMenu.IsChecked == true;
                bool launchNow = chkLaunch.IsChecked == true;

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                        var asm = Assembly.GetExecutingAssembly();

                        // 1. 释放主程序
                        string mainExe = Path.Combine(targetDir, "CMP 90HX 控制中心.exe");
                        ExtractResource(asm, "app.bin", mainExe);

                        // 2. 释放卸载程序
                        string uninstExe = Path.Combine(targetDir, "uninstall.exe");
                        ExtractResource(asm, "uninstall.bin", uninstExe);

                        // 3. 释放图标与说明
                        string icoPath = Path.Combine(targetDir, "app.ico");
                        ExtractResource(asm, "app.ico", icoPath);

                        string readmePath = Path.Combine(targetDir, "使用说明.txt");
                        ExtractResource(asm, "readme.txt", readmePath);

                        // 4. 创建快捷方式
                        try
                        {
                            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                            if (shellType != null)
                            {
                                dynamic shell = Activator.CreateInstance(shellType);
                                if (createDesktop)
                                {
                                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                                    string deskLnk = Path.Combine(desktop, "CMP 90HX 控制中心.lnk");
                                    dynamic sc = shell.CreateShortcut(deskLnk);
                                    sc.TargetPath = mainExe;
                                    sc.WorkingDirectory = targetDir;
                                    sc.Description = "CMP 90HX 极速调优控制中心";
                                    if (File.Exists(icoPath)) sc.IconLocation = icoPath + ",0";
                                    sc.Save();
                                }

                                if (createStart)
                                {
                                    string startPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "CMP 90HX 控制中心");
                                    if (!Directory.Exists(startPrograms)) Directory.CreateDirectory(startPrograms);

                                    string menuLnk = Path.Combine(startPrograms, "CMP 90HX 控制中心.lnk");
                                    dynamic scMenu = shell.CreateShortcut(menuLnk);
                                    scMenu.TargetPath = mainExe;
                                    scMenu.WorkingDirectory = targetDir;
                                    scMenu.Description = "CMP 90HX 极速调优控制中心";
                                    if (File.Exists(icoPath)) scMenu.IconLocation = icoPath + ",0";
                                    scMenu.Save();

                                    string uninstLnk = Path.Combine(startPrograms, "卸载 CMP 90HX 控制中心.lnk");
                                    dynamic scUninst = shell.CreateShortcut(uninstLnk);
                                    scUninst.TargetPath = uninstExe;
                                    scUninst.WorkingDirectory = targetDir;
                                    scUninst.Description = "卸载 CMP 90HX 控制中心";
                                    scUninst.Save();
                                }
                            }
                        }
                        catch { }

                        // 5. 注册控制面板卸载项
                        try
                        {
                            RegisterUninstall(targetDir, mainExe, uninstExe, icoPath);
                        }
                        catch { }

                        window.Dispatcher.Invoke(new Action(() =>
                        {
                            pBar.IsIndeterminate = false;
                            pBar.Value = 100;
                            txtStatus.Text = "安装成功完成！";

                            MessageBox.Show("【CMP 90HX 控制中心】已成功安装至您的系统！\n\n安装路径: " + targetDir, "安装完成", MessageBoxButton.OK, MessageBoxImage.Information);

                            if (launchNow && File.Exists(mainExe))
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = mainExe,
                                    WorkingDirectory = targetDir
                                });
                            }

                            window.Close();
                        }));
                    }
                    catch (Exception ex)
                    {
                        window.Dispatcher.Invoke(new Action(() =>
                        {
                            pBar.Visibility = Visibility.Collapsed;
                            txtStatus.Text = "安装失败: " + ex.Message;
                            btnCancel.IsEnabled = true;
                            MessageBox.Show("安装过程中发生错误:\n" + ex.Message, "安装失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        }));
                    }
                });
            };

            app.Run(window);
        }

        private static void ExtractResource(Assembly asm, string resName, string outPath)
        {
            using (Stream s = asm.GetManifestResourceStream(resName))
            {
                if (s == null) throw new FileNotFoundException("内嵌资源未找到: " + resName);
                using (FileStream fs = File.Create(outPath))
                {
                    s.CopyTo(fs);
                }
            }
        }

        private static void RegisterUninstall(string targetDir, string mainExe, string uninstExe, string icoPath)
        {
            const string uninstKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CMP90HX_Toolbox";

            RegistryKey[] targets = new RegistryKey[] { Registry.LocalMachine, Registry.CurrentUser };
            foreach (var root in targets)
            {
                try
                {
                    using (var key = root.CreateSubKey(uninstKey))
                    {
                        if (key != null)
                        {
                            key.SetValue("DisplayName", "CMP 90HX 控制中心", RegistryValueKind.String);
                            key.SetValue("DisplayVersion", "2.5.0", RegistryValueKind.String);
                            key.SetValue("Publisher", "RainCandy Technology", RegistryValueKind.String);
                            key.SetValue("InstallLocation", targetDir, RegistryValueKind.String);
                            key.SetValue("DisplayIcon", icoPath, RegistryValueKind.String);
                            key.SetValue("UninstallString", "\"" + uninstExe + "\"", RegistryValueKind.String);
                            key.SetValue("EstimatedSize", 25000, RegistryValueKind.DWord);
                            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                        }
                    }
                }
                catch { }
            }
        }
    }
}
