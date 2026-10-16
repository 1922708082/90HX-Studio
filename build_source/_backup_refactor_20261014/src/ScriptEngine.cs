using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CMP90HXToolbox
{
    public static class ScriptEngine
    {
        public static string ResolveToolPath(string relativePath, string fDriveFallback)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localPath = Path.Combine(baseDir, relativePath);
            if (File.Exists(localPath) || Directory.Exists(localPath))
            {
                return localPath;
            }
            if (!string.IsNullOrEmpty(fDriveFallback) && (File.Exists(fDriveFallback) || Directory.Exists(fDriveFallback)))
            {
                return fDriveFallback;
            }
            return localPath;
        }

        public static Task<int> RunPowerShellAsync(string scriptPath, string extraArgs, Action<string, bool> onLogLine)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    if (!File.Exists(scriptPath))
                    {
                        if (onLogLine != null)
                        {
                            onLogLine(string.Format("[错误] 找不到目标脚本文件: {0}", scriptPath), true);
                        }
                        return -1;
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = string.Format("-NoProfile -ExecutionPolicy Bypass -File \"{0}\" -NonInteractive {1}", scriptPath, extraArgs ?? ""),
                        WorkingDirectory = Path.GetDirectoryName(scriptPath),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using (var proc = new Process())
                    {
                        proc.StartInfo = psi;
                        proc.OutputDataReceived += (s, e) =>
                        {
                            if (e.Data != null && onLogLine != null)
                            {
                                onLogLine(e.Data, false);
                            }
                        };
                        proc.ErrorDataReceived += (s, e) =>
                        {
                            if (e.Data != null && onLogLine != null)
                            {
                                onLogLine(string.Format("[错误] {0}", e.Data), true);
                            }
                        };

                        proc.Start();
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();
                        proc.WaitForExit();

                        return proc.ExitCode;
                    }
                }
                catch (Exception ex)
                {
                    if (onLogLine != null)
                    {
                        onLogLine(string.Format("[异常] 执行脚本时发生错误: {0}", ex.Message), true);
                    }
                    return -1;
                }
            });
        }

        public static Task<int> RunCommandAsync(string command, Action<string, bool> onLogLine)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"{0}\"", command),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using (var proc = new Process())
                    {
                        proc.StartInfo = psi;
                        proc.OutputDataReceived += (s, e) =>
                        {
                            if (e.Data != null && onLogLine != null)
                            {
                                onLogLine(e.Data, false);
                            }
                        };
                        proc.ErrorDataReceived += (s, e) =>
                        {
                            if (e.Data != null && onLogLine != null)
                            {
                                onLogLine(string.Format("[错误] {0}", e.Data), true);
                            }
                        };

                        proc.Start();
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();
                        proc.WaitForExit();

                        return proc.ExitCode;
                    }
                }
                catch (Exception ex)
                {
                    if (onLogLine != null)
                    {
                        onLogLine(string.Format("[异常] 执行命令时发生错误: {0}", ex.Message), true);
                    }
                    return -1;
                }
            });
        }
    }
}
