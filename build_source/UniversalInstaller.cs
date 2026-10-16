using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CMP90HXInstaller
{
    public class SetupForm : Form
    {
        private TextBox txtInstallPath;
        private CheckBox chkDesktopShortcut;
        private CheckBox chkStartMenu;
        private CheckBox chkLaunchNow;
        private Button btnInstall;
        private Button btnCancel;
        private Button btnBrowse;
        private ProgressBar progressBar;
        private Label lblStatus;

        public SetupForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "CMP 90HX 控制中心 - 安装向导";
            this.Size = new Size(590, 430);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.BackColor = Color.FromArgb(24, 27, 32);
            this.ForeColor = Color.FromArgb(226, 232, 240);
            this.Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular);

            // 顶部横幅
            Panel banner = new Panel();
            banner.Dock = DockStyle.Top;
            banner.Height = 84;
            banner.BackColor = Color.FromArgb(17, 20, 24);
            banner.Paint += (s, e) =>
            {
                using (Pen p = new Pen(Color.FromArgb(53, 61, 74), 1))
                {
                    e.Graphics.DrawLine(p, 0, banner.Height - 1, banner.Width, banner.Height - 1);
                }
            };

            // 绿色图标方块
            Panel logoBox = new Panel();
            logoBox.Location = new Point(22, 18);
            logoBox.Size = new Size(46, 46);
            logoBox.BackColor = Color.FromArgb(38, 45, 54);
            logoBox.Paint += (s, e) =>
            {
                using (Pen p = new Pen(Color.FromArgb(90, 158, 36), 2))
                {
                    e.Graphics.DrawRectangle(p, 1, 1, 43, 43);
                }
                using (Font f = new Font("Segoe UI", 16f, FontStyle.Bold))
                using (Brush b = new SolidBrush(Color.FromArgb(90, 158, 36)))
                {
                    e.Graphics.DrawString("90", f, b, 4, 6);
                }
            };

            Label lblTitle = new Label();
            lblTitle.Text = "CMP 90HX 控制中心 安装向导";
            lblTitle.Font = new Font("Microsoft YaHei", 12.5f, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(243, 244, 246);
            lblTitle.Location = new Point(78, 18);
            lblTitle.AutoSize = true;

            Label lblSub = new Label();
            lblSub.Text = "GA102 矿卡调优与多模式解锁套件 v2.5 | 显卡伪装 · 覆盖解锁 · PCIe Gen2 · 双卡调用";
            lblSub.Font = new Font("Microsoft YaHei", 8.5f, FontStyle.Regular);
            lblSub.ForeColor = Color.FromArgb(156, 163, 175);
            lblSub.Location = new Point(80, 46);
            lblSub.AutoSize = true;

            banner.Controls.Add(logoBox);
            banner.Controls.Add(lblTitle);
            banner.Controls.Add(lblSub);
            this.Controls.Add(banner);

            // 主工作区
            Panel content = new Panel();
            content.Location = new Point(24, 98);
            content.Size = new Size(526, 220);

            Label lblPathTitle = new Label();
            lblPathTitle.Text = "安装目标文件夹 (默认安装至 Program Files):";
            lblPathTitle.ForeColor = Color.FromArgb(209, 213, 219);
            lblPathTitle.Location = new Point(0, 8);
            lblPathTitle.AutoSize = true;

            txtInstallPath = new TextBox();
            txtInstallPath.Text = @"C:\Program Files\CMP90HX_Toolbox";
            txtInstallPath.Location = new Point(0, 32);
            txtInstallPath.Size = new Size(420, 28);
            txtInstallPath.BackColor = Color.FromArgb(19, 22, 27);
            txtInstallPath.ForeColor = Color.FromArgb(243, 244, 246);
            txtInstallPath.BorderStyle = BorderStyle.FixedSingle;

            btnBrowse = new Button();
            btnBrowse.Text = "浏览...";
            btnBrowse.Location = new Point(430, 30);
            btnBrowse.Size = new Size(88, 30);
            btnBrowse.BackColor = Color.FromArgb(45, 53, 66);
            btnBrowse.ForeColor = Color.FromArgb(243, 244, 246);
            btnBrowse.FlatStyle = FlatStyle.Flat;
            btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(65, 76, 94);
            btnBrowse.Click += (s, e) =>
            {
                using (FolderBrowserDialog dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "请选择 CMP 90HX 控制中心的安装目录";
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        txtInstallPath.Text = dlg.SelectedPath;
                    }
                }
            };

            chkDesktopShortcut = new CheckBox();
            chkDesktopShortcut.Text = "在桌面创建快捷方式 (推荐)";
            chkDesktopShortcut.Checked = true;
            chkDesktopShortcut.Location = new Point(4, 76);
            chkDesktopShortcut.AutoSize = true;

            chkStartMenu = new CheckBox();
            chkStartMenu.Text = "在开始菜单创建快捷方式及程序组";
            chkStartMenu.Checked = true;
            chkStartMenu.Location = new Point(4, 104);
            chkStartMenu.AutoSize = true;

            chkLaunchNow = new CheckBox();
            chkLaunchNow.Text = "安装完成后立即启动 CMP 90HX 控制中心";
            chkLaunchNow.Checked = true;
            chkLaunchNow.Location = new Point(4, 132);
            chkLaunchNow.AutoSize = true;

            progressBar = new ProgressBar();
            progressBar.Location = new Point(0, 168);
            progressBar.Size = new Size(518, 16);
            progressBar.Visible = false;

            lblStatus = new Label();
            lblStatus.Text = "准备安装。点击下方【立即安装】按钮开始。";
            lblStatus.ForeColor = Color.FromArgb(156, 163, 175);
            lblStatus.Location = new Point(0, 192);
            lblStatus.AutoSize = true;

            content.Controls.Add(lblPathTitle);
            content.Controls.Add(txtInstallPath);
            content.Controls.Add(btnBrowse);
            content.Controls.Add(chkDesktopShortcut);
            content.Controls.Add(chkStartMenu);
            content.Controls.Add(chkLaunchNow);
            content.Controls.Add(progressBar);
            content.Controls.Add(lblStatus);
            this.Controls.Add(content);

            // 底部操作按钮栏
            Panel bottomBar = new Panel();
            bottomBar.Dock = DockStyle.Bottom;
            bottomBar.Height = 60;
            bottomBar.BackColor = Color.FromArgb(17, 20, 24);
            bottomBar.Paint += (s, e) =>
            {
                using (Pen p = new Pen(Color.FromArgb(53, 61, 74), 1))
                {
                    e.Graphics.DrawLine(p, 0, 0, bottomBar.Width, 0);
                }
            };

            btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.Location = new Point(468, 14);
            btnCancel.Size = new Size(84, 32);
            btnCancel.BackColor = Color.FromArgb(45, 53, 66);
            btnCancel.ForeColor = Color.FromArgb(226, 232, 240);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(65, 76, 94);
            btnCancel.Click += (s, e) => this.Close();

            btnInstall = new Button();
            btnInstall.Text = "立即安装";
            btnInstall.Location = new Point(356, 14);
            btnInstall.Size = new Size(102, 32);
            btnInstall.BackColor = Color.FromArgb(90, 158, 36);
            btnInstall.ForeColor = Color.White;
            btnInstall.Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold);
            btnInstall.FlatStyle = FlatStyle.Flat;
            btnInstall.FlatAppearance.BorderColor = Color.FromArgb(108, 184, 43);
            btnInstall.Click += BtnInstall_Click;

            bottomBar.Controls.Add(btnInstall);
            bottomBar.Controls.Add(btnCancel);
            this.Controls.Add(bottomBar);
        }

        private void BtnInstall_Click(object sender, EventArgs e)
        {
            string targetDir = txtInstallPath.Text.Trim();
            if (string.IsNullOrEmpty(targetDir))
            {
                MessageBox.Show("请指定有效的安装目标路径！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnInstall.Enabled = false;
            btnBrowse.Enabled = false;
            txtInstallPath.Enabled = false;
            chkDesktopShortcut.Enabled = false;
            chkStartMenu.Enabled = false;
            chkLaunchNow.Enabled = false;
            btnCancel.Enabled = false;

            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            lblStatus.Text = "正在解包并部署文件...";

            Thread t = new Thread(() =>
            {
                try
                {
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    Assembly asm = Assembly.GetExecutingAssembly();

                    // 1. 释放主程序
                    string mainExe = Path.Combine(targetDir, "CMP 90HX 控制中心.exe");
                    ExtractResource(asm, "app.bin", mainExe);

                    // 2. 释放卸载程序
                    string uninstallerExe = Path.Combine(targetDir, "uninstall.exe");
                    ExtractResource(asm, "uninstall.bin", uninstallerExe);

                    // 3. 释放图标与说明
                    string icoPath = Path.Combine(targetDir, "app.ico");
                    ExtractResource(asm, "app.ico", icoPath);

                    string readmePath = Path.Combine(targetDir, "使用说明.txt");
                    ExtractResource(asm, "readme.txt", readmePath);

                    // 4. 创建快捷方式 (WScript.Shell)
                    try
                    {
                        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                        if (shellType != null)
                        {
                            dynamic shell = Activator.CreateInstance(shellType);
                            if (chkDesktopShortcut.Checked)
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

                            if (chkStartMenu.Checked)
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
                                scUninst.TargetPath = uninstallerExe;
                                scUninst.WorkingDirectory = targetDir;
                                scUninst.Description = "卸载 CMP 90HX 控制中心";
                                scUninst.Save();
                            }
                        }
                    }
                    catch { }

                    // 5. 注册 Windows 官方控制面板卸载项
                    try
                    {
                        RegisterUninstallEntry(targetDir, mainExe, uninstallerExe, icoPath);
                    }
                    catch { }

                    this.Invoke(new Action(() =>
                    {
                        progressBar.Style = ProgressBarStyle.Continuous;
                        progressBar.Value = 100;
                        lblStatus.Text = "安装成功！";

                        MessageBox.Show("【CMP 90HX 控制中心】已成功安装到您的电脑！\n\n安装路径: " + targetDir, "安装完成", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        if (chkLaunchNow.Checked && File.Exists(mainExe))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = mainExe,
                                WorkingDirectory = targetDir
                            });
                        }

                        this.Close();
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        progressBar.Visible = false;
                        lblStatus.Text = "安装遇到问题: " + ex.Message;
                        btnCancel.Enabled = true;
                        MessageBox.Show("安装过程中发生错误:\n" + ex.Message, "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });

            t.IsBackground = true;
            t.Start();
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

        private static void RegisterUninstallEntry(string targetDir, string mainExe, string uninstallerExe, string icoPath)
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
                            key.SetValue("UninstallString", "\"" + uninstallerExe + "\"", RegistryValueKind.String);
                            key.SetValue("EstimatedSize", 25000, RegistryValueKind.DWord);
                            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                        }
                    }
                }
                catch { }
            }
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }
}
