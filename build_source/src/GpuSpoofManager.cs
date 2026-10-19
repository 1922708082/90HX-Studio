using System;
using System.Threading.Tasks;

namespace CMP90HXToolbox
{
    /// <summary>
    /// GpuSpoofManager: 纯注册表安全注入、76 款目标型号映射与自适应出厂干净回滚
    /// 严禁修改任何系统 INF 与数字签名，确保驱动 WHQL 证书绝对完整
    /// </summary>
    public static class GpuSpoofManager
    {
        public static string[] SpoofTargetModels
        {
            get { return NativeCore.SpoofTargetModels; }
        }

        public static Task<bool> ApplyGpuRenameTo3080Async(Action<string, bool> log)
        {
            return NativeCore.ApplyGpuRenameTo3080Async(log);
        }

        public static Task<bool> ApplyGpuRenameAsync(string targetName, Action<string, bool> log)
        {
            return NativeCore.ApplyGpuRenameAsync(targetName, log);
        }

        public static Task<bool> RestoreGpuTo90hxAsync(Action<string, bool> log)
        {
            return NativeCore.RestoreGpuTo90hxAsync(log);
        }

        public static Task<bool> RestoreMiningGpuByNameAsync(NativeCore.InstalledMiningGpu targetGpu, Action<string, bool> log)
        {
            return NativeCore.RestoreMiningGpuByNameAsync(targetGpu, log);
        }

        public static Task<bool> RestoreMiningGpuAsync(NativeCore.InstalledMiningGpu gpu, Action<string, bool> log)
        {
            return NativeCore.RestoreMiningGpuAsync(gpu, log);
        }
    }
}
