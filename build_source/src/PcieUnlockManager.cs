using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CMP90HXToolbox
{
    /// <summary>
    /// PcieUnlockManager: UEFI 固件部署、ESP 挂载、startup.nsh 生成、BCD 独立备份与开机引导解锁
    /// </summary>
    public static class PcieUnlockManager
    {
        public static NativeCore.PcieUnlockMode DetectCurrentPcieUnlockMode(out string detailMsg)
        {
            return NativeCore.DetectCurrentPcieUnlockMode(out detailMsg);
        }

        public static bool CleanAllPcieUnlocks(Action<string, bool> log)
        {
            return NativeCore.CleanAllPcieUnlocks(log);
        }

        public static Task<bool> InstallPcieMode1Async(Action<string, bool> log)
        {
            return NativeCore.InstallPcieMode1Async(log);
        }

        public static Task<bool> InstallPcieMode2Async(Action<string, bool> log)
        {
            return NativeCore.InstallPcieMode2Async(log);
        }

        public static Task<bool> CreateMode3UsbDriveAsync(string driveLetter, Action<string, bool> log)
        {
            return NativeCore.CreateMode3UsbDriveAsync(driveLetter, log);
        }

        public static Task<bool> SetUsbFirstBootAsync(string driveLetter, Action<string, bool> log)
        {
            return NativeCore.SetUsbFirstBootAsync(driveLetter, log);
        }

        public static Task<bool> RemoveUsbFirstBootAsync(Action<string, bool> log)
        {
            return NativeCore.RemoveUsbFirstBootAsync(log);
        }

        public static Task<bool> RepairBootMenuAsync(Action<string, bool> log)
        {
            return NativeCore.RepairBootMenuAsync(log);
        }

        public static string BackupBcd(Action<string, bool> log)
        {
            return NativeCore.BackupBcd(log);
        }

        public static bool RestoreBcdFromBackup(Action<string, bool> log)
        {
            return NativeCore.RestoreBcdFromBackup(log);
        }

        public static Task<bool> ApplyNvPermissiveUnlockAsync(List<NativeCore.InstalledMiningGpu> gpus, int installChannel, string usbDrive, Action<string, bool> log)
        {
            return NativeCore.ApplyNvPermissiveUnlockAsync(gpus, installChannel, usbDrive, log);
        }

        public static Task<bool> Create90hxFirmwareUsbAsync(string driveLetter, bool computeOnly, Action<string, bool> log)
        {
            return NativeCore.Create90hxFirmwareUsbAsync(driveLetter, computeOnly, log);
        }

        public static Task<bool> RollbackPcieUnlockAsync(Action<string, bool> log)
        {
            return NativeCore.RollbackPcieUnlockAsync(log);
        }

        public static Task<bool> InstallComputeMode1Async(Action<string, bool> log)
        {
            return NativeCore.InstallComputeMode1Async(log);
        }

        public static Task<bool> InstallComputeMode2Async(Action<string, bool> log)
        {
            return NativeCore.InstallComputeMode2Async(log);
        }

        public static Task<bool> ApplyComputeFirmwareMultiGpuUnlockAsync(List<NativeCore.InstalledMiningGpu> gpus, int installChannel, string usbDrive, Action<string, bool> log)
        {
            return NativeCore.ApplyComputeFirmwareMultiGpuUnlockAsync(gpus, installChannel, usbDrive, log);
        }

        public static List<string> GetAvailableRemovableDrives()
        {
            return NativeCore.GetAvailableRemovableDrives();
        }

        public static string GenerateSmartStartupNsh(List<NativeCore.InstalledMiningGpu> gpus)
        {
            return NativeCore.GenerateSmartStartupNsh(gpus);
        }

        public static Task<bool> ApplySingleGpuComputeUnlockAsync(NativeCore.InstalledMiningGpu gpu, Action<string, bool> log)
        {
            return NativeCore.ApplySingleGpuComputeUnlockAsync(gpu, log);
        }

        public static Task<bool> ApplySingleGpuGraphicsUnlockAsync(NativeCore.InstalledMiningGpu gpu, Action<string, bool> log, string overrideTargetName = null)
        {
            return NativeCore.ApplySingleGpuGraphicsUnlockAsync(gpu, log, overrideTargetName);
        }

        public static bool DeployNative40hxUnlock(Action<string, bool> log)
        {
            return NativeCore.DeployNative40hxUnlock(log);
        }

        public static bool DeployNative30hxUnlock(Action<string, bool> log)
        {
            return NativeCore.DeployNative30hxUnlock(log);
        }

        public static Task<bool> ApplyHybridMultiGpuUnlockAsync(List<NativeCore.InstalledMiningGpu> gpus, bool unlockGraphics, bool unlockCompute, Action<string, bool> log)
        {
            return NativeCore.ApplyHybridMultiGpuUnlockAsync(gpus, unlockGraphics, unlockCompute, log);
        }

        public static Task<bool> ApplyHybridMultiGpuUnlockAsync(List<NativeCore.InstalledMiningGpu> gpus, bool unlockGraphics, bool unlockCompute, int installChannel, Action<string, bool> log)
        {
            return NativeCore.ApplyHybridMultiGpuUnlockAsync(gpus, unlockGraphics, unlockCompute, installChannel, log);
        }
    }
}
