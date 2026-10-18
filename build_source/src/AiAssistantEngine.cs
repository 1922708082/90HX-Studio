using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CMP90HXToolbox
{
    #region 数据实体与配置模型

    /// <summary>
    /// AI 助手与中转站配置模型
    /// </summary>
    public class AiConfig
    {
        public string ApiBaseUrl { get; set; }
        public string ApiKey { get; set; }
        public string Model { get; set; }
        public double Temperature { get; set; }
        public int MaxTokens { get; set; }
        public int TimeoutSeconds { get; set; }
        public bool EnableOfflineFallback { get; set; }
        public string CustomSystemPrompt { get; set; }

        public AiConfig()
        {
            ApiBaseUrl = "https://api.muyaa.top/hx-assistant/v1";
            ApiKey = "";
            Model = "gemini-3.8-flash";
            Temperature = 0.7;
            MaxTokens = 2048;
            TimeoutSeconds = 20;
            EnableOfflineFallback = true;
            CustomSystemPrompt = DefaultPrompts.DefaultSystemPrompt;
        }

        /// <summary>
        /// 智能规整规范化 OpenAI /v1/chat/completions 请求地址
        /// </summary>
        public string GetNormalizedChatUrl()
        {
            string url = string.IsNullOrEmpty(ApiBaseUrl) ? "https://api.openai.com/v1" : ApiBaseUrl.Trim();
            if (url.EndsWith("/"))
            {
                url = url.Substring(0, url.Length - 1);
            }

            if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            if (url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                return url + "/chat/completions";
            }

            return url + "/v1/chat/completions";
        }
    }

    /// <summary>
    /// 聊天会话消息
    /// </summary>
    public class AiChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }

        public AiChatMessage()
        {
        }

        public AiChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }

    /// <summary>
    /// AI 诊断返回结果
    /// </summary>
    public class AiDiagnosisResult
    {
        public bool Success { get; set; }
        public string Answer { get; set; }
        public bool IsOfflineDiagnosis { get; set; }
        public string MatchedTopicId { get; set; }
        public string MatchedTopicTitle { get; set; }
        public string ErrorMessage { get; set; }
        public int TokensUsed { get; set; }
        public long DurationMs { get; set; }
        public string QuickCommand { get; set; }

        public AiDiagnosisResult()
        {
            Success = false;
            Answer = string.Empty;
            IsOfflineDiagnosis = false;
            MatchedTopicId = string.Empty;
            MatchedTopicTitle = string.Empty;
            ErrorMessage = string.Empty;
            TokensUsed = 0;
            DurationMs = 0;
            QuickCommand = string.Empty;
        }
    }

    /// <summary>
    /// 硬件环境上下文信息（用于给大模型注入精准设备状态）
    /// </summary>
    public class AiContextInfo
    {
        public string GpuModel { get; set; }
        public string DriverVersion { get; set; }
        public string Motherboard { get; set; }
        public string CpuName { get; set; }
        public string BiosVersion { get; set; }
        public string PcieLinkSpeed { get; set; }
        public string SecureBoot { get; set; }
        public string UefiStatus { get; set; }
        public string HasIntegratedGpu { get; set; }
        public string ExtraNotes { get; set; }

        public AiContextInfo()
        {
            GpuModel = "CMP 90HX";
            DriverVersion = "未知";
            Motherboard = "未知";
            CpuName = "未知";
            BiosVersion = "未知";
            PcieLinkSpeed = "未知";
            SecureBoot = "未知";
            UefiStatus = "UEFI";
            HasIntegratedGpu = "未知";
            ExtraNotes = string.Empty;
        }

        public static AiContextInfo FromMachineProfile(MachineHardwareProfile profile)
        {
            AiContextInfo ctx = new AiContextInfo();
            if (profile == null) return ctx;

            ctx.Motherboard = profile.MotherboardModel ?? "未知";
            ctx.CpuName = profile.CpuName ?? "未知";
            ctx.BiosVersion = profile.BiosVersion ?? "未知";
            ctx.SecureBoot = profile.SecureBootStatus ?? "未知";
            ctx.UefiStatus = profile.UefiStatus ?? "UEFI";

            if (profile.GpuList != null && profile.GpuList.Count > 0)
            {
                StringBuilder sbGpu = new StringBuilder();
                bool hasIgpu = false;
                for (int i = 0; i < profile.GpuList.Count; i++)
                {
                    GpuDeviceInfo g = profile.GpuList[i];
                    if (g != null)
                    {
                        if (sbGpu.Length > 0) sbGpu.Append(" | ");
                        sbGpu.Append(g.Name ?? "未知显卡");
                        if (!string.IsNullOrEmpty(g.DriverVersion))
                        {
                            sbGpu.Append(" (驱动: ").Append(g.DriverVersion).Append(")");
                        }
                        if (g.Name != null && (g.Name.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               g.Name.IndexOf("UHD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               g.Name.IndexOf("Iris", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               g.Name.IndexOf("Radeon(TM) Graphics", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            hasIgpu = true;
                        }
                    }
                }
                ctx.GpuModel = sbGpu.ToString();
                ctx.HasIntegratedGpu = hasIgpu ? "已检测到核显输出" : "未发现明显核显或使用独立点亮卡";
            }
            return ctx;
        }

        public string ToPromptSnippet()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("【当前用户宿主机检测到的硬件状态上下文】：");
            sb.Append("• 显卡设备: ").AppendLine(GpuModel ?? "未知");
            sb.Append("• 驱动版本: ").AppendLine(DriverVersion ?? "未知");
            sb.Append("• 处理器: ").AppendLine(CpuName ?? "未知");
            sb.Append("• 主板型号: ").AppendLine(Motherboard ?? "未知");
            sb.Append("• BIOS版本: ").AppendLine(BiosVersion ?? "未知");
            sb.Append("• 安全启动: ").AppendLine(SecureBoot ?? "未知");
            sb.Append("• 引导环境: ").AppendLine(UefiStatus ?? "UEFI");
            sb.Append("• 核显/亮机卡环境: ").AppendLine(HasIntegratedGpu ?? "未知");
            if (!string.IsNullOrEmpty(ExtraNotes))
            {
                sb.Append("• 额外状态: ").AppendLine(ExtraNotes);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 离线知识库问答条目
    /// </summary>
    public class OfflineFaqItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string[] Keywords { get; set; }
        public string Symptoms { get; set; }
        public string RootCause { get; set; }
        public string DiagnosisAndSteps { get; set; }
        public string QuickCommand { get; set; }

        public OfflineFaqItem(string id, string title, string category, string[] keywords, string symptoms, string rootCause, string steps, string quickCommand)
        {
            Id = id;
            Title = title;
            Category = category;
            Keywords = keywords ?? new string[0];
            Symptoms = symptoms ?? string.Empty;
            RootCause = rootCause ?? string.Empty;
            DiagnosisAndSteps = steps ?? string.Empty;
            QuickCommand = quickCommand ?? string.Empty;
        }
    }

    #endregion
    #region 离线急诊知识库（深度内嵌）

    /// <summary>
    /// CMP 矿卡硬件急诊专家离线知识库与精准匹配引擎
    /// </summary>
    public static class OfflineKnowledgeBase
    {
        private static readonly List<OfflineFaqItem> Items = new List<OfflineFaqItem>();

        static OfflineKnowledgeBase()
        {
            InitializeKnowledgeBase();
        }

        private static void InitializeKnowledgeBase()
        {
            // 1. Code 43 掉驱动排查与 PnP 自愈流程
            Items.Add(new OfflineFaqItem(
                "code43_driver_error",
                "Code 43 掉驱动排查与 PnP 自愈流程",
                "驱动与系统兼容",
                new string[] { "43", "code43", "code 43", "代码43", "掉驱动", "感叹号", "黄色感叹号", "设备停止", "停止工作", "驱动异常", "打不上驱动", "pnp", "签名", "nvpermissive" },
                "设备管理器中 NVIDIA CMP 90HX（或伪装后的 RTX 3080）出现黄色感叹号，属性提示：Windows 已停止此设备，因为该设备已报告有问题。(代码 43)。",
                "CMP 90HX 无物理显示输出，Windows 官方驱动在检测到设备无显示管道时会主动拒绝初始化。必须依赖 NVPermissive EFI 补丁在引导阶段绕过此限制。Code 43 通常由 UEFI 安全启动未关闭、引导链未经过 NVPermissive、显卡 8pin 供电松动或驱动版本冲突引起。",
                @"【Code 43 急诊与 PnP 自愈 5 步排查方案】：

步骤 1：排查 UEFI 安全启动 (Secure Boot)
• 重启进入主板 BIOS 设置（开机狂按 Del 或 F2）。
• 找到【Security】或【Boot】页面，务必将【Secure Boot】设为【Disabled】（关闭）。若开启，主板将拦截未签名的 NVPermissive EFI 补丁导致显卡脱核。

步骤 2：确认引导项是否通过 NVPermissive 启动
• 检查当前 Windows 是否从 NVPermissive 引导启动。
• 在本工具箱『引导与解锁』页面，检查 NVPermissive 状态，或点击【一键注入/修复引导】。如使用多系统引导，确保首选启动项为带有 90HX 解锁补丁的引导条目。

步骤 3：执行 PnP 驱动软复位命令（秒级自愈）
• 点击下方快捷命令按钮，或以管理员身份运行 CMD 执行：
  pnputil /restart-device ""PCI\VEN_10DE*""
• 该命令强制重置 NVIDIA 显卡 PnP 状态机，无需重启即可重新触发驱动握手。

步骤 4：硬件供电与金手指检查
• 关机断电，确认显卡双 8-Pin（或 3×8Pin）独立电源线已插紧插满（严禁使用单路一分二转接线）。
• 拔出显卡，用橡皮擦或无水酒精清洁 PCIe 金手指，排除氧化层接触不良。

步骤 5：彻底清理并重装推荐驱动
• 若上述无效，使用 DDU (Display Driver Uninstaller) 进入安全模式彻底卸载当前显卡驱动。
• 重新开机后安装本工具箱推荐的 528.49 或 537.58 魔改签名驱动包。",
                "pnputil /restart-device \"PCI\\VEN_10DE*\""
            ));

            // 2. PCIe 跑不满 Gen2 x16（冷重启 10 秒关键步骤与主板 BIOS 锁频）
            Items.Add(new OfflineFaqItem(
                "pcie_link_speed_gen2",
                "PCIe 跑不满 Gen2 x16（冷重启 10 秒关键步骤与主板 BIOS 锁频）",
                "总线与通道",
                new string[] { "pcie", "gen2", "gen1", "gen3", "gen4", "x16", "跑不满", "x1", "x8", "速率", "带宽", "1.1", "2.0", "3.0", "掉速", "降速", "帧率低", "卡顿", "冷重启", "放电" },
                "GPU-Z 或工具箱显示 PCIe 当前处于 x16 1.1、x8 1.1 或 x1 1.1 状态，开启渲染测试后依然无法跃升至 Gen2 (5.0 GT/s)，游戏内帧率严重偏低且频繁卡顿。",
                "CMP 90HX 基于 GA102 矿卡核心，晶圆在生产时并未经过消费级全速 Gen3/4 PCIe PHY 高频电气校准与兼容握手优化。当主板 PCIe 协商模式设为 [Auto] 时，极易协商失败跌落至 Gen1.1。此外，开机时电源瞬态供电未稳、主板电容残存杂波也会导致初始 PCIe PHY 链路握手降级。",
                @"【PCIe 跑不满 Gen2 终极破解——黄金三步法】：

★ 核心绝招：【冷重启 10 秒放电秘籍】（成功率超 90%）
1. 正常关闭电脑系统。
2. 拔掉主机背面的 220V 电源线（或关闭电源总开关）。
3. 长按机箱开机电源键 10 秒钟不放！彻底放空主板、电源滤波电容及显卡供电回路中的残余电荷。
4. 静置 15 秒后重新插上电源线，通电开机！
※ 原理：完全放电使 PCIe PHY 控制器硬件状态寄存器彻底硬复位，强制在通电瞬间与主板重新进行完整的全速链路时序协商。

步骤 2：主板 BIOS 强制锁定 PCIe Gen2
• 开机进入主板 BIOS 设置（按 Del 或 F2）。
• 找到【Advanced】->【PCI Subsystem Settings】或【System Agent (SA) Configuration】->【PEG Port Configuration】。
• 找到插放 90HX 的插槽（如 PCIEX16_1 或 Slot 1），将【PCIe Speed / Link Speed】从 [Auto] 强制修改为【Gen2】！
※ 绝对不要设为 Auto、Gen3 或 Gen4！90HX 不支持标准稳定 Gen3/4，强制 Gen2 才能实现 100% 满带宽无误码传输。

步骤 3：金手指与插槽阻抗清理
• 矿卡长期处于高温多尘环境，金手指表面存在不可见微氧化膜。
• 使用高纯度无水酒精棉球擦拭金手指正反面，并检查插槽内是否有灰尘棉絮。",
                "nvidia-smi -q -d PERFORMANCE"
            ));

            // 3. 核显 + 90HX 双卡 MS-Hybrid 跨卡渲染与游戏黑屏
            Items.Add(new OfflineFaqItem(
                "mshybrid_cross_render_blackscreen",
                "核显 + 90HX 双卡 MS-Hybrid 跨卡渲染与游戏黑屏",
                "多卡协同与显示",
                new string[] { "核显", "跨卡", "mshybrid", "ms-hybrid", "双卡", "黑屏", "无显示", "闪退", "不出力", "独显不工作", "游戏黑屏", "集显", "无信号", "副卡", "亮机卡", "核显输出" },
                "显示器连接在 CPU 核显（Intel UHD/Iris 或 AMD Radeon）或亮机卡（如 GT710/RX550）上。进入游戏时黑屏、启动闪退、只有声音无图像，或者游戏仍然由核显极低帧率渲染，90HX 占用率为 0%。",
                "90HX 物理无显示接口，必须借助 Windows DWM 与 Direct3D 交换链机制（MS-Hybrid 跨适配器呈现技术），将 90HX 渲染的帧缓冲通过 PCIe Copy Engine 跨总线传回核显显存输出。如果 Windows 图形首选项未重定向、核显显存预分配不足，或游戏默认使用独占全屏（Exclusive Fullscreen），就会引发交换链创建失败导致黑屏。",
                @"【MS-Hybrid 跨卡渲染与游戏黑屏全套自愈案】：

步骤 1：在 Windows 系统中指定游戏使用 90HX 高性能核心
1. 打开 Windows『设置』->『系统』->『屏幕』-> 滑到最底部点击『图形设置』。
2. 在“添加应用”中点击“浏览”，选中游戏的可执行文件（.exe）。
3. 点击该游戏条目下的『选项』，将其强制勾选为【高性能 (NVIDIA CMP 90HX / RTX 3080)】，点击保存。

步骤 2：BIOS 调大核显显存（至关重要！）
• 重启进入 BIOS，找到【Chipset / System Agent】->【Graphics Configuration】。
• 找到【DVMT Pre-Allocated】或【UMA Frame Buffer Size】（核显共享显存）。
• 将默认的 32M/64M 调大至【512M】或【1024M (1G)】！
※ 充足的核显缓冲区才能承载 90HX 传回的 2K/4K 高画质完整渲染帧，避免内存溢出黑屏。

步骤 3：消除独占全屏冲突——改用“无边框窗口化”
• 很多老游戏和特定 DirectX 引擎默认请求独占全屏模式，会导致 MS-Hybrid 跨卡握手黑屏。
• 解决办法：在游戏设置中将显示模式由【独占全屏】修改为【无边框窗口化 (Borderless Windowed)】或【窗口化全屏】。

步骤 4：注入 MS-Hybrid 跨卡优化注册表
• 使用工具箱『双卡协同』功能中的“一键配置跨卡渲染与 DirectShow 兼容补丁”，开启驱动内部跨卡 Copy Engine 硬件加速。",
                "explorer.exe ms-settings:display-advancedgraphics"
            ));
            // 4. 待机省电与恢复全速（显存 405MHz 死锁自愈）
            Items.Add(new OfflineFaqItem(
                "standby_power_405mhz_lock",
                "待机省电与恢复全速（显存 405MHz 死锁自愈）",
                "功耗与频率控制",
                new string[] { "405", "405mhz", "死锁", "降频", "锁频", "待机", "省电", "功耗高", "发热", "锁显存", "频率卡死", "p8", "p0", "频率低", "210mhz", "恢复全速" },
                "电脑待机时，90HX 功耗高达 70W~90W 居高不下，风扇狂转；或者反过来：进入大型 3D 游戏后，核心频率卡在 210MHz，显存频率死锁在 405MHz（P8 节能档位无法唤醒），游戏帧率只有 10~15 帧。",
                "CMP 矿卡 VBIOS 原生去除了显示引擎驱动模块，NVIDIA 驱动原有的基于垂直同步时序与显示活动触发的 P-State（电源状态）动态升降频机制失效，导致显卡要么卡在 P0 满血状态无法降频节能，要么卡在 P8 最低状态无法触发高负载睿频。",
                @"【405MHz 死锁秒级自愈与超低待机省电方案】：

★ 场景 A：游戏时显存 405MHz / 核心 210MHz 死锁自愈
1. 立即点击下方快捷命令按钮，或以管理员身份运行 CMD 执行：
   nvidia-smi -rgc
   nvidia-smi -rmc
   （该命令可立刻重置驱动锁频限制，恢复默认自动睿频曲线）。
2. 工具箱脉冲激活法：在工具箱『频率控制』面板点击【一键唤醒 P0 满血状态】，通过一段轻量级 CUDA 微负载触发驱动状态机切换至 1710MHz+ 满血频率。

★ 场景 B：日常办公/待机降温省电（将 80W 降至 15W 极低功耗）
1. 在工具箱『省电节能』页面，开启【动态待机节能守护】。
2. 守护进程在检测到系统无 3D 图形/计算负载时，会自动将显卡锁定在 P8 超低能耗模式，核心 210MHz、显存 405MHz、功耗骤降至 15W-18W，温度降低 25℃。
3. 一旦检测到游戏进程启动，毫秒级自动解锁并注入 P0 满血睿频，完全不影响游戏体验。",
                "nvidia-smi -rgc && nvidia-smi -rmc"
            ));

            // 5. NVIDIA App 游戏覆盖 Alt+R / Alt+Z 无法呼出与白名单
            Items.Add(new OfflineFaqItem(
                "nvapp_overlay_whitelist",
                "NVIDIA App 游戏覆盖 Alt+R / Alt+Z 无法呼出与白名单",
                "软件与辅助功能",
                new string[] { "nvidia app", "alt+r", "alt+z", "覆盖", "游戏覆盖", "overlay", "呼出", "无法呼出", "快捷键", "滤镜", "帧率显示", "geforce experience", "gfe", "不支持", "白名单" },
                "在官方 NVIDIA App 或 GeForce Experience 中，按下 Alt+Z（控制面板）或 Alt+R（性能监控浮窗）毫无反应；或者设置页面提示“需要支持的显卡”，游戏内覆盖开关显示为灰色不可点击。",
                "NVIDIA 官方软件后台服务会校验显卡硬件 Device ID。CMP 90HX 的原生硬件 ID（10DE:2207）被定义为数据中心/挖矿计算卡，官方软件白名单策略将其硬编码拉黑，禁止加载 NvCamera 与 NvOverlay 注入模块。",
                @"【NVIDIA App 游戏覆盖 Alt+R / Alt+Z 完美恢复三步法】：

步骤 1：使用工具箱一键注入 SHIM 白名单
• 工具箱已经深度适配 NVIDIA App 后台机制。
• 打开工具箱『增强工具』->『NVIDIA App 修复』，点击【一键打入 Overlay 修复补丁】。
• 工具箱将自动在系统层修改 `%LocalAppData%\NVIDIA Corporation\NVIDIA app\NvBackend\SHIM.json`，将 CMP 90HX 强制加入渲染与覆盖白名单。

步骤 2：显卡名称伪装为 RTX 3080
• 在工具箱首页点击【显卡名称伪装】，将显卡在注册表与系统信息中一键改写为『NVIDIA GeForce RTX 3080』。
• 改名后，NVIDIA 客户端软件与各大游戏检测均识别为正统 GeForce 游戏显卡，不再提示不受支持。

步骤 3：重启 NVIDIA 核心服务
• 以管理员权限运行以下命令（或点击快捷执行）：
  net stop NVDisplay.ContainerLocalSystem
  net start NVDisplay.ContainerLocalSystem
• 随后重新打开 NVIDIA App，在设置中即可顺畅开启【游戏内覆盖】，进入游戏后 Alt+R 完美浮现帧率、功耗与延迟！",
                "powershell -Command \"Restart-Service -Name 'NVDisplay.ContainerLocalSystem' -Force\""
            ));

            // 6. 华南/精粤 X79/X99 主板通道 B 引导卡死恢复
            Items.Add(new OfflineFaqItem(
                "huanan_x99_x79_pcie_stuck",
                "华南/精粤 X79/X99 主板通道 B 引导卡死恢复",
                "主板与平台兼容",
                new string[] { "x99", "x79", "华南", "精粤", "科脑", "通道b", "卡死", "卡b2", "卡code", "卡99", "卡67", "开机黑屏", "洋垃圾", "e5", "至强", "双路", "二桥", "通道冲突" },
                "在华南金牌 (Huananzhi)、精粤 (Jingyue)、科脑等 X79/X99 魔改主板插上 CMP 90HX 后，开机主板诊断卡卡在代码 B2、67、79 或 99，显示器不亮，键盘灯不响应，甚至无限自动重启。",
                "X79/X99 平台主板所配的至强 E5 处理器拥有高达 40 条 PCIe 通道，山寨主板厂商为引出双 PCIe 插槽，通常将第二条（通道 B）挂在第二控制器上或使用劣质通道拆分芯片。CMP 90HX 的大容量 10GB 显存需要开启大容量 MMIO (Above 4G Decoding) 映射，山寨 BIOS 的通道 B 逻辑极其脆弱，与 90HX 的 BAR 空间握手冲突导致卡死在 PCI 总线枚举阶段。",
                @"【华南/精粤 X79/X99 救砖与通道 B 终极适配指南】：

★ 规则一：严禁插在通道 B！务必插在『通道 A 主插槽』
• 华南/精粤主板只有最靠近 CPU 的第一条 PCIe x16 插槽（通道 A）是由 CPU 直出完整时序。
• 第二条插槽（通道 B）或走南桥 PCH 的黑色插槽绝对无法正常引导 90HX！请立刻将显卡拔下换到第一条主插槽。

★ 规则二：BIOS 核心开关组合（必须严格遵照）
1. 重启进入 BIOS，找到【PCI Subsystem Settings】：
   • 【Above 4G Decoding】（4G 以上解码）：务必开启【Enabled】！
   • 【PCIe Slot Link Speed】：必须强制锁定为【Gen2】！
2. 找到【Boot】选项：
   • 【CSM Support】：设为【Disabled】（或者若开启 CSM，将 Storage 与 Video OpROM 必须设为【UEFI Only】）。
   • 90HX 的 NVPermissive 补丁完全基于纯 64 位 UEFI 运行，Legacy 传统引导必然卡死。

★ 规则三：开机卡 B2/67 盲操放电复位
1. 断开 220V 电源，抠出主板上的 CR2032 纽扣电池。
2. 用螺丝刀金属短接电池座正负极 15 秒（或拔插 CLR_CMOS 跳线）。
3. 只保留 1 条内存插在 DIMM_A1 槽，开机触发主板 BIOS 自检完全重置，待正常点亮后重新调整上述 BIOS 参数。",
                "shutdown /r /fw /t 1"
            ));

            // 7. 额外调优：VBIOS 功耗墙与频率解锁指南
            Items.Add(new OfflineFaqItem(
                "vbios_power_limit",
                "VBIOS 功耗墙与频率解锁指南",
                "进阶调优",
                new string[] { "vbios", "功耗墙", "250w", "320w", "刷bios", "nvflash", "功耗限制", "超频", "power limit" },
                "显卡在烤机或高画质游戏中功耗撞墙（如被限制在 220W 或 250W），核心频率上不去。",
                "CMP 90HX 出厂多为挖矿定制 VBIOS，不同厂商（如微星、映众、技嘉等）给设定的功耗上限不同。",
                @"【90HX 功耗墙调优与安全性提醒】：
1. 90HX 硬件供电为 10+2 相或 12+2 相，默认 250W 功耗属于甜点能效区间。
2. 可在工具箱『超频与功耗』面板中将 Power Target 上调至 105%~110%。
3. 若需刷写 320W 高功耗 VBIOS，必须使用本工具箱定制版带证书绕过的 nvflashk 工具，刷写前必须备份原厂 ROM！",
                "nvidia-smi -pl 250"
            ));
        }

        public static List<OfflineFaqItem> GetAllItems()
        {
            return new List<OfflineFaqItem>(Items);
        }

        public static OfflineFaqItem FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Items.Count; i++)
            {
                if (string.Equals(Items[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return Items[i];
                }
            }
            return null;
        }

        /// <summary>
        /// 基于权重与模糊语义打分的离线急诊匹配
        /// </summary>
        public static AiDiagnosisResult MatchDiagnosis(string userQuery, AiContextInfo context)
        {
            AiDiagnosisResult result = new AiDiagnosisResult();
            result.IsOfflineDiagnosis = true;

            if (string.IsNullOrEmpty(userQuery))
            {
                userQuery = "";
            }

            string q = userQuery.ToLowerInvariant();
            OfflineFaqItem bestItem = null;
            int highestScore = 0;

            for (int i = 0; i < Items.Count; i++)
            {
                OfflineFaqItem item = Items[i];
                int score = 0;

                // 标题匹配
                if (q.IndexOf(item.Title.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 50;
                }

                // 关键词权重计算
                for (int k = 0; k < item.Keywords.Length; k++)
                {
                    string kw = item.Keywords[k].ToLowerInvariant();
                    if (string.IsNullOrEmpty(kw)) continue;

                    if (q.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (kw.Length <= 2)
                        {
                            score += 15;
                        }
                        else
                        {
                            score += 25;
                        }
                    }
                }

                // 上下文加权
                if (context != null)
                {
                    if (item.Id == "huanan_x99_x79_pcie_stuck" &&
                        !string.IsNullOrEmpty(context.Motherboard) &&
                        (context.Motherboard.IndexOf("X99", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         context.Motherboard.IndexOf("X79", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        score += 30;
                    }

                    if (item.Id == "code43_driver_error" &&
                        !string.IsNullOrEmpty(context.SecureBoot) &&
                        context.SecureBoot.IndexOf("Enabled", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        score += 20;
                    }
                }

                if (score > highestScore)
                {
                    highestScore = score;
                    bestItem = item;
                }
            }

            // 命中有效知识点
            if (bestItem != null && highestScore >= 15)
            {
                result.Success = true;
                result.MatchedTopicId = bestItem.Id;
                result.MatchedTopicTitle = bestItem.Title;
                result.QuickCommand = bestItem.QuickCommand;

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("【CMP 矿卡硬件急诊专家 · 离线知识库即时诊断】");
                sb.Append("针对故障分类：").AppendLine(bestItem.Category);
                sb.Append("匹配诊断主题：").AppendLine(bestItem.Title);
                sb.AppendLine("--------------------------------------------------");
                if (!string.IsNullOrEmpty(bestItem.Symptoms))
                {
                    sb.Append("【典型症状】：\n").AppendLine(bestItem.Symptoms);
                }
                if (!string.IsNullOrEmpty(bestItem.RootCause))
                {
                    sb.Append("【根本病因】：\n").AppendLine(bestItem.RootCause);
                }
                sb.AppendLine(bestItem.DiagnosisAndSteps);
                if (!string.IsNullOrEmpty(bestItem.QuickCommand))
                {
                    sb.AppendLine("--------------------------------------------------");
                    sb.Append("【工具箱建议快捷执行命令】：\n  ").AppendLine(bestItem.QuickCommand);
                }

                result.Answer = sb.ToString();
                return result;
            }

            // 兜底通用体检方案
            result.Success = true;
            result.MatchedTopicId = "general_fallback";
            result.MatchedTopicTitle = "CMP 90HX 全生命周期通用排查与自愈指引";
            result.Answer = BuildFallbackAnswer(userQuery, context);
            return result;
        }

        private static string BuildFallbackAnswer(string userQuery, AiContextInfo context)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("【CMP 矿卡硬件急诊专家 · 离线智能诊断】");
            sb.AppendLine("未能直接命中特定单一故障编号，已为你启动 CMP 90HX / 矿卡家族【黄金急诊六部曲】：");
            sb.AppendLine("--------------------------------------------------");

            if (context != null)
            {
                sb.Append("• 宿主机主板: ").Append(context.Motherboard).Append(" | 处理器: ").AppendLine(context.CpuName);
                sb.Append("• 当前检测显卡: ").AppendLine(context.GpuModel);
                sb.AppendLine("--------------------------------------------------");
            }

            sb.AppendLine("1. [Code 43 掉驱动]：关闭 BIOS 中的 Secure Boot，并在本工具箱检查 NVPermissive 引导。");
            sb.AppendLine("2. [PCIe 速率跑不满]：关机断电，按住机箱开机键 10 秒彻底放电，并在 BIOS 中强制锁 Gen2。");
            sb.AppendLine("3. [游戏黑屏闪退]：将游戏设为无边框窗口化，并在 Windows 图形设置中指定 90HX 为高性能卡。");
            sb.AppendLine("4. [显存死锁 405MHz]：以管理员运行 cmd 执行 `nvidia-smi -rgc && nvidia-smi -rmc` 重置锁定。");
            sb.AppendLine("5. [NVIDIA App 覆盖打不开]：在工具箱注入 SHIM.json 白名单并将显卡名称伪装为 RTX 3080。");
            sb.AppendLine("6. [X79/X99 平台开机卡自检]：严禁插在副插槽，插靠近 CPU 第一槽，开机卡 B2 时扣电池放电复位。");
            sb.AppendLine();
            sb.AppendLine("💡 提示：如需针对“" + (string.IsNullOrEmpty(userQuery) ? "具体故障" : userQuery) + "”进行更深度分析，可随时配置中转站 API Key 接入云端大模型！");
            return sb.ToString();
        }
    }

    #endregion
    #region 极简原生零依赖 JSON 解析与序列化器

    /// <summary>
    /// 纯原生 C# 5 零外部依赖 MiniJson 解析与序列化工具
    /// 满足复杂嵌套字典、列表、Unicode字符转义、格式容错
    /// </summary>
    public static class SimpleJson
    {
        public static string Serialize(object obj)
        {
            StringBuilder sb = new StringBuilder();
            SerializeValue(obj, sb);
            return sb.ToString();
        }

        private static void SerializeValue(object obj, StringBuilder sb)
        {
            if (obj == null)
            {
                sb.Append("null");
                return;
            }

            if (obj is string)
            {
                SerializeString((string)obj, sb);
                return;
            }

            if (obj is bool)
            {
                sb.Append(((bool)obj) ? "true" : "false");
                return;
            }

            if (obj is int || obj is long || obj is short || obj is byte)
            {
                sb.Append(obj.ToString());
                return;
            }

            if (obj is float || obj is double || obj is decimal)
            {
                sb.Append(Convert.ToString(obj, System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            if (obj is IDictionary<string, object>)
            {
                IDictionary<string, object> dict = (IDictionary<string, object>)obj;
                sb.Append("{");
                bool first = true;
                foreach (KeyValuePair<string, object> kvp in dict)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    SerializeString(kvp.Key, sb);
                    sb.Append(":");
                    SerializeValue(kvp.Value, sb);
                }
                sb.Append("}");
                return;
            }

            if (obj is System.Collections.IEnumerable)
            {
                System.Collections.IEnumerable list = (System.Collections.IEnumerable)obj;
                sb.Append("[");
                bool first = true;
                foreach (object item in list)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    SerializeValue(item, sb);
                }
                sb.Append("]");
                return;
            }

            SerializeString(obj.ToString(), sb);
        }

        private static void SerializeString(string str, StringBuilder sb)
        {
            sb.Append('"');
            if (!string.IsNullOrEmpty(str))
            {
                for (int i = 0; i < str.Length; i++)
                {
                    char c = str[i];
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < 32)
                            {
                                sb.AppendFormat("\\u{0:x4}", (int)c);
                            }
                            else
                            {
                                sb.Append(c);
                            }
                            break;
                    }
                }
            }
            sb.Append('"');
        }

        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int index = 0;
            return ParseValue(json, ref index);
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }
        }

        private static object ParseValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length) return null;

            char c = json[index];
            if (c == '{') return ParseObject(json, ref index);
            if (c == '[') return ParseArray(json, ref index);
            if (c == '"') return ParseString(json, ref index);
            if (c == 't' || c == 'T' || c == 'f' || c == 'F') return ParseBoolean(json, ref index);
            if (c == 'n' || c == 'N') return ParseNull(json, ref index);
            if (c == '-' || char.IsDigit(c)) return ParseNumber(json, ref index);

            index++;
            return null;
        }

        private static Dictionary<string, object> ParseObject(string json, ref int index)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            index++;

            while (index < json.Length)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length) break;
                if (json[index] == '}')
                {
                    index++;
                    return dict;
                }

                if (json[index] == ',')
                {
                    index++;
                    continue;
                }

                string key = ParseString(json, ref index);
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ':')
                {
                    index++;
                }

                object val = ParseValue(json, ref index);
                dict[key] = val;
            }

            return dict;
        }

        private static List<object> ParseArray(string json, ref int index)
        {
            List<object> list = new List<object>();
            index++;

            while (index < json.Length)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length) break;
                if (json[index] == ']')
                {
                    index++;
                    return list;
                }

                if (json[index] == ',')
                {
                    index++;
                    continue;
                }

                object val = ParseValue(json, ref index);
                list.Add(val);
            }

            return list;
        }

        private static string ParseString(string json, ref int index)
        {
            StringBuilder sb = new StringBuilder();
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index] != '"') return string.Empty;
            index++;

            while (index < json.Length)
            {
                char c = json[index++];
                if (c == '"')
                {
                    return sb.ToString();
                }

                if (c == '\\')
                {
                    if (index >= json.Length) break;
                    char esc = json[index++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (index + 4 <= json.Length)
                            {
                                string hex = json.Substring(index, 4);
                                index += 4;
                                int code;
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out code))
                                {
                                    sb.Append((char)code);
                                }
                            }
                            break;
                        default:
                            sb.Append(esc);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static bool ParseBoolean(string json, ref int index)
        {
            if (index + 4 <= json.Length && string.Compare(json.Substring(index, 4), "true", StringComparison.OrdinalIgnoreCase) == 0)
            {
                index += 4;
                return true;
            }
            if (index + 5 <= json.Length && string.Compare(json.Substring(index, 5), "false", StringComparison.OrdinalIgnoreCase) == 0)
            {
                index += 5;
                return false;
            }
            index++;
            return false;
        }

        private static object ParseNull(string json, ref int index)
        {
            if (index + 4 <= json.Length && string.Compare(json.Substring(index, 4), "null", StringComparison.OrdinalIgnoreCase) == 0)
            {
                index += 4;
            }
            else
            {
                index++;
            }
            return null;
        }

        private static object ParseNumber(string json, ref int index)
        {
            int start = index;
            while (index < json.Length)
            {
                char c = json[index];
                if (char.IsDigit(c) || c == '.' || c == '-' || c == '+' || c == 'e' || c == 'E')
                {
                    index++;
                }
                else
                {
                    break;
                }
            }
            string numStr = json.Substring(start, index - start);
            long l;
            if (long.TryParse(numStr, out l))
            {
                if (l >= int.MinValue && l <= int.MaxValue) return (int)l;
                return l;
            }
            double d;
            if (double.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d))
            {
                return d;
            }
            return 0;
        }

        #region 字典辅助读取

        public static string GetString(Dictionary<string, object> dict, string key, string defVal)
        {
            if (dict != null && dict.ContainsKey(key) && dict[key] != null)
            {
                return dict[key].ToString();
            }
            return defVal;
        }

        public static int GetInt(Dictionary<string, object> dict, string key, int defVal)
        {
            if (dict != null && dict.ContainsKey(key) && dict[key] != null)
            {
                try
                {
                    return Convert.ToInt32(dict[key]);
                }
                catch { }
            }
            return defVal;
        }

        public static double GetDouble(Dictionary<string, object> dict, string key, double defVal)
        {
            if (dict != null && dict.ContainsKey(key) && dict[key] != null)
            {
                try
                {
                    return Convert.ToDouble(dict[key], System.Globalization.CultureInfo.InvariantCulture);
                }
                catch { }
            }
            return defVal;
        }

        public static bool GetBool(Dictionary<string, object> dict, string key, bool defVal)
        {
            if (dict != null && dict.ContainsKey(key) && dict[key] != null)
            {
                try
                {
                    return Convert.ToBoolean(dict[key]);
                }
                catch { }
            }
            return defVal;
        }

        public static Dictionary<string, object> GetDict(Dictionary<string, object> dict, string key)
        {
            if (dict != null && dict.ContainsKey(key))
            {
                return dict[key] as Dictionary<string, object>;
            }
            return null;
        }

        public static List<object> GetList(Dictionary<string, object> dict, string key)
        {
            if (dict != null && dict.ContainsKey(key))
            {
                return dict[key] as List<object>;
            }
            return null;
        }

        #endregion
    }

    #endregion

    #region 默认 System Prompt

    public static class DefaultPrompts
    {
        public const string DefaultSystemPrompt =
@"你是由 90HX Studio / HX家族多功能工具箱 开发的【CMP 矿卡硬件急诊专家 AI】。
你精通 NVIDIA CMP 30HX/40HX/50HX/70HX/90HX/170HX 系列魔改显卡、魔改驱动、NVPermissive EFI 解锁补丁、MS-Hybrid 跨卡渲染、PCIe 拓扑协商、以及老旧至强 X79/X99 主板硬件避坑技术。

你的回答准则：
1. 【直击要害】：先给出最关键的自愈方案与判断依据，拒绝无意义套话。
2. 【保姆级操作】：提供具体可执行的步骤（如 BIOS 怎么按、命令怎么敲、工具箱哪个按钮点击）。
3. 【硬件实操诀窍】：例如 Code 43 需优先核验 UEFI 安全启动关闭；PCIe 跑不满 Gen2 优先使用冷重启放电 10 秒法则；双卡黑屏优先将游戏设为无边框窗口化并分配核显大显存等。
4. 【语气亲切】：技术极客风格，条理分明，用词专业准确。";
    }

    #endregion
    #region AI 助手主引擎核心服务

    /// <summary>
    /// AI 硬件诊断助手后端服务引擎
    /// 包含 OpenAI 兼容标准接口客户端、离线自愈知识库、配置持久化与上下文融合
    /// </summary>
    public class AiAssistantEngine
    {
        private static readonly object _syncLock = new object();
        private static AiAssistantEngine _instance;

        public static AiAssistantEngine Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_syncLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new AiAssistantEngine();
                        }
                    }
                }
                return _instance;
            }
        }

        public AiConfig Config { get; set; }

        public event Action<string> OnLog;

        private void Log(string message)
        {
            if (OnLog != null)
            {
                try { OnLog(message); } catch { }
            }
        }

        private AiAssistantEngine()
        {
            Config = new AiConfig();
            LoadConfig();
            EnsureTlsProtocols();
        }

        private void EnsureTlsProtocols()
        {
            try
            {
                // 开启 TLS 1.2 支持，确保与现代反代/HTTPS 服务兼容
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch { }
        }

        #region 配置持久化 (%ProgramData%\CMP90HX_Toolbox\ai_config.json)

        public string GetConfigFilePath()
        {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (string.IsNullOrEmpty(programData))
            {
                programData = @"C:\ProgramData";
            }
            string dir = Path.Combine(programData, "CMP90HX_Toolbox");
            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }
            return Path.Combine(dir, "ai_config.json");
        }

        public bool SaveConfig()
        {
            try
            {
                string path = GetConfigFilePath();
                Dictionary<string, object> dict = new Dictionary<string, object>();
                dict["ApiBaseUrl"] = Config.ApiBaseUrl ?? "";
                dict["ApiKey"] = Config.ApiKey ?? "";
                dict["Model"] = Config.Model ?? "gpt-4o-mini";
                dict["Temperature"] = Config.Temperature;
                dict["MaxTokens"] = Config.MaxTokens;
                dict["TimeoutSeconds"] = Config.TimeoutSeconds;
                dict["EnableOfflineFallback"] = Config.EnableOfflineFallback;
                dict["CustomSystemPrompt"] = Config.CustomSystemPrompt ?? DefaultPrompts.DefaultSystemPrompt;

                string json = SimpleJson.Serialize(dict);
                File.WriteAllText(path, json, Encoding.UTF8);
                Log("[AI] 成功持久化保存 AI 引擎配置: " + path);
                return true;
            }
            catch (Exception ex)
            {
                Log("[AI ERROR] 保存配置失败: " + ex.Message);
                return false;
            }
        }

        public bool LoadConfig()
        {
            try
            {
                string path = GetConfigFilePath();
                if (!File.Exists(path))
                {
                    return false;
                }

                string json = File.ReadAllText(path, Encoding.UTF8);
                Dictionary<string, object> dict = SimpleJson.Deserialize(json) as Dictionary<string, object>;
                if (dict == null) return false;

                Config.ApiBaseUrl = SimpleJson.GetString(dict, "ApiBaseUrl", "https://api.openai.com/v1");
                Config.ApiKey = SimpleJson.GetString(dict, "ApiKey", "");
                Config.Model = SimpleJson.GetString(dict, "Model", "gpt-4o-mini");
                Config.Temperature = SimpleJson.GetDouble(dict, "Temperature", 0.7);
                Config.MaxTokens = SimpleJson.GetInt(dict, "MaxTokens", 2048);
                Config.TimeoutSeconds = SimpleJson.GetInt(dict, "TimeoutSeconds", 20);
                Config.EnableOfflineFallback = SimpleJson.GetBool(dict, "EnableOfflineFallback", true);
                Config.CustomSystemPrompt = SimpleJson.GetString(dict, "CustomSystemPrompt", DefaultPrompts.DefaultSystemPrompt);

                Log("[AI] 成功从本地载入 AI 配置 (BaseUrl: " + Config.ApiBaseUrl + ", Model: " + Config.Model + ")");
                return true;
            }
            catch (Exception ex)
            {
                Log("[AI ERROR] 读取本地配置失败，恢复默认: " + ex.Message);
                return false;
            }
        }

        #endregion

        #region 问答诊断 API (支持 OpenAI 兼容请求与离线降级)

        /// <summary>
        /// 异步问答接口（OpenAI 兼容协议客户端，支持 20 秒超时与离线降级）
        /// </summary>
        public Task<AiDiagnosisResult> AskAsync(string userPrompt, AiContextInfo context = null, List<AiChatMessage> history = null)
        {
            return Task.Factory.StartNew<AiDiagnosisResult>(() =>
            {
                Stopwatch sw = Stopwatch.StartNew();

                // 1. 如果用户未配置 API Key，直接调用离线急诊知识库
                if (string.IsNullOrEmpty(Config.ApiKey) || string.IsNullOrEmpty(Config.ApiKey.Trim()))
                {
                    Log("[AI] 未检测到 API Key，直接调用 CMP 离线急诊专家知识库...");
                    AiDiagnosisResult offlineResult = OfflineKnowledgeBase.MatchDiagnosis(userPrompt, context);
                    offlineResult.DurationMs = sw.ElapsedMilliseconds;
                    return offlineResult;
                }

                // 2. 发起 OpenAI 兼容 HTTP 请求
                try
                {
                    Log("[AI] 正在向中转站发起诊断请求 (URL: " + Config.GetNormalizedChatUrl() + ", 模型: " + Config.Model + ") ...");
                    string responseJson = SendChatCompletionRequest(userPrompt, context, history);
                    AiDiagnosisResult onlineResult = ParseOpenAiResponse(responseJson);
                    onlineResult.DurationMs = sw.ElapsedMilliseconds;
                    return onlineResult;
                }
                catch (Exception ex)
                {
                    Log("[AI 警告] 在线 API 请求异常: " + ex.Message);

                    // 若允许降级，返回离线急诊结果并提示网络异常原因
                    if (Config.EnableOfflineFallback)
                    {
                        Log("[AI] 触发离线兜底机制，返回内置急诊知识库匹配结果...");
                        AiDiagnosisResult fallbackResult = OfflineKnowledgeBase.MatchDiagnosis(userPrompt, context);
                        fallbackResult.DurationMs = sw.ElapsedMilliseconds;
                        fallbackResult.ErrorMessage = "在线 API 连接受阻 (" + ex.Message + ")，已自动切换为内置专家知识库。";
                        return fallbackResult;
                    }
                    else
                    {
                        AiDiagnosisResult errResult = new AiDiagnosisResult();
                        errResult.Success = false;
                        errResult.ErrorMessage = "请求失败: " + ex.Message;
                        errResult.DurationMs = sw.ElapsedMilliseconds;
                        return errResult;
                    }
                }
            });
        }

        /// <summary>
        /// 直接进行本地离线智能急诊诊断（不发起网络请求）
        /// </summary>
        public AiDiagnosisResult DiagnoseOffline(string userPrompt, AiContextInfo context = null)
        {
            Stopwatch sw = Stopwatch.StartNew();
            AiDiagnosisResult res = OfflineKnowledgeBase.MatchDiagnosis(userPrompt, context);
            res.DurationMs = sw.ElapsedMilliseconds;
            return res;
        }

        /// <summary>
        /// 获取内置的所有离线诊断条目（可供 UI 渲染故障快捷排查卡片）
        /// </summary>
        public List<OfflineFaqItem> GetAllFaqItems()
        {
            return OfflineKnowledgeBase.GetAllItems();
        }

        #endregion

        #region HTTP 请求与协议封装 (HttpWebRequest 20秒超时防护)

        private string SendChatCompletionRequest(string userPrompt, AiContextInfo context, List<AiChatMessage> history)
        {
            string url = Config.GetNormalizedChatUrl();
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.UserAgent = "90HXStudio-AiAssistant/5.0 (.NET Framework)";

            // 认证头
            string key = (Config.ApiKey ?? "").Trim();
            if (!string.IsNullOrEmpty(key))
            {
                request.Headers["Authorization"] = "Bearer " + key;
            }

            // 超时保护（20秒）
            int timeoutMs = (Config.TimeoutSeconds > 0 ? Config.TimeoutSeconds : 20) * 1000;
            request.Timeout = timeoutMs;
            request.ReadWriteTimeout = timeoutMs;

            // 组装 Messages 列表
            List<object> messagesList = new List<object>();

            // System Message (包含硬件急诊专家人设与上下文)
            StringBuilder sbSystem = new StringBuilder();
            sbSystem.AppendLine(string.IsNullOrEmpty(Config.CustomSystemPrompt) ? DefaultPrompts.DefaultSystemPrompt : Config.CustomSystemPrompt);
            if (context != null)
            {
                sbSystem.AppendLine();
                sbSystem.AppendLine(context.ToPromptSnippet());
            }

            Dictionary<string, object> msgSys = new Dictionary<string, object>();
            msgSys["role"] = "system";
            msgSys["content"] = sbSystem.ToString();
            messagesList.Add(msgSys);

            // 历史消息（若有）
            if (history != null && history.Count > 0)
            {
                for (int i = 0; i < history.Count; i++)
                {
                    AiChatMessage m = history[i];
                    if (m != null && !string.IsNullOrEmpty(m.Content))
                    {
                        Dictionary<string, object> msgHist = new Dictionary<string, object>();
                        msgHist["role"] = m.Role ?? "user";
                        msgHist["content"] = m.Content;
                        messagesList.Add(msgHist);
                    }
                }
            }

            // 当前用户问题
            Dictionary<string, object> msgUser = new Dictionary<string, object>();
            msgUser["role"] = "user";
            msgUser["content"] = userPrompt ?? "";
            messagesList.Add(msgUser);

            // 根请求 Payload
            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["model"] = string.IsNullOrEmpty(Config.Model) ? "gpt-4o-mini" : Config.Model.Trim();
            payload["messages"] = messagesList;
            payload["temperature"] = Config.Temperature;
            payload["max_tokens"] = Config.MaxTokens > 0 ? Config.MaxTokens : 2048;

            string requestJson = SimpleJson.Serialize(payload);
            byte[] bytes = Encoding.UTF8.GetBytes(requestJson);
            request.ContentLength = bytes.Length;

            using (Stream reqStream = request.GetRequestStream())
            {
                reqStream.Write(bytes, 0, bytes.Length);
            }

            // 接收响应并处理异常
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException webEx)
            {
                string errDetail = webEx.Message;
                if (webEx.Response != null)
                {
                    try
                    {
                        using (Stream errStream = webEx.Response.GetResponseStream())
                        using (StreamReader reader = new StreamReader(errStream, Encoding.UTF8))
                        {
                            string errJson = reader.ReadToEnd();
                            Dictionary<string, object> errObj = SimpleJson.Deserialize(errJson) as Dictionary<string, object>;
                            if (errObj != null)
                            {
                                Dictionary<string, object> errChild = SimpleJson.GetDict(errObj, "error");
                                if (errChild != null)
                                {
                                    string apiMsg = SimpleJson.GetString(errChild, "message", "");
                                    if (!string.IsNullOrEmpty(apiMsg))
                                    {
                                        errDetail = apiMsg;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
                throw new Exception("API 接口响应错误: " + errDetail, webEx);
            }
        }

        private AiDiagnosisResult ParseOpenAiResponse(string json)
        {
            AiDiagnosisResult result = new AiDiagnosisResult();
            if (string.IsNullOrEmpty(json))
            {
                result.Success = false;
                result.ErrorMessage = "服务端返回空响应";
                return result;
            }

            Dictionary<string, object> root = SimpleJson.Deserialize(json) as Dictionary<string, object>;
            if (root == null)
            {
                result.Success = false;
                result.ErrorMessage = "无法解析服务端响应 JSON";
                return result;
            }

            // 检查 error 字段
            Dictionary<string, object> err = SimpleJson.GetDict(root, "error");
            if (err != null)
            {
                result.Success = false;
                result.ErrorMessage = SimpleJson.GetString(err, "message", "未知 API 错误");
                return result;
            }

            // 解析 choices[0].message.content
            List<object> choices = SimpleJson.GetList(root, "choices");
            if (choices != null && choices.Count > 0)
            {
                Dictionary<string, object> firstChoice = choices[0] as Dictionary<string, object>;
                if (firstChoice != null)
                {
                    Dictionary<string, object> msg = SimpleJson.GetDict(firstChoice, "message");
                    if (msg != null)
                    {
                        result.Answer = SimpleJson.GetString(msg, "content", "");
                        result.Success = true;
                    }
                }
            }

            // 解析 tokens usage
            Dictionary<string, object> usage = SimpleJson.GetDict(root, "usage");
            if (usage != null)
            {
                result.TokensUsed = SimpleJson.GetInt(usage, "total_tokens", 0);
            }

            if (!result.Success && string.IsNullOrEmpty(result.Answer))
            {
                result.Success = false;
                result.ErrorMessage = "响应中未包含有效的文本回答内容";
            }

            return result;
        }

        #endregion
    }

    #endregion
}