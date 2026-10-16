using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Packager
{
    public class Program
    {
        public static readonly byte[] SaltKey = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes("CMP90HX_SECURE_HARDWARE_SUITE_GA102_2026"));
        public static readonly byte[] SaltIV = new byte[] { 0x1A, 0x2B, 0x3C, 0x4D, 0x5E, 0x6F, 0x70, 0x81, 0x92, 0xA3, 0xB4, 0xC5, 0xD6, 0xE7, 0xF8, 0x09 };

        public static void Main(string[] args)
        {
            string outPkg = @"D:\app\core.pkg";
            string tk = @"D:\app\build_source\mining_unlock_toolkit";
            string usb = Path.Combine(tk, "usb_90hx");

            using (var zipStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    // ============================================================
                    // 0. 90HX 专属一键解锁 U 盘套件 (新增 · 修复"只写一个文件"的 Bug)
                    //    EFI/Boot/bootx64.efi  = UEFI Shell
                    //    EFI/Boot/bootmgfw.efi = Windows 引导管理器
                    //    EFI/Boot/startup.nsh  = 解锁 + 链启脚本
                    //    根目录 NVPermissiveEFI.efi / bootmgfw.efi / shellx64.efi / startup.nsh
                    // ============================================================
                    AddFile(archive, "usb90/shellx64.efi", Path.Combine(usb, "shellx64.efi"));
                    AddFile(archive, "usb90/bootmgfw.efi", Path.Combine(usb, "bootmgfw.efi"));
                    AddFile(archive, "usb90/NVPermissiveEFI.efi", Path.Combine(usb, "NVPermissiveEFI.efi"));
                    AddFile(archive, "usb90/startup.nsh", Path.Combine(usb, "startup.nsh"));

                    // 1. 模式 1: 免Shell直启双保险固件
                    AddFile(archive, "mode1/NVPermissiveCompat.efi", @"F:\new\NVPermissiveCompat.efi");
                    AddFile(archive, "NVPermissiveCompat.efi", @"F:\new\NVPermissiveCompat.efi");

                    // 2. 模式 2: UEFI Shell 兼容模式固件
                    AddFile(archive, "mode2/Shell.efi", @"D:\app\build_source\90HXUnlock_v2.0\90HXUnlock_v2.0\files\legacy_v1_shell\Shell.efi");
                    AddFile(archive, "mode2/NVPermissiveEFI.efi", @"D:\app\build_source\90HXUnlock_v2.0\90HXUnlock_v2.0\files\legacy_v1_shell\NVPermissiveEFI.efi");
                    AddFile(archive, "mode2/startup.nsh", @"D:\app\build_source\90HXUnlock_v2.0\90HXUnlock_v2.0\files\legacy_v1_shell\startup.nsh");

                    // 3. 模式 3: U盘直启诊断套件
                    AddFile(archive, "mode3/BOOTX64.EFI", @"F:\new\BOOTX64.EFI");
                    AddFile(archive, "mode3/NVPermissiveCompat.efi", @"F:\new\NVPermissiveCompat.efi");
                    AddFile(archive, "mode3/startup.nsh", @"F:\new\startup.nsh");

                    // 3.5 本机 ESP 直装固件 (兼容加强版)
                    AddFile(archive, "esp/NVPermissiveCompat.efi", Path.Combine(tk, @"firmware\NVPermissiveCompat.efi"));
                    AddFile(archive, "esp/shellx64.efi", Path.Combine(usb, "shellx64.efi"));
                    AddFile(archive, "esp/startup.nsh", Path.Combine(usb, "startup.nsh"));

                    // 4. DLSS 运行时
                    string dlssDll = @"D:\app\dlss_runtime\version.dll";
                    if (!File.Exists(dlssDll)) dlssDll = @"F:\dlss\version.dll";
                    string dlssIni = @"D:\app\dlss_runtime\dlssg_sm86.ini";
                    if (!File.Exists(dlssIni)) dlssIni = @"F:\dlss\dlssg_sm86.ini";

                    AddFile(archive, "dlss/version.dll", dlssDll);
                    AddFile(archive, "version.dll", dlssDll);
                    AddFile(archive, "dlss/dlssg_sm86.ini", dlssIni);
                    AddFile(archive, "dlssg_sm86.ini", dlssIni);

                    // 5. RE 框架
                    string reDll = @"F:\RE框架\dinput8.dll";
                    if (!File.Exists(reDll)) reDll = @"C:\Users\19227\Desktop\RE框架\dinput8.dll";
                    AddFile(archive, "re_framework/dinput8.dll", reDll);
                    AddFile(archive, "dinput8.dll", reDll);

                    // 6. 40HX 官方开源解锁套件
                    string tk40 = @"D:\app\build_source\mining_unlock_toolkit\40hx_release";
                    AddFile(archive, "40hx/files/40HXUNLK.EFI", Path.Combine(tk40, @"files\40HXUNLK.EFI"));
                    AddFile(archive, "40hx/files/WinRing0x64.sys", Path.Combine(tk40, @"files\WinRing0x64.sys"));
                    AddFile(archive, "40hx/40HXUNLK.EFI", Path.Combine(tk40, "40HXUNLK.EFI"));
                    AddFile(archive, "40hx/WinRing0x64.sys", Path.Combine(tk40, "WinRing0x64.sys"));
                    AddFile(archive, "40hx/manual_install.bat", Path.Combine(tk40, "manual_install.bat"));
                    AddFile(archive, "40hx/manual_uninstall.bat", Path.Combine(tk40, "manual_uninstall.bat"));
                    AddFile(archive, "40hx/40HXInstaller.exe", Path.Combine(tk40, "40HXInstaller.exe"));
                    AddFile(archive, "40hx/40HXCheck.exe", Path.Combine(tk40, "40HXCheck.exe"));
                    AddFile(archive, "40hx/40HXUninstaller.exe", Path.Combine(tk40, "40HXUninstaller.exe"));
                    AddFile(archive, "40hx/README.md", Path.Combine(tk40, "README.md"));

                    // 7. 30HX Gen2 官方开源解锁套件
                    string tk30 = @"D:\app\build_source\mining_unlock_toolkit\30hx_gen2";
                    AddFile(archive, "30hx/Setup_CMP30HX_WindowsAIO.bat", Path.Combine(tk30, "Setup_CMP30HX_WindowsAIO.bat"));
                    AddFile(archive, "30hx/Setup_ReBar_CMP30HX.bat", Path.Combine(tk30, "Setup_ReBar_CMP30HX.bat"));
                    AddFile(archive, "30hx/README_ZH.md", Path.Combine(tk30, "README_ZH.md"));

                    // 8. 90HX 固件级算力解锁 (WildFlash1st v3.05)
                    string tk90 = @"D:\app\build_source\mining_unlock_toolkit\90hx_firmware";
                    AddFile(archive, "90hx_compute/BOOTX64.EFI", Path.Combine(tk90, "BOOTX64-compute.EFI"));
                    AddFile(archive, "90hx_full/BOOTX64.EFI", Path.Combine(tk90, "BOOTX64-full.EFI"));
                }

                byte[] rawData = zipStream.ToArray();
                Console.WriteLine("Raw ZIP stream size: " + rawData.Length + " bytes");

                using (var aes = Aes.Create())
                {
                    aes.Key = SaltKey;
                    aes.IV = SaltIV;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (var enc = aes.CreateEncryptor())
                    using (var fsOut = File.Create(outPkg))
                    using (var cs = new CryptoStream(fsOut, enc, CryptoStreamMode.Write))
                    {
                        cs.Write(rawData, 0, rawData.Length);
                        cs.FlushFinalBlock();
                    }
                }
            }

            Console.WriteLine("Encrypted core.pkg generated successfully: " + new FileInfo(outPkg).Length + " bytes");
        }

        private static void AddFile(ZipArchive archive, string entryName, string filePath)
        {
            if (File.Exists(filePath))
            {
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                using (var es = entry.Open())
                using (var fs = File.OpenRead(filePath))
                {
                    fs.CopyTo(es);
                }
                Console.WriteLine("Packed: " + entryName + " (" + new FileInfo(filePath).Length + " bytes)");
            }
            else
            {
                Console.WriteLine("WARNING: Source file not found: " + filePath);
            }
        }
    }
}
