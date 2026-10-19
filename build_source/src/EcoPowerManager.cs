using System;
using System.Threading;
using System.Threading.Tasks;

namespace CMP90HXToolbox
{
    /// <summary>
    /// EcoPowerManager: Session 1+ 交互令牌任务部署、-rac/-rmc 全套时钟自愈恢复链、SMI 自适应限制与长驻守护
    /// </summary>
    public static class EcoPowerManager
    {
        public static bool IsGpuIdleSaveActive()
        {
            return NativeCore.IsGpuIdleSaveActive();
        }

        public static void LoadDynamicClockParams(Action<string, bool> log = null)
        {
            NativeCore.LoadDynamicClockParams(log);
        }

        public static string GetMinCoreClockLock()
        {
            return NativeCore.GetMinCoreClockLock();
        }

        public static string GetIdleAppClocks()
        {
            return NativeCore.GetIdleAppClocks();
        }

        public static int EffectiveIdlePowerLimit()
        {
            return NativeCore.EffectiveIdlePowerLimit();
        }

        public static int EffectiveDefaultPowerLimit()
        {
            return NativeCore.EffectiveDefaultPowerLimit();
        }

        public static bool IsPowerSaverTaskInstalled()
        {
            return NativeCore.IsPowerSaverTaskInstalled();
        }

        public static bool InstallPowerSaverTask(Action<string, bool> log)
        {
            return NativeCore.InstallPowerSaverTask(log);
        }

        public static bool UninstallPowerSaverTask(Action<string, bool> log)
        {
            return NativeCore.UninstallPowerSaverTask(log);
        }

        public static void RunPowerSaverTaskNow(Action<string, bool> log)
        {
            NativeCore.RunPowerSaverTaskNow(log);
        }

        public static Mutex CreateGlobalMutex(string name, out bool createdNew)
        {
            return NativeCore.CreateGlobalMutex(name, out createdNew);
        }

        public static EventWaitHandle CreateGlobalExitEvent(string name)
        {
            return NativeCore.CreateGlobalExitEvent(name);
        }

        public static string GetPowerSaverPidFile()
        {
            return NativeCore.GetPowerSaverPidFile();
        }

        public static bool IsPowerSaverRunning()
        {
            return NativeCore.IsPowerSaverRunning();
        }

        public static bool StartPowerSaverProcess(Action<string, bool> log)
        {
            return NativeCore.StartPowerSaverProcess(log);
        }

        public static void StopPowerSaverProcess(Action<string, bool> log)
        {
            NativeCore.StopPowerSaverProcess(log);
        }

        public static bool IsAutoIdleSaveRunning()
        {
            return NativeCore.IsAutoIdleSaveRunning();
        }

        public static void StartAutoIdleSave(int idleSeconds, Action<string, bool> log = null)
        {
            NativeCore.StartAutoIdleSave(idleSeconds, log);
        }

        public static bool ApplyGpuPowerSaveCore(Action<string, bool> log)
        {
            return NativeCore.ApplyGpuPowerSaveCore(log);
        }

        public static bool RestoreGpuPowerSaveCore(Action<string, bool> log)
        {
            return NativeCore.RestoreGpuPowerSaveCore(log);
        }

        public static void StopAutoIdleSave(Action<string, bool> log = null)
        {
            NativeCore.StopAutoIdleSave(log);
        }

        public static Task<bool> ToggleGpuIdleSaveAsync(bool enable, Action<string, bool> log)
        {
            return NativeCore.ToggleGpuIdleSaveAsync(enable, log);
        }
    }
}
