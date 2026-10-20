using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CMP90HXToolbox
{
    /// <summary>
    /// AI 可执行动作：助手不只回答方案，而是能直接调用工具箱的真实能力。
    /// 所有破坏性操作都必须由用户在对话里点“执行”确认后才会落地。
    /// </summary>
    public class AiAgentAction
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public bool IsDestructive { get; set; }
        public Func<Action<string, bool>, Task<string>> Executor { get; set; }
    }

    public static class AiAgentActionRegistry
    {
        private static readonly string[] SpoofKeywords = new string[] { "伪装", "改名", "型号" };
        private static readonly string[] UnlockKeywords = new string[] { "解锁", "vperm", "引导", "开图形", "启用图形" };
        private static readonly string[] OverlayKeywords = new string[] { "覆盖", "nvapp", "shadowplay", "alt+z", "滤镜", "录屏" };
        private static readonly string[] PowerKeywords = new string[] { "省电", "功耗", "降温", "待机功耗" };
        private static readonly string[] DriverKeywords = new string[] { "驱动", "重启驱动", "热重载", "code 43", "43" };
        private static readonly string[] DualKeywords = new string[] { "双卡", "核显", "调度", "首选显卡", "默认显卡" };
        private static readonly string[] DiagnoseKeywords = new string[] {
            "解锁失败", "没解锁", "没有解锁", "不解锁", "不成功", "失败原因", "为什么失败",
            "诊断", "排查", "怎么回事", "没反应", "code 43", "报错"
        };

        private static bool ContainsAny(string text, string[] keys)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < keys.Length; i++)
            {
                if (text.IndexOf(keys[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static NativeCore.InstalledMiningGpu PickGpu(string modelHint)
        {
            List<NativeCore.InstalledMiningGpu> gpus = GpuHardwareManager.DetectAllMiningGpus();
            if (gpus == null || gpus.Count == 0) return null;
            if (string.IsNullOrEmpty(modelHint)) return gpus[0];
            for (int i = 0; i < gpus.Count; i++)
            {
                if (gpus[i].DetectedModel != null &&
                    gpus[i].DetectedModel.IndexOf(modelHint, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return gpus[i];
                }
            }
            return gpus[0];
        }

        private static string DetectModelHint(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            if (text.IndexOf("170HX", StringComparison.OrdinalIgnoreCase) >= 0) return "170HX";
            if (text.IndexOf("90HX", StringComparison.OrdinalIgnoreCase) >= 0) return "90HX";
            if (text.IndexOf("70HX", StringComparison.OrdinalIgnoreCase) >= 0) return "70HX";
            if (text.IndexOf("50HX", StringComparison.OrdinalIgnoreCase) >= 0) return "50HX";
            if (text.IndexOf("40HX", StringComparison.OrdinalIgnoreCase) >= 0) return "40HX";
            if (text.IndexOf("30HX", StringComparison.OrdinalIgnoreCase) >= 0) return "30HX";
            return null;
        }

        private static string[] KeywordsById(string id)
        {
            if (id == "spoof") return SpoofKeywords;
            if (id == "unlock") return UnlockKeywords;
            if (id == "overlay") return OverlayKeywords;
            if (id == "power") return PowerKeywords;
            if (id == "driver") return DriverKeywords;
            if (id == "dual") return DualKeywords;
            return null;
        }

        /// <summary>
        /// 多步指令支持：「解锁 90HX 并伪装成 3080」这类话会返回多个动作，
        /// 按关键词在句子中出现的先后顺序排列（先解锁再伪装）。
        /// </summary>
        public static List<AiAgentAction> MatchMany(string userText)
        {
            List<AiAgentAction> list = new List<AiAgentAction>();
            if (string.IsNullOrWhiteSpace(userText)) return list;

            string[][] groups = new string[][] {
                SpoofKeywords, UnlockKeywords, OverlayKeywords, PowerKeywords, DriverKeywords, DualKeywords
            };

            // 1) 找出每一类动作在句中的最早出现位置
            List<int> kinds = new List<int>();
            List<int> positions = new List<int>();
            for (int k = 0; k < groups.Length; k++)
            {
                int best = -1;
                for (int i = 0; i < groups[k].Length; i++)
                {
                    int p = userText.IndexOf(groups[k][i], StringComparison.OrdinalIgnoreCase);
                    if (p >= 0 && (best < 0 || p < best)) best = p;
                }
                if (best >= 0)
                {
                    kinds.Add(k);
                    positions.Add(best);
                }
            }

            // 2) 按出现位置排序（插入排序，避免依赖 LINQ 与 C# 新语法）
            for (int i = 1; i < positions.Count; i++)
            {
                int pi = positions[i];
                int ki = kinds[i];
                int j = i - 1;
                while (j >= 0 && positions[j] > pi)
                {
                    positions[j + 1] = positions[j];
                    kinds[j + 1] = kinds[j];
                    j--;
                }
                positions[j + 1] = pi;
                kinds[j + 1] = ki;
            }

            // 3) 逐个构造动作：屏蔽其他组的关键词，只让当前组命中
            for (int i = 0; i < kinds.Count; i++)
            {
                string tmp = userText;
                for (int k = 0; k < groups.Length; k++)
                {
                    if (k == kinds[i]) continue;
                    for (int j = 0; j < groups[k].Length; j++)
                    {
                        tmp = tmp.Replace(groups[k][j], " ");
                    }
                }
                AiAgentAction a = Match(tmp);
                if (a == null) continue;

                bool dup = false;
                for (int d = 0; d < list.Count; d++)
                {
                    if (list[d].Id == a.Id) { dup = true; break; }
                }
                if (!dup) list.Add(a);
            }

            return list;
        }

        /// <summary>根据用户这句话，给出它能直接执行的动作（没有匹配则返回 null）。</summary>
        public static AiAgentAction Match(string userText)
        {
            if (string.IsNullOrWhiteSpace(userText)) return null;
            string hint = DetectModelHint(userText);
            NativeCore.InstalledMiningGpu gpu = PickGpu(hint);

            // 诊断必须排在解锁之前，否则"解锁失败了"会被当成"要解锁"
            if (ContainsAny(userText, DiagnoseKeywords))
            {
                return new AiAgentAction
                {
                    Id = "diagnose",
                    Title = "诊断解锁状态",
                    Description = "读取显卡型号、驱动版本、设备错误码、PCIe 链路与解锁寄存器，定位失败原因",
                    Executor = delegate(Action<string, bool> log)
                    {
                        return Task.Factory.StartNew(() => BuildUnlockDiagnosis(log));
                    }
                };
            }

            if (ContainsAny(userText, SpoofKeywords))
            {
                if (gpu == null) return null;
                string target = NativeCore.GetSafeDefaultSpoofTarget(gpu.DetectedModel, gpu.ChipArchitecture);
                if (string.IsNullOrEmpty(target)) target = gpu.TargetGeForceName;
                target = NativeCore.NormalizeGpuDisplayName(target);
                string finalTarget = target;
                return new AiAgentAction
                {
                    Id = "spoof",
                    Title = "伪装显卡型号",
                    Description = "把 [" + gpu.DetectedModel + "] 伪装为 " + finalTarget + "（同架构安全目标）",
                    Executor = delegate(Action<string, bool> log)
                    {
                        return Task.Factory.StartNew(() =>
                        {
                            bool ok = GpuSpoofManager.ApplyGpuRenameAsync(finalTarget, log).Result;
                            if (ok) GpuHardwareManager.SoftRestartGpuDriverAsync(log).Wait();
                            return ok ? "显卡已伪装为 " + finalTarget + "。" : "伪装未成功，请查看工具箱日志。";
                        });
                    }
                };
            }

            if (ContainsAny(userText, UnlockKeywords))
            {
                if (gpu == null) return null;
                return new AiAgentAction
                {
                    Id = "unlock",
                    Title = "执行显卡解锁",
                    Description = "对 [" + gpu.DetectedModel + "] 执行图形解锁与驱动层配置（需要管理员权限）",
                    IsDestructive = true,
                    Executor = delegate(Action<string, bool> log)
                    {
                        return Task.Factory.StartNew(() =>
                        {
                            string target = NativeCore.GetSafeDefaultSpoofTarget(gpu.DetectedModel, gpu.ChipArchitecture);
                            bool ok = PcieUnlockManager.ApplySingleGpuGraphicsUnlockAsync(gpu, log, target).Result;
                            if (ok) GpuHardwareManager.SoftRestartGpuDriverAsync(log).Wait();
                            return ok ? "已对 " + gpu.DetectedModel + " 执行解锁配置。" : "解锁未成功，请查看右侧日志。";
                        });
                    }
                };
            }

            if (ContainsAny(userText, OverlayKeywords))
            {
                return new AiAgentAction
                {
                    Id = "overlay",
                    Title = "解除游戏覆盖限制",
                    Description = "为 NVIDIA App 注入游戏内覆盖免检补丁，Alt+Z 覆盖可正常使用",
                    Executor = delegate(Action<string, bool> log)
                    {
                        return Task.Factory.StartNew(() =>
                        {
                            bool ok = EcosystemPatchManager.UnlockNvAppOverlayAsync(log).Result;
                            return ok ? "NVIDIA App 覆盖限制已解除。" : "覆盖补丁未成功，请确认已安装 NVIDIA App。";
                        });
                    }
                };
            }

            if (ContainsAny(userText, DualKeywords))
            {
                return new AiAgentAction
                {
                    Id = "dualgpu",
                    Title = "锁定首选高性能显卡",
                    Description = "把当前矿卡设为 DirectX 全局首选高性能显卡",
                    Executor = delegate(Action<string, bool> log)
                    {
                        return Task.Factory.StartNew(() =>
                        {
                            bool ok = GpuHardwareManager.ApplyDirectXGlobalDefaultGpu(log);
                            return ok ? "已锁定高性能显卡为全局首选。" : "写入失败，请以管理员身份重试。";
                        });
                    }
                };
            }

            if (ContainsAny(userText, DriverKeywords))
            {
                return new AiAgentAction
                {
                    Id = "restartdriver",
                    Title = "软重启显卡驱动",
                    Description = "热重载显示驱动，用于 Code 43 或改名后不刷新",
                    Executor = delegate(Action<string, bool> log)
                    {
                        return Task.Factory.StartNew(() =>
                        {
                            bool ok = GpuHardwareManager.SoftRestartGpuDriverAsync(log).Result;
                            return ok ? "显卡驱动已热重载。" : "驱动热重载失败，请重启电脑。";
                        });
                    }
                };
            }

            if (ContainsAny(userText, PowerKeywords))
            {
                return new AiAgentAction
                {
                    Id = "powersave",
                    Title = "启动系统级省电",
                    Description = "按本卡真实功耗下限启用空闲省电（数值来自驱动，不写死）",
                    Executor = delegate(Action<string, bool> log)
                    {
                        return Task.Factory.StartNew(() =>
                        {
                            bool ok = EcoPowerManager.InstallPowerSaverTask(log);
                            return ok ? "系统级省电已注册，空闲时自动降压降功耗。" : "省电任务安装失败，请以管理员身份重试。";
                        });
                    }
                };
            }

            return null;
        }

        public static string BuildRealtimeFacts()
        {
            StringBuilder sb = new StringBuilder();
            NativeCore.GpuSensorSnapshot snap = GpuHardwareManager.QueryGpuSensors();
            if (snap != null && snap.IsValid)
            {
                sb.Append("GPU=").Append(snap.GpuName);
                sb.Append("; 温度=").Append(snap.Temperature).Append("C");
                sb.Append("; 功耗=").Append(snap.PowerDraw.ToString("F1")).Append("W");
                if (snap.PowerLimit > 0) sb.Append("/上限").Append(snap.PowerLimit.ToString("F0")).Append("W");
                if (snap.PowerMaxLimit > 0) sb.Append("/硬件最大").Append(snap.PowerMaxLimit.ToString("F0")).Append("W");
                sb.Append("; 显存=").Append(snap.MemoryUsedMB).Append("/").Append(snap.MemoryTotalMB).Append("MB");
                sb.Append("; 频率=").Append(snap.GraphicsClockMHz).Append("MHz");
                sb.Append("; PState=").Append(snap.PState);
            }
            List<NativeCore.InstalledMiningGpu> gpus = GpuHardwareManager.DetectAllMiningGpus();
            if (gpus != null && gpus.Count > 0)
            {
                sb.Append("; 检测到的矿卡=");
                for (int i = 0; i < gpus.Count; i++)
                {
                    if (i > 0) sb.Append(" | ");
                    sb.Append(gpus[i].DetectedModel)
                      .Append("(槽").Append(gpus[i].SlotIndex + 1).Append(")")
                      .Append(" 安全伪装目标=")
                      .Append(NativeCore.GetSafeDefaultSpoofTarget(gpus[i].DetectedModel, gpus[i].ChipArchitecture));
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 给模型的"可用工具"清单。当前网关不一定支持 function calling，
        /// 所以用提示词注入：模型在回复末尾输出 [[ACTION:id]]，客户端解析后执行。
        /// </summary>
        public static string BuildToolCatalogue()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("可用动作（仅当判断用户想执行某个操作时，在回复最后单独一行输出 [[ACTION:id]]，一次最多一个）：");
            sb.AppendLine("- id=diagnose：诊断解锁状态。用户说解锁失败/没反应/报错/为什么不行时用");
            sb.AppendLine("- id=unlock：执行显卡解锁。用户明确要解锁显卡、开图形、跑固件解锁时用");
            sb.AppendLine("- id=spoof：伪装显卡型号。用户要改显卡显示名、伪装成某个型号时用");
            sb.AppendLine("- id=overlay：解锁游戏覆盖。用户要修 NVIDIA App 覆盖、录屏、滤镜时用");
            sb.AppendLine("- id=power：动态省电。用户要省电、降待机功耗、降温时用");
            sb.AppendLine("- id=driver：重启显卡驱动。用户遇到驱动异常、Code 43、黑屏时用");
            sb.AppendLine("- id=dual：双卡调度。用户提双卡、核显输出、首选渲染卡时用");
            sb.AppendLine("规则：普通问答不要输出动作标记；不确定就不要输出。");
            return sb.ToString();
        }

        /// <summary>从模型回复中提取动作标记，没有则返回 null。</summary>
        public static string ExtractActionId(string reply)
        {
            if (string.IsNullOrEmpty(reply)) return null;
            System.Text.RegularExpressions.Match m =
                System.Text.RegularExpressions.Regex.Match(reply, @"\[\[ACTION:([a-zA-Z]+)\]\]");
            if (!m.Success) return null;
            string id = m.Groups[1].Value.ToLowerInvariant();
            if (KeywordsById(id) == null) return null;
            return id;
        }

        /// <summary>按动作 id 构造可执行动作（屏蔽其他组关键词后交给 Match 走对应分支）。</summary>
        public static AiAgentAction BuildById(string id, string userText)
        {
            if (string.IsNullOrEmpty(id)) return null;
            string[] own = KeywordsById(id);
            if (own == null || own.Length == 0) return null;

            string text = (userText ?? "") + " " + own[0];
            string[][] groups = new string[][] {
                SpoofKeywords, UnlockKeywords, OverlayKeywords, PowerKeywords, DriverKeywords, DualKeywords, DiagnoseKeywords
            };
            for (int k = 0; k < groups.Length; k++)
            {
                if (groups[k] == own) continue;
                for (int j = 0; j < groups[k].Length; j++) text = text.Replace(groups[k][j], " ");
            }
            return Match(text);
        }

        /// <summary>
        /// 解锁失败诊断：把显卡、驱动、设备错误码、PCIe、解锁状态、引导环境全部读一遍，
        /// 给出可直接执行的结论（用户问"解锁失败了"时由 AI 调用）。
        /// </summary>
        public static string BuildUnlockDiagnosis(Action<string, bool> log)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("解锁诊断报告");
            sb.AppendLine();

            List<NativeCore.InstalledMiningGpu> gpus = GpuHardwareManager.DetectAllMiningGpus();
            if (gpus == null || gpus.Count == 0)
            {
                sb.AppendLine("结论：系统里没有检测到 CMP 矿卡。");
                sb.AppendLine("可能原因：显卡未插好 / 供电未接 / 驱动未安装 / 设备被禁用。");
                return sb.ToString();
            }

            bool anyProblem = false;
            bool anyComputeOff = false;
            bool anyPcieOff = false;

            for (int i = 0; i < gpus.Count; i++)
            {
                NativeCore.InstalledMiningGpu g = gpus[i];
                sb.AppendLine("[" + (i + 1) + "] " + g.DetectedModel + " · " + (g.ChipArchitecture ?? "未知架构"));
                sb.AppendLine("    当前描述: " + (g.CurrentDesc ?? "-"));
                sb.AppendLine("    设备实例: " + (g.DevInstanceId ?? "-"));
                sb.AppendLine("    显存: " + (g.VramCapacity ?? "-"));
                sb.AppendLine("    已伪装: " + (g.IsSpoofed ? "是" : "否"));
                sb.AppendLine("    算力解锁: " + (g.IsComputeUnlocked ? "是" : "否"));
                sb.AppendLine("    PCIe 解锁: " + (g.IsPcieUnlocked ? "是" : "否"));

                if (!g.IsComputeUnlocked) anyComputeOff = true;
                if (!g.IsPcieUnlocked) anyPcieOff = true;

                try
                {
                    string pnp = GpuHardwareManager.FindPnpInstanceIdByClassSubKey(g.ClassSubKey);
                    if (!string.IsNullOrEmpty(pnp))
                    {
                        int code = GpuHardwareManager.GetDeviceProblemCode(pnp);
                        if (code == 0)
                        {
                            sb.AppendLine("    设备状态码: 正常");
                        }
                        else
                        {
                            anyProblem = true;
                            sb.AppendLine("    设备状态码: Code " + code + "（驱动加载异常）");
                        }
                    }
                }
                catch { }

                sb.AppendLine();
            }

            try
            {
                SystemStatus st = SystemDetector.DetectAll();
                if (st != null && st.Hardware != null)
                {
                    sb.AppendLine("运行环境");
                    sb.AppendLine("    UEFI: " + (st.Hardware.UefiStatus ?? "-"));
                    sb.AppendLine("    Secure Boot: " + (st.Hardware.SecureBootStatus ?? "-"));
                    sb.AppendLine("    显卡驱动: " + (st.DriverVersion ?? "-"));
                    sb.AppendLine();
                }
            }
            catch { }

            sb.AppendLine("判读与建议");
            if (anyProblem)
            {
                sb.AppendLine("- 有设备报错误码，驱动没正常加载。先到设备管理器卸载该设备并重装配套驱动；Code 43 多为驱动与卡不匹配。");
            }
            if (anyComputeOff)
            {
                sb.AppendLine("- 算力仍未解锁。需要通过固件解锁流程（开机 EFI 执行一次），或检查该卡型号是否在支持列表内。");
            }
            if (anyPcieOff)
            {
                sb.AppendLine("- PCIe 仍在 Gen1 x4。若只跑了 Windows 内的驱动方案，PCIe 部分可能不生效；固件解锁才会一并放开。");
            }
            if (!anyProblem && !anyComputeOff && !anyPcieOff)
            {
                sb.AppendLine("- 各项状态看起来都正常。如果实际用起来还是不对，请说明具体表现（黑屏 / 跑分低 / 游戏报错 / 驱动装不上）。");
            }

            return sb.ToString();
        }
    }
}
