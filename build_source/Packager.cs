using System;
using System.IO;
using System.IO.Compression;

// ============================================================================
// 90HX Studio 明文资源打包器 (开源版)
// ----------------------------------------------------------------------------
// 本软件 100% 开源：所有内置组件（EFI 固件、DLSS 运行库、RE 框架、各型号
// 官方解锁工具包）均以【标准未加密 ZIP】形式打包为 assets.zip，然后原样
// 内嵌进 EXE。不含任何 AES / XOR / Base64 混淆，任何人可用 7-Zip 直接
// 解包审阅全部内容。
//
// 用法：pack.ps1 会自动编译并运行本程序，生成 D:\app\assets.zip
// ============================================================================

namespace Packager
{
    public class Program
    {
        // 所有源文件都收敛在 mining_unlock_toolkit 与几个固定位置，路径集中在此便于审计
        private static readonly string ToolkitRoot = @"D:\app\build_source\mining_unlock_toolkit";
        private static readonly string LegacyShell = @"D:\app\build_source\90HXUnlock_v2.0\90HXUnlock_v2.0\files\legacy_v1_shell";
        private static readonly string UsbDir = Path.Combine(ToolkitRoot, "usb_90hx");
        private static readonly string FirmwareDir = Path.Combine(ToolkitRoot, "firmware");
        private static readonly string Tk40 = Path.Combine(ToolkitRoot, "40hx_release");
        private static readonly string Tk30 = Path.Combine(ToolkitRoot, "30hx_gen2");
        private static readonly string Tk90 = Path.Combine(ToolkitRoot, "90hx_firmware");

        public static void Main(string[] args)
        {
            string outZip = @"D:\app\assets.zip";
            int packed = 0, missing = 0;

            using (var fsOut = File.Create(outZip))
            using (var archive = new ZipArchive(fsOut, ZipArchiveMode.Create))
            {
                // ============================================================
                // A. 90HX 一键解锁 U 盘套件（黄金基准，与用户实测 U 盘字节级一致）
                // ============================================================
                packed += AddFile(archive, "usb90/shellx64.efi", Path.Combine(UsbDir, "shellx64.efi"), ref missing);
                packed += AddFile(archive, "usb90/bootmgfw.efi", Path.Combine(UsbDir, "bootmgfw.efi"), ref missing);
                packed += AddFile(archive, "usb90/NVPermissiveEFI.efi", Path.Combine(UsbDir, "NVPermissiveEFI.efi"), ref missing);
                packed += AddFile(archive, "usb90/startup.nsh", Path.Combine(UsbDir, "startup.nsh"), ref missing);

                // ============================================================
                // B. 模式 1：免 Shell 直启双保险固件（ESP 直装 / 现代 UEFI 主板）
                // ============================================================
                packed += AddFile(archive, "mode1/NVPermissiveCompat.efi", First(@"F:\new\NVPermissiveCompat.efi", Path.Combine(FirmwareDir, "NVPermissiveCompat.efi")), ref missing);
                packed += AddFile(archive, "NVPermissiveCompat.efi", First(@"F:\new\NVPermissiveCompat.efi", Path.Combine(FirmwareDir, "NVPermissiveCompat.efi")), ref missing);

                // ============================================================
                // C. 模式 2：UEFI Shell 兼容模式（老主板华南/X79/X99 等）
                // ============================================================
                packed += AddFile(archive, "mode2/Shell.efi", Path.Combine(LegacyShell, "Shell.efi"), ref missing);
                packed += AddFile(archive, "mode2/NVPermissiveEFI.efi", Path.Combine(LegacyShell, "NVPermissiveEFI.efi"), ref missing);
                packed += AddFile(archive, "mode2/startup.nsh", Path.Combine(LegacyShell, "startup.nsh"), ref missing);

                // ============================================================
                // D. 模式 3：U 盘免安装直启诊断套件
                // ============================================================
                packed += AddFile(archive, "mode3/BOOTX64.EFI", First(@"F:\new\BOOTX64.EFI", Path.Combine(UsbDir, "shellx64.efi")), ref missing);
                packed += AddFile(archive, "mode3/NVPermissiveCompat.efi", First(@"F:\new\NVPermissiveCompat.efi", Path.Combine(FirmwareDir, "NVPermissiveCompat.efi")), ref missing);
                packed += AddFile(archive, "mode3/startup.nsh", First(@"F:\new\startup.nsh", Path.Combine(UsbDir, "startup.nsh")), ref missing);

                // ============================================================
                // E. 本机 ESP 直装固件（兼容加强版）
                // ============================================================
                packed += AddFile(archive, "esp/NVPermissiveCompat.efi", Path.Combine(FirmwareDir, "NVPermissiveCompat.efi"), ref missing);
                packed += AddFile(archive, "esp/shellx64.efi", Path.Combine(UsbDir, "shellx64.efi"), ref missing);
                packed += AddFile(archive, "esp/startup.nsh", Path.Combine(UsbDir, "startup.nsh"), ref missing);

                // ============================================================
                // F. DLSS 3 帧生成运行库 (SM86)
                // ============================================================
                packed += AddFile(archive, "dlss/version.dll", First(@"D:\app\dlss_runtime\version.dll", @"F:\dlss\version.dll"), ref missing);
                packed += AddFile(archive, "version.dll", First(@"D:\app\dlss_runtime\version.dll", @"F:\dlss\version.dll"), ref missing);
                packed += AddFile(archive, "dlss/dlssg_sm86.ini", First(@"D:\app\dlss_runtime\dlssg_sm86.ini", @"F:\dlss\dlssg_sm86.ini"), ref missing);
                packed += AddFile(archive, "dlssg_sm86.ini", First(@"D:\app\dlss_runtime\dlssg_sm86.ini", @"F:\dlss\dlssg_sm86.ini"), ref missing);

                // ============================================================
                // G. RE 框架代理 (DLL 注入)
                // ============================================================
                packed += AddFile(archive, "re_framework/dinput8.dll", First(@"F:\RE框架\dinput8.dll", @"C:\Users\19227\Desktop\RE框架\dinput8.dll"), ref missing);
                packed += AddFile(archive, "dinput8.dll", First(@"F:\RE框架\dinput8.dll", @"C:\Users\19227\Desktop\RE框架\dinput8.dll"), ref missing);

                // ============================================================
                // H. 40HX 官方开源解锁套件 (PZH1gdmu/CMP40HX-Unlock)
                // ============================================================
                packed += AddFile(archive, "40hx/files/40HXUNLK.EFI", Path.Combine(Tk40, @"files\40HXUNLK.EFI"), ref missing);
                packed += AddFile(archive, "40hx/files/WinRing0x64.sys", Path.Combine(Tk40, @"files\WinRing0x64.sys"), ref missing);
                packed += AddFile(archive, "40hx/40HXUNLK.EFI", Path.Combine(Tk40, "40HXUNLK.EFI"), ref missing);
                packed += AddFile(archive, "40hx/WinRing0x64.sys", Path.Combine(Tk40, "WinRing0x64.sys"), ref missing);
                packed += AddFile(archive, "40hx/manual_install.bat", Path.Combine(Tk40, "manual_install.bat"), ref missing);
                packed += AddFile(archive, "40hx/manual_uninstall.bat", Path.Combine(Tk40, "manual_uninstall.bat"), ref missing);
                packed += AddFile(archive, "40hx/40HXInstaller.exe", Path.Combine(Tk40, "40HXInstaller.exe"), ref missing);
                packed += AddFile(archive, "40hx/40HXCheck.exe", Path.Combine(Tk40, "40HXCheck.exe"), ref missing);
                packed += AddFile(archive, "40hx/40HXUninstaller.exe", Path.Combine(Tk40, "40HXUninstaller.exe"), ref missing);
                packed += AddFile(archive, "40hx/README.md", Path.Combine(Tk40, "README.md"), ref missing);

                // ============================================================
                // I. 30HX Gen2 官方开源解锁套件
                // ============================================================
                packed += AddFile(archive, "30hx/Setup_CMP30HX_WindowsAIO.bat", Path.Combine(Tk30, "Setup_CMP30HX_WindowsAIO.bat"), ref missing);
                packed += AddFile(archive, "30hx/Setup_ReBar_CMP30HX.bat", Path.Combine(Tk30, "Setup_ReBar_CMP30HX.bat"), ref missing);
                packed += AddFile(archive, "30hx/README_ZH.md", Path.Combine(Tk30, "README_ZH.md"), ref missing);

                // ============================================================
                // J. 90HX 固件级算力解锁 (WildFlash1st/cmp90hx-unlock-for-windows v3.05)
                // ============================================================
                packed += AddFile(archive, "90hx_compute/BOOTX64.EFI", Path.Combine(Tk90, "BOOTX64-compute.EFI"), ref missing);
                packed += AddFile(archive, "90hx_full/BOOTX64.EFI", Path.Combine(Tk90, "BOOTX64-full.EFI"), ref missing);

                // ============================================================
                // K. 50HX Windows 一键解锁套件 (xrip/cmp50hx-unlock v1.1.22)
                //    从发布包中解出的三个可执行文件 + EFI 固件
                // ============================================================
                string tk50 = Path.Combine(ToolkitRoot, "50hx_unlock", "extracted");
                packed += AddFile(archive, "50hx/50HXInstaller.exe", Path.Combine(tk50, "50HXInstaller.exe"), ref missing);
                packed += AddFile(archive, "50hx/50HXCheck.exe", Path.Combine(tk50, "50HXCheck.exe"), ref missing);
                packed += AddFile(archive, "50hx/50HXUninstaller.exe", Path.Combine(tk50, "50HXUninstaller.exe"), ref missing);
                packed += AddFile(archive, "50hx/50HXUNLK.EFI", Path.Combine(tk50, "50HXUNLK.EFI"), ref missing);
                packed += AddFile(archive, "50hx/WinRing0x64.sys", Path.Combine(tk50, @"drivers\WinRing0x64.sys"), ref missing);
            }

            var fi = new FileInfo(outZip);
            Console.WriteLine("==================================================");
            Console.WriteLine("[OK] 明文资源包生成成功（无任何加密）: " + outZip);
            Console.WriteLine("     大小: " + fi.Length + " bytes (" + Math.Round(fi.Length / 1024.0 / 1024.0, 2) + " MB)");
            Console.WriteLine("     打包: " + packed + " 个条目 | 缺失: " + missing + " 个");
            Console.WriteLine("     任何人可用 7-Zip 直接解包审阅。");
            Console.WriteLine("==================================================");
        }

        private static string First(params string[] paths)
        {
            foreach (var p in paths)
            {
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
            }
            return paths.Length > 0 ? paths[0] : "";
        }

        private static int AddFile(ZipArchive archive, string entryName, string filePath, ref int missing)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Console.WriteLine("[MISS] " + entryName + "  <- 源文件不存在: " + filePath);
                missing++;
                return 0;
            }

            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (var es = entry.Open())
            using (var fs = File.OpenRead(filePath))
            {
                fs.CopyTo(es);
            }
            Console.WriteLine("[OK]   " + entryName + "  (" + new FileInfo(filePath).Length + " bytes)");
            return 1;
        }
    }
}
