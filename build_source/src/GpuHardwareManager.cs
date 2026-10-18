using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CMP90HXToolbox
{
    /// <summary>
    /// GpuHardwareManager: 显卡 PnP 枚举、DeviceID 解析、拓扑关系、驱动软重启与传感器监控
    /// </summary>
    public static class GpuHardwareManager
    {
        public static List<NativeCore.InstalledMiningGpu> DetectAllMiningGpus(Action<string, bool> log = null)
        {
            return NativeCore.DetectAllMiningGpus();
        }

        public static string GetGpuPciHardwareDid(string classSubKey = null)
        {
            return NativeCore.GetGpuPciHardwareDid(classSubKey);
        }

        public static string FindPnpInstanceIdByClassSubKey(string classSubKey)
        {
            return NativeCore.FindPnpInstanceIdByClassSubKey(classSubKey);
        }

        public static int GetDeviceProblemCode(string pnpId)
        {
            return NativeCore.GetDeviceProblemCode(pnpId);
        }

        public static Task<bool> SoftRestartGpuDriverAsync(Action<string, bool> log)
        {
            return NativeCore.SoftRestartGpuDriverAsync(log);
        }

        public static NativeCore.DualGpuTopology DetectDualGpuTopology()
        {
            return NativeCore.DetectDualGpuTopology();
        }

        public static List<string> GetConfiguredGpuPreferenceApps()
        {
            return NativeCore.GetConfiguredGpuPreferenceApps();
        }

        public static bool AddAppToGpuPreference(string exePath, Action<string, bool> log = null)
        {
            return NativeCore.AddAppToGpuPreference(exePath, log);
        }

        public static bool RemoveAppFromGpuPreference(string exePath, Action<string, bool> log = null)
        {
            return NativeCore.RemoveAppFromGpuPreference(exePath, log);
        }

        public static Task<int> AutoScanAndBindCommonGamesAsync(Action<string, bool> log)
        {
            return NativeCore.AutoScanAndBindCommonGamesAsync(log);
        }

        public static bool IsDirectXGlobalDefaultGpuApplied()
        {
            return NativeCore.IsDirectXGlobalDefaultGpuApplied();
        }

        public static bool ApplyDirectXGlobalDefaultGpu(Action<string, bool> log)
        {
            return NativeCore.ApplyDirectXGlobalDefaultGpu(log);
        }

        public static bool EnableWindowsAdvancedGpuSettings(Action<string, bool> log)
        {
            return NativeCore.EnableWindowsAdvancedGpuSettings(log);
        }

        public static bool IsOpenGlIcdApplied()
        {
            return NativeCore.IsOpenGlIcdApplied();
        }

        public static bool ApplyOpenGLIcdTo90hx(Action<string, bool> log)
        {
            return NativeCore.ApplyOpenGLIcdTo90hx(log);
        }

        public static bool SetHagsEnabled(bool enable, Action<string, bool> log)
        {
            return NativeCore.SetHagsEnabled(enable, log);
        }

        public static bool IsHagsEnabled()
        {
            return NativeCore.IsHagsEnabled();
        }

        public static bool EnableUltimatePerformancePowerPlan(Action<string, bool> log)
        {
            return NativeCore.EnableUltimatePerformancePowerPlan(log);
        }

        public static NativeCore.GpuSensorSnapshot QueryGpuSensors()
        {
            return NativeCore.QueryGpuSensors();
        }

        public static int ReadGpuUtilization()
        {
            return NativeCore.ReadGpuUtilization();
        }

        public static string ReadPstate()
        {
            return NativeCore.ReadPstate();
        }

        public static List<NativeCore.RunningProcessItem> GetRunningWindowProcesses()
        {
            return NativeCore.GetRunningWindowProcesses();
        }
    }
}
