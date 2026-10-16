using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

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
            this.Text = "CMP 90HX 极速调优控制中心 - 安装向导";
            this.Size = new Size(580, 420);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.BackColor = Color.FromArgb(14, 18, 25);
            this.ForeColor = Color.FromArgb(226, 232, 240);
            this.Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Regular);

            // 顶部横幅
            Panel banner = new Panel();
            banner.Dock = DockStyle.Top;
            banner.Height = 82;
            banner.BackColor = Color.FromArgb(19, 25, 36);
            banner.Paint += (s, e) =>
            {
                using (Pen p = new Pen(Color.FromArgb(34, 44, 61), 1))
                {
                    e.Graphics.DrawLine(p, 0, banner.Height - 1, banner.Width, banner.Height - 1);
                }
            };

            Label lblTitle = new Label();
            lblTitle.Text = "CMP 90HX 极速调优控制中心";
            lblTitle.Font = new Font("Microsoft YaHei", 12.5f, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(248, 250, 252);
            lblTitle.Location = new Point(22, 16);
            lblTitle.AutoSize = true;

            Label lblSub = new Label();
            lblSub.Text = "专业版 v2.5 (专有闭源发行版) | 显卡改名 · 游戏覆盖 · PCIe 速率解锁 · DLSS 3";
            lblSub.Font = new Font("Microsoft YaHei", 9f, FontStyle.Regular);
            lblSub.ForeColor = Color.FromArgb(148, 163, 184);
            lblSub.Location = new Point(23, 46);
            lblSub.AutoSize = true;

            banner.Controls.Add(lblTitle);
            banner.Controls.Add(lblSub);
            this.Controls.Add(banner);

            // 主工作区
            Panel content = new Panel();
            content.Location = new Point(24, 96);
            content.Size = new Size(516, 215);

            Label lblPathTitle = new Label();
            lblPathTitle.Text = "安装目标路径:";
            lblPathTitle.Location = new Point(0, 8);
            lblPathTitle.AutoSize = true;

            txtInstallPath = new TextBox();
            txtInstallPath.Text = @"C:\Program Files\CMP90HX_Toolbox";
            txtInstallPath.Location = new Point(0, 32);
            txtInstallPath.Size = new Size(410, 28);
            txtInstallPath.BackColor = Color.FromArgb(24, 32, 47);
            txtInstallPath.ForeColor = Color.FromArgb(241, 245, 249);
            txtInstallPath.BorderStyle = BorderStyle.FixedSingle;

            btnBrowse = new Button();
            btnBrowse.Text = "浏览...";
            btnBrowse.Location = new Point(420, 30);
            btnBrowse.Size = new Size(88, 30);
            btnBrowse.BackColor = Color.FromArgb(30, 41, 59);
            btnBrowse.ForeColor = Color.FromArgb(241, 245, 249);
            btnBrowse.FlatStyle = FlatStyle.Flat;
            btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);
            btnBrowse.Click += (s, e) =>
            {
                using (FolderBrowserDialog dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "请选择安装目录";
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        txtInstallPath.Text = dlg.SelectedPath;
                    }
                }
            };

            chkDesktopShortcut = new CheckBox();
            chkDesktopShortcut.Text = "创建桌面快捷方式 (推荐)";
            chkDesktopShortcut.Checked = true;
            chkDesktopShortcut.Location = new Point(4, 76);
            chkDesktopShortcut.AutoSize = true;

            chkStartMenu = new CheckBox();
            chkStartMenu.Text = "添加到开始菜单程序列表";
            chkStartMenu.Checked = true;
            chkStartMenu.Location = new Point(4, 104);
            chkStartMenu.AutoSize = true;

            chkLaunchNow = new CheckBox();
            chkLaunchNow.Text = "安装完成后立即启动控制中心";
            chkLaunchNow.Checked = true;
            chkLaunchNow.Location = new Point(4, 132);
            chkLaunchNow.AutoSize = true;

            progressBar = new ProgressBar();
            progressBar.Location = new Point(0, 168);
            progressBar.Size = new Size(508, 16);
            progressBar.Visible = false;

            lblStatus = new Label();
            lblStatus.Text = "点击【立即安装】开始部署...";
            lblStatus.ForeColor = Color.FromArgb(148, 163, 184);
            lblStatus.Location = new Point(0, 190);
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

            // 底部按钮栏
            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 56;
            bottom.BackColor = Color.FromArgb(19, 25, 36);

            btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.Size = new Size(88, 34);
            btnCancel.Location = new Point(446, 11);
            btnCancel.BackColor = Color.FromArgb(30, 41, 59);
            btnCancel.ForeColor = Color.FromArgb(241, 245, 249);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);
            btnCancel.Click += (s, e) => this.Close();

            btnInstall = new Button();
            btnInstall.Text = "立即安装";
            btnInstall.Size = new Size(110, 34);
            btnInstall.Location = new Point(324, 11);
            btnInstall.BackColor = Color.FromArgb(118, 185, 0);
            btnInstall.ForeColor = Color.FromArgb(15, 23, 42);
            btnInstall.Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold);
            btnInstall.FlatStyle = FlatStyle.Flat;
            btnInstall.FlatAppearance.BorderSize = 0;
            btnInstall.Click += (s, e) => StartInstall();

            bottom.Controls.Add(btnInstall);
            bottom.Controls.Add(btnCancel);
            this.Controls.Add(bottom);
        }

        private void StartInstall()
        {
            string targetDir = txtInstallPath.Text.Trim();
            if (string.IsNullOrEmpty(targetDir))
            {
                MessageBox.Show("请指定有效的安装路径！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnInstall.Enabled = false;
            btnCancel.Enabled = false;
            btnBrowse.Enabled = false;
            txtInstallPath.Enabled = false;
            chkDesktopShortcut.Enabled = false;
            chkStartMenu.Enabled = false;
            chkLaunchNow.Enabled = false;

            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            lblStatus.Text = "正在解包并部署文件，请稍候...";

            Thread t = new Thread(() =>
            {
                try
                {
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    string tempZip = Path.Combine(Path.GetTempPath(), "cmp90hx_payload_" + Guid.NewGuid().ToString("N") + ".zip");
                    using (FileStream fs = File.Create(tempZip))
                    {
                        Assembly asm = Assembly.GetExecutingAssembly();
                        foreach (string resName in new string[] { "payload.part1", "payload.part2" })
                        {
                            using (Stream s = asm.GetManifestResourceStream(resName))
                            {
                                if (s == null) throw new Exception("缺失内嵌安装包资源: " + resName);
                                s.CopyTo(fs);
                            }
                        }
                    }

                    // 解压到目标目录
                    System.IO.Compression.ZipFile.ExtractToDirectory(tempZip, targetDir);
                    File.Delete(tempZip);

                    string exePath = Path.Combine(targetDir, "CMP 90HX 极速工具箱.exe");
                    string icoPath = Path.Combine(targetDir, "app.ico");

                    // 快捷方式创建 (WScript.Shell)
                    Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        dynamic shell = Activator.CreateInstance(shellType);
                        if (chkDesktopShortcut.Checked)
                        {
                            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                            string deskLnk = Path.Combine(desktop, "CMP 90HX 极速调优控制中心.lnk");
                            dynamic sc = shell.CreateShortcut(deskLnk);
                            sc.TargetPath = exePath;
                            sc.WorkingDirectory = targetDir;
                            sc.Description = "CMP 90HX 显卡调优控制中心";
                            if (File.Exists(icoPath)) sc.IconLocation = icoPath + ",0";
                            sc.Save();
                        }

                        if (chkStartMenu.Checked)
                        {
                            string startPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "CMP 90HX 控制中心");
                            if (!Directory.Exists(startPrograms)) Directory.CreateDirectory(startPrograms);
                            string menuLnk = Path.Combine(startPrograms, "CMP 90HX 极速调优控制中心.lnk");
                            dynamic scMenu = shell.CreateShortcut(menuLnk);
                            scMenu.TargetPath = exePath;
                            scMenu.WorkingDirectory = targetDir;
                            scMenu.Description = "CMP 90HX 显卡调优控制中心";
                            if (File.Exists(icoPath)) scMenu.IconLocation = icoPath + ",0";
                            scMenu.Save();
                        }
                    }

                    this.Invoke(new Action(() =>
                    {
                        progressBar.Style = ProgressBarStyle.Continuous;
                        progressBar.Value = 100;
                        lblStatus.Text = "安装成功完成！";

                        MessageBox.Show("CMP 90HX 极速调优控制中心 已成功安装到您的电脑！\n\n安装路径: " + targetDir, "安装完成", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        if (chkLaunchNow.Checked && File.Exists(exePath))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = exePath,
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
                        lblStatus.Text = "安装失败: " + ex.Message;
                        btnCancel.Enabled = true;
                        MessageBox.Show("安装过程中发生错误:\n" + ex.Message, "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });

            t.IsBackground = true;
            t.Start();
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
