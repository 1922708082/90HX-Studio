# CMP 90HX Unlock 应急修复指南 (v2.0)

> **本工具 v2.0 架构自带「官方原生双保险引导备份」，即使解锁固件未正常执行，系统也会自动回退至原生 Windows，通常无需手动干预。**

---

## 一、常见状态与排查

1. **开机后直接进了 Windows，但 GPU-Z 仍显示 PCIe 1.1**：
   - 原因 A：开机时未完全断电。GA102 芯片需要冷启动才能重新训练 PCIe 寄存器，请点击关机、拔掉电源线 10 秒后重新开机。
   - 原因 B：BIOS 中 Secure Boot 被重新开启了。请进 BIOS 确认 Secure Boot 保持 Disabled。
2. **开机提示 FAILED 报错或卡住**：
   - 重启时在主板自检画面连按启动菜单键（华硕 `F8`、微星 `F11`、技嘉/其他 `F12`）；
   - 在启动列表中直接选择 **`Windows Boot Manager (Backup)`** 即可立刻进入原生 Windows；
   - 进系统后运行 `manual_uninstall.bat` 即可一键恢复。

---

## 二、Windows PE / 安装 U 盘终极恢复命令

如果在极端情况下需要通过 PE 恢复：
1. 插入 WinPE 或 Windows 安装 U 盘启动；
2. 按 `Shift + F10` 打开命令提示符；
3. 挂载并清理 EFI 目录：
   ```cmd
   diskpart
   list volume
   select volume X  (X 为 FAT32 格式的 ESP 引导分区)
   assign letter=Z:
   exit
   ```
4. 删除工具目录并用 Windows 原生命令重建引导：
   ```cmd
   rmdir /s /q Z:\EFI\NVPermissive
   rmdir /s /q Z:\EFI\90HX
   del Z:\startup.nsh
   bcdboot C:\Windows /s Z: /f UEFI
   ```
5. 重启电脑即可完全恢复。