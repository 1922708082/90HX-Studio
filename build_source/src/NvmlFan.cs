using System;
using System.Runtime.InteropServices;

namespace CMP90HXToolbox
{
    /// <summary>
    /// 多风扇读数。
    ///
    /// 背景：部分矿卡（如 CMP 90HX）板载 2 个风扇接口，但 nvidia-smi 的
    /// fan.speed 只报 primary（实测 fan0 = 0%，真实转速在 fan1 = 52%）。
    /// nvidia-smi 命令行拿不到分风扇数据，只能走 NVML 的
    /// nvmlDeviceGetNumFans / nvmlDeviceGetFanSpeed_v2。
    ///
    /// 本类只做一件事：取该卡所有风扇里最高的百分比。
    /// 任何一步失败都返回 -1，由调用方决定是否沿用原值。
    /// </summary>
    internal static class NvmlFan
    {
        [DllImport("nvml.dll", EntryPoint = "nvmlInit_v2")]
        private static extern int nvmlInit_v2();

        [DllImport("nvml.dll", EntryPoint = "nvmlInit")]
        private static extern int nvmlInit();

        [DllImport("nvml.dll", EntryPoint = "nvmlShutdown")]
        private static extern int nvmlShutdown();

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
        private static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetNumFans")]
        private static extern int nvmlDeviceGetNumFans(IntPtr device, out uint numFans);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetFanSpeed_v2")]
        private static extern int nvmlDeviceGetFanSpeed_v2(IntPtr device, uint fan, out uint speed);

        private static bool _initTried;
        private static bool _initOk;

        private static bool EnsureInit()
        {
            if (_initTried) return _initOk;
            _initTried = true;
            try
            {
                int rc = nvmlInit_v2();
                if (rc != 0) rc = nvmlInit();
                _initOk = (rc == 0);
            }
            catch
            {
                _initOk = false;
            }
            return _initOk;
        }

        /// <summary>
        /// 返回该卡所有风扇中的最高转速百分比；读不到返回 -1。
        /// gpuIndex 为 NVML 设备序号，一般 0。
        /// </summary>
        public static int GetMaxFanPercent(int gpuIndex)
        {
            try
            {
                if (!EnsureInit()) return -1;

                IntPtr dev;
                if (nvmlDeviceGetHandleByIndex_v2((uint)gpuIndex, out dev) != 0) return -1;

                uint count;
                if (nvmlDeviceGetNumFans(dev, out count) != 0 || count == 0) return -1;

                int max = -1;
                for (uint i = 0; i < count; i++)
                {
                    uint spd;
                    if (nvmlDeviceGetFanSpeed_v2(dev, i, out spd) == 0)
                    {
                        if ((int)spd > max) max = (int)spd;
                    }
                }
                return max;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>风扇数量；读不到返回 0。</summary>
        public static int GetFanCount(int gpuIndex)
        {
            try
            {
                if (!EnsureInit()) return 0;
                IntPtr dev;
                if (nvmlDeviceGetHandleByIndex_v2((uint)gpuIndex, out dev) != 0) return 0;
                uint count;
                if (nvmlDeviceGetNumFans(dev, out count) != 0) return 0;
                return (int)count;
            }
            catch
            {
                return 0;
            }
        }
    }
}
