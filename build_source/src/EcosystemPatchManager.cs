using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CMP90HXToolbox
{
    /// <summary>
    /// EcosystemPatchManager: NVIDIA App 双 CEF 缓存清理、弹性正则绕过与 DLSS 2500ms 并发探活热替换
    /// </summary>
    public static class EcosystemPatchManager
    {
        public static string GetDlssUnlockedDirectory()
        {
            return NativeCore.GetDlssUnlockedDirectory();
        }

        public static string GetDlssUnlockedLocalVersion()
        {
            return NativeCore.GetDlssUnlockedLocalVersion();
        }

        public static string CheckDlssUnlockedLatestTag(Action<string, bool> log)
        {
            return NativeCore.CheckDlssUnlockedLatestTag(log);
        }

        public static Task<bool> DownloadDlssUnlockedAsync(string tag, Action<int, string> progress, Action<string, bool> log)
        {
            return NativeCore.DownloadDlssUnlockedAsync(tag, progress, log);
        }

        public static string ValidateDlssUnlockedPackage(Action<string, bool> log)
        {
            return NativeCore.ValidateDlssUnlockedPackage(log);
        }

        public static bool DeployDlssUnlockedToGame(string gameDir, Action<string, bool> log)
        {
            return NativeCore.DeployDlssUnlockedToGame(gameDir, log);
        }

        public static bool LaunchToolkitApp(string toolkitKey, string exeName, Action<string, bool> log)
        {
            return NativeCore.LaunchToolkitApp(toolkitKey, exeName, log);
        }

        public static bool OpenToolkitFolder(string toolkitKey, Action<string, bool> log)
        {
            return NativeCore.OpenToolkitFolder(toolkitKey, log);
        }

        public static Task<bool> UnlockNvAppOverlayAsync(Action<string, bool> log)
        {
            return NativeCore.UnlockNvAppOverlayAsync(log);
        }

        public static Task<bool> RestoreNvAppOverlayAsync(Action<string, bool> log)
        {
            return NativeCore.RestoreNvAppOverlayAsync(log);
        }

        public static void CleanCefCacheInternal(Action<string, bool> log)
        {
            NativeCore.CleanCefCacheInternal(log);
        }

        public static List<NativeCore.DlssMirror> GetDlssMirrors()
        {
            return NativeCore.GetDlssMirrors();
        }

        public static string GetLocalDlssVersion()
        {
            return NativeCore.GetLocalDlssVersion();
        }

        public static Task<NativeCore.DlssCheckResult> CheckDlssUpdateAsync(int mirrorIndex, Action<string, bool> log)
        {
            return NativeCore.CheckDlssUpdateAsync(mirrorIndex, log);
        }

        public static Task<NativeCore.DlssDownloadResult> PullAndReplaceDlssAsync(int mirrorIndex, string targetGameDir, Action<int> progress, Action<string, bool> log)
        {
            return NativeCore.PullAndReplaceDlssAsync(mirrorIndex, targetGameDir, progress, log);
        }

        public static bool InjectDlssG(string gameDir, string optVal, string maxFramesVal, bool injectReFramework, string proxyName, string engineVal, string presetVal, string srModeVal, Action<string, bool> log)
        {
            return NativeCore.InjectDlssG(gameDir, optVal, maxFramesVal, injectReFramework, proxyName, engineVal, presetVal, srModeVal, log);
        }

        public static bool InjectDlssG(string gameDir, string optVal, string maxFramesVal, bool injectReFramework, Action<string, bool> log)
        {
            return NativeCore.InjectDlssG(gameDir, optVal, maxFramesVal, injectReFramework, log);
        }

        public static bool RemoveDlssG(string gameDir, Action<string, bool> log)
        {
            return NativeCore.RemoveDlssG(gameDir, log);
        }

        public static NativeCore.DlssPreflightResult PreflightDlssDeploy(string gameDir, string proxyName, bool injectReFramework)
        {
            return NativeCore.PreflightDlssDeploy(gameDir, proxyName, injectReFramework);
        }

        public static bool IsDlssIniHealthy(string text)
        {
            return NativeCore.IsDlssIniHealthy(text);
        }

        public static string SetIniValue(string text, string section, string key, string value)
        {
            return NativeCore.SetIniValue(text, section, key, value);
        }

        public static string GetReFrameworkDirectory()
        {
            return NativeCore.GetReFrameworkDirectory();
        }

        public static string GetLocalReFrameworkVersion()
        {
            return NativeCore.GetLocalReFrameworkVersion();
        }

        public static string ResolveReFrameworkLatestTag(Action<string, bool> log = null)
        {
            return NativeCore.ResolveReFrameworkLatestTag();
        }

        public static Task<NativeCore.ReFwCheckResult> CheckReFrameworkUpdateAsync(Action<string, bool> log)
        {
            return NativeCore.CheckReFrameworkUpdateAsync(log);
        }

        public static Task<NativeCore.ReFwPullResult> PullReFrameworkAsync(string targetGameDir, Action<int> progress, Action<string, bool> log)
        {
            return NativeCore.PullReFrameworkAsync(targetGameDir, progress, log);
        }
    }
}
