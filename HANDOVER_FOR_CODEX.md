# 90HX Studio - AI 编程助手（Codex）开发交接文档

> **文档版本**：v2.7.0-Handover  
> **更新时间**：2026-10-04  
> **适用对象**：后续接手维护与功能迭代的 AI 编程助手（Codex / Copilot / Claude）及开发者  
> **工作区根目录**：`D:/app/`  

---

## 1. 项目背景与当前开发进展 (Project Overview & Status)

### 1.1 项目定位与核心目标
**90HX Studio**（曾用名：CMP 90HX 控制中心）是一款专为 NVIDIA CMP 全系矿卡（30HX / 40HX / 50HX / 70HX / 90HX / 170HX）打造的高性能硬件解锁与跨卡调度桌面套件。

CMP 系列显卡出厂时被 NVIDIA 官方在固件与驱动层施加了多重人为限制：
1. **PCIe 总线速率封锁**：官方限制在 PCIe 1.1 (x4/x16)；
2. **算力节流与熔断机制**：驱动中存在 `Compute Throttle` 与 P-State 限制；
3. **图形渲染被屏蔽**：WDDM 模式与 Direct3D 3A 游戏光栅化渲染被软硬件双重锁死；
4. **无物理视频输出接口**：必须依赖 CPU 核显（Intel/AMD）或独立亮机卡进行物理画面输出；
5. **NVIDIA App 覆盖拦截**：新版 NVIDIA App 通过云端硬件白名单屏蔽矿卡的 ShadowPlay 录屏与 Alt+R 性能面板。

本项目旨在通过**纯原生 Windows 桌面应用**，一键穿透上述限制，实现：
* **PCIe 速率解锁**：突破至 Gen2 x16（双向 10 GB/s）；
* **固件级算力解锁**：集成 WildFlash1st 核心，解除 SEC2/PLM 锁，大模型推理实测提速约 16 倍；
* **显卡型号深度伪装**：将矿卡深度伪装为对应架构的 GeForce 显卡（如 90HX➔RTX 3080、40HX➔RTX 2060S、50HX➔RTX 2080Ti），彻底激活 Direct3D 游戏图形渲染与设备管理器识别；
* **MS-Hybrid 跨卡调度**：利用 Direct3D CASO 技术，将矿卡渲染的画面极速复制给核显/输出卡显示；
* **DLSS 3 帧生成注入**：为 RTX 30 系列 (GA102) 架构激活 2X~6X 动态插帧；
* **全单文件绿色免安装交付**：所有固件、脚本、DLSS 运行库及收款二维码全部编译打包进单文件 EXE（约 56.5MB），零外部依赖，即拷即用。

### 1.2 当前已完成的核心成果与最新进展
* **UI 现代化改造**：遵循 **Clean Studio 苹果极简纯白规范**，左侧现代垂直导航，彻底移除了所有冗长啰嗦的问号说明徽章与花哨边框，界面清爽通透。
* **苹果风 3D 浮雕启动动画**：无边框全透明背景，中心纯粹展示 148×148px 苹果黄金圆角曜石黑金属渐变「90」图标，配合 Apple Spring 弹簧阻尼放大（0.68➔1.04➔1.00）与 28° Apple Shine 高光扫掠。
* **单文件自给自足架构**：内置 55.75MB 的加密资源包 `core.pkg`（含 40HX/30HX 官方工具、90HX compute/full EFI 镜像、DLSS 运行时）以及赞赏码图片资源，运行时自动从内存流加载或安全释放。
* **显卡型号伪装全链路修复（2026-10-03 核心攻坚）**：
  * 根治了“显卡型号伪装不生效”的历史顽疾：不仅写入 `Control\Class`、`Control\Video`、`Services\nvlddmkm\Video`、`DirectX` 注册表项，更实现了**动态定位并同步修改 `C:\Windows\INF\oemXX.inf` 与 `DriverStore` 驱动定义**；
  * 重构了驱动热重载：新增 `FindPnpInstanceIdByClassSubKey` 反查真实物理 PnP 实例 ID，调用系统原生 `pnputil.exe /restart-device` 进行真正的硬件热重载，无需重启电脑即可让设备管理器、任务管理器与 DirectX 实时变身为 RTX 3080。
* **细节体验打磨**：修复了暗色审计日志文本框鼠标滑过变白的样式穿透 Bug；加入了未提权时的 UAC 自动引导机制。

---

## 2. 核心架构与关键代码模块说明 (Architecture & Codebase)

### 2.1 整体目录拓扑
```text
D:/app/
├── 90HX Studio.exe                    # [编译产物] 最终单文件免安装可执行程序 (约 56.5MB)
├── app.ico                            # 256x256 高清多尺寸应用图标 (黑灰渐变圆角「90」)
├── core.pkg                           # 加密打包的固件与工具包容器 (55.75MB)
├── build_source/                      # 核心源码与编译工作区
│   ├── app.manifest                   # UAC requireAdministrator 权限清单
│   ├── build.ps1                      # 主程序全自动编译脚本 (调用系统 64位 csc.exe)
│   ├── pack.ps1                       # 将 toolkit 资源打包为 core.pkg 的构建脚本
│   ├── render.ps1                     # 全Tab无头渲染测试脚本 (生成 render_tab*.png)
│   ├── render_splash.ps1              # 启动动画窗口无头渲染测试脚本
│   ├── Packager.cs                    # 资源打包器源码
│   ├── RenderAllTabsV2.cs             # 全页面无头测试渲染器
│   ├── RenderSplash.cs                # 启动动画无头测试渲染器
│   ├── assets/                        # 内置静态图片资产 (wechat_pay.png, alipay.png)
│   ├── mining_unlock_toolkit/         # 各型号真实固件与批处理库
│   │   ├── 30hx_gen2/                 # 30HX Gen2 MMIO 脚本与 ReBar
│   │   ├── 40hx_release/              # 40HX 官方发布包 (40HXUNLK.EFI + 官方安装器)
│   │   └── 90hx_firmware/             # 90HX WildFlash1st 官方 EFI (compute / full)
│   └── src/                           # 主程序 C# 源码
│       ├── AssemblyInfo.cs            # 程序集信息与版本元数据 (2.7.0.0, RainCandy Tech)
│       ├── SplashWindow.xaml          # 启动动画窗口 XAML (苹果风 3D 浮雕黑色图标)
│       ├── MainWindow.xaml            # 主窗体 XAML (9大选项卡与 Clean Studio 控件模板)
│       ├── SystemDetector.cs          # 系统与全机硬件资产探测器 (图吧工具箱风格)
│       ├── NativeCore.cs              # 核心硬件控制、注册表注入、PnP热重载与固件部署
│       └── App.cs                     # 应用程序入口、事件交互绑定、定时器与弹窗逻辑
└── .workbuddy/memory/                 # 每日工作日志与持久化记忆 (YYYY-MM-DD.md)
```

### 2.2 核心代码模块职责与关键接口

#### 模块 1：`NativeCore.cs`（底层硬件引擎）
* **命名空间**：`CMP90HXToolbox`
* **核心职责**：与 Windows 内核、注册表、INF 驱动文件、PnP 设备管理器、BCD 引导层及 UEFI 文件系统打交道。
* **关键数据结构**：
  * `InstalledMiningGpu`：代表识别到的矿卡实例（含 `SlotIndex`、`ClassSubKey`、`DevInstanceId`、`DetectedModel`、`TargetGeForceName`、`IsSpoofed`、`IsComputeUnlocked`、`IsPcieUnlocked` 等）。
  * `SupportedCmpModels`：内置全系 CMP 规则库（30HX/40HX/50HX/70HX/90HX/170HX 对应的 Device ID、芯片架构、伪装目标 GeForce 型号）。
* **核心方法**：
  * `DetectAllMiningGpus()`：遍历 `HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-...}` 下所有 4 位数字子项，提取 `MatchingDeviceId` 与 `DriverDesc`，精准识别插槽与型号。
  * `ApplySingleGpuGraphicsUnlockAsync(gpu, log)`：
    1. 修改 Class 注册表（`DriverDesc`、`HardwareInformation.*`、`AdapterType=1`、`EnableMsHybrid=1`、`RMDisableTcc=1`）；
    2. 同步 `Control\Video`、`Services\nvlddmkm\Video`、`DirectX` 描述；
    3. 调用 `PatchInfFileForGpu()` 取得系统 `C:\Windows\INF\oemXX.inf` 及 `DriverStore` 文件权限并替换字符；
    4. 同步更新各用户目录下的 NVIDIA App `SHIM.json`；
    5. 调用 `DeployNative40hxUnlock()` 或 `DeployNative30hxUnlock()` 原生部署固件。
  * `FindPnpInstanceIdByClassSubKey(classSubKey)`：通过注册表 Driver 关联或 `pnputil /enum-devices` 反查显卡的真实物理 PnP 实例 ID。
  * `SoftRestartGpuDriverAsync(log)`：调用 `pnputil.exe /restart-device "<InstanceId>"` 对显卡进行真实硬件热重载，并执行 `pnputil /scan-devices` 与 `dwm.exe` 刷新。
  * `ApplyHybridMultiGpuUnlockAsync(gpus, unlockGraphics, unlockCompute, log)`：全机多卡混合批量解锁主入口。
  * `EnsureToolkitExtracted(key)`：在运行时如果本地工具不存在，从内置加密的 `core.pkg` 解密解压到 `%ProgramData%\CMP90HX_Toolbox\toolkit\<key>\`。

#### 模块 2：`SystemDetector.cs`（硬件看板探测）
* **核心职责**：毫秒级直读硬件信息，支撑“💻 硬件信息”Tab 的图吧工具箱风格看板。
* **检测项**：
  * CPU 型号、核心数、基准主频（读取 `HARDWARE\DESCRIPTION\System\CentralProcessor\0`）；
  * 主板厂商、型号、BIOS 版本（读取 `HARDWARE\DESCRIPTION\System\BIOS`）；
  * 物理内存容量与双通道（调用 Win32 API `GlobalMemoryStatusEx`）；
  * 系统版本、开机运行时间（`Environment.TickCount`）；
  * 全机所有显卡资产总览（包括插槽独显、矿卡、核显、输出卡角色自动判定）。

#### 模块 3：`App.cs`（交互控制器与主流程）
* **核心职责**：单实例 Mutex 互斥防双开、启动动画调度、全页面控件绑定、传感器定时轮询、弹窗封装。
* **重要实现**：
  * `LoadSplashWindow()`：从嵌入程序集资源动态反射加载 `SplashWindow.xaml`。
  * `ModernMessageBox`：自研纯原生轻奢弹窗静态类，彻底替代 Windows 98 风格原生系统弹窗，支持信息（蓝）、警告（橙）、错误（红）、确认（蓝），带柔光阴影与 Pill 胶囊按钮。
  * `EnsureAdminPrivilege()`：核心操作前的权限防呆守卫，未提权时弹出精致弹窗并引导使用 `Verb="runas"` 提权重启。
  * `LoadEmbeddedSponsorImages()`：使用内存流与 `bi.Freeze()` 从自身二进制流中高保真解压呈现微信/支付宝收款码。

#### 模块 4：`MainWindow.xaml`（UI 布局与模板）
* **包含 9 个核心 TabItem**：
  1. `💻 硬件信息`：图吧工具箱 1:1 像素级复刻看板与斑马纹详情面板；
  2. `🎭 显卡型号伪装`：多卡下拉切换器、单卡伪装、一键全机伪装、出厂原名恢复；
  3. `🎮 NVIDIA 覆盖`：NVIDIA App 路径检测、白名单免检补丁一键注入/还原；
  4. `⚡ 全系解锁矩阵`：30/40/50/90HX 通用硬件解锁、图形+算力双轨大卡片（二选一互斥高亮）、四种安装通道选择（A/B/C/D）、U盘盘符展开选择器、主执行控制台；
  5. `🖥️ 双卡调度`：MS-Hybrid 拓扑状态、高性能卡/输出卡识别、全局 DirectX/OpenGL 锁定；
  6. `🚀 DLSS 3 帧生成`：游戏目录浏览、DLSSG 运行库注入与卸载、在线镜像更新；
  7. `🛠️ 硬件与电源`：传感器仪表板（温度/功耗/风扇/显存/核心频率）、8 大硬件实用工具网格；
  8. `📝 审计日志`：深黑极客终端文本框（绑定 `TerminalLogBoxStyle`，彻底防悬停变白）；
  9. `💖 赞助与支持`：微信与支付宝大尺寸赞赏码（大留白、防反光、秒扫）。

---

## 3. 待完成任务清单 (Task Backlog & Prioritization)

后续开发应严格按照以下优先级推进，每个任务均明确了具体输入、处理逻辑与验收标准。

| 任务编号 | 优先级 | 任务名称 | 核心关注点 |
| :--- | :--- | :--- | :--- |
| **TASK-01** | **P0 (阻塞级)** | 多卡混合环境下的并发热重载与稳定性验证 | 防止多卡同时重载导致显卡掉驱动（Code 43） |
| **TASK-02** | **P0 (阻塞级)** | 通道 B (老主板 Shell) 引导部署稳健性增强 | X79/X99 华南/精粤主板自动挂载 ESP 并注入 startup.nsh |
| **TASK-03** | **P1 (核心功能)** | 新版 NVIDIA App 覆盖补丁防失效机制 | 动态适配 NVIDIA App 新版前端打包目录与白名单校验规则 |
| **TASK-04** | **P1 (核心功能)** | DLSS 3 运行库多源镜像测速与故障自动转移 | 4 条镜像线路并发探测并自动择优下载 |
| **TASK-05** | **P2 (体验优化)** | 硬件功耗墙与风扇自定义曲线（NVAPI 联动） | 硬件与电源页面支持自定义 TGP 功耗上限与锁定转速 |

---

### 详细任务规格说明 (Task Specifications)

#### TASK-01：多卡混合环境下的并发热重载与稳定性验证 (P0)
* **需求背景**：当用户机器上同时插有两张或以上矿卡（例如 90HX 渲染卡 + 40HX 算力卡）时，执行“一键全机伪装”可能会对多个硬件节点连续发起 PnP 重启，若时序不当可能导致第二个设备重载超时。
* **输入**：`NativeCore.DetectAllMiningGpus()` 返回的多张有效矿卡列表。
* **处理要求**：
  1. 在 `ApplyHybridMultiGpuUnlockAsync` 中，遍历多卡完成注册表与 INF 替换后，对 `SoftRestartGpuDriverAsync` 引入**串行等待与状态就绪轮询**；
  2. 针对每个设备的 `pnputil /restart-device`，增加 1000ms 硬件复位缓冲；
  3. 执行完毕后调用 `pnputil /enum-devices /problem`，确保没有设备处于 Code 43（设备无法启动）或 Code 14（需要重启）异常状态。
* **验收标准**：
  * 在多卡混合机器上点击【⚡ 一键全机多卡全部伪装】；
  * 系统屏幕正常闪烁重载 1~2 次；
  * 设备管理器中所有对应插槽的显卡全部显示为对应的 GeForce 型号，无黄色感叹号。

#### TASK-02：通道 B (老主板 Shell) 引导部署稳健性增强 (P0)
* **需求背景**：部分矿渣平台（如 E5 志强配华南金牌 X79/X99 主板）由于缺少原生 UEFI NVRAM 变量写入支持，必须依赖 EFI Shell 链式加载。
* **输入**：用户选择“通道 B · 老主板 Shell”并点击执行解锁。
* **处理要求**：
  1. 在 `NativeCore.InstallPcieMode2Async` 中，检查系统是否成功分配盘符给 ESP 分区（如 `S:`）；
  2. 自动部署 `startup.nsh`，内容根据 `GenerateSmartStartupNsh` 动态生成；
  3. 配置 Windows BCD 添加一个独立的恢复启动项，设置超时为 3 秒，防止主板不识别默认项；
  4. 在操作完成后自动执行 `bcdedit /enum` 校验，输出日志提示。
* **验收标准**：
  * 点击执行通道 B 解锁后，日志明确提示 ESP 挂载、脚本写入与 BCD 项创建成功；
  * 系统重启后主板固件能顺利读取 `startup.nsh` 并自动进入 Windows。

#### TASK-03：新版 NVIDIA App 覆盖补丁防失效机制 (P1)
* **需求背景**：NVIDIA 官方正在频繁迭代 NVIDIA App，未来可能会更换 `www` 目录结构或强化白名单特征码。
* **输入**：`C:\Program Files\NVIDIA Corporation\NVIDIA app` 目录及其子文件。
* **处理要求**：
  1. 优化 `NativeCore.UnlockNvAppOverlayAsync`：不仅搜索静态的 `.js`，增加正则表达式动态匹配 `isSupportedGpu`、`allowMiningCards` 或 `gpuWhitelist` 类似逻辑；
  2. 增加对 `AppData\Local\NVIDIA Corporation\NVIDIA app\www` 用户级缓存目录的联合清理；
  3. 写入补丁前强制备份原文件为 `.bak`，支持随时无损一键还原。
* **验收标准**：
  * 在安装了最新 NVIDIA App 的机器上点击【解锁覆盖功能】；
  * 启动游戏后按 `Alt+R` 能正常弹出性能悬浮窗，按 `Alt+Z` 能正常呼出 ShadowPlay 录屏菜单。

#### TASK-04：DLSS 3 运行库多源镜像测速与故障自动转移 (P1)
* **需求背景**：国内网络访问 GitHub 经常发生连接重置，当前预置了 4 条镜像源（Ghproxy、jsDelivr、FastGit、GitHub 直连），需要具备全自动容灾。
* **输入**：用户在 DLSS 页面点击“检查并更新运行库”。
* **处理要求**：
  1. 在 `NativeCore.cs` 中实现并发轻量探测（HTTP HEAD 请求至各镜像源的 `version.dll` 或 `latest` 节点，超时 2.5 秒）；
  2. 按照响应延迟最低原则自动挑选最优线路；
  3. 若首选线路下载中断（如网络抖动），自动顺延尝试备用线路并记录日志。
* **验收标准**：
  * 在断开外网直连的环境下，依然能通过任一可用国内镜像源顺畅下载更新 `version.dll` 运行库。

---

## 4. 开发与测试环境配置指南 (Environment & Build Guide)

本项目经过严格的工程化设计，**完全免除了庞大的 Visual Studio 安装与任何外部 NuGet 包依赖**，直接利用 Windows 系统原生自带的编译工具链即可一键构建。

### 4.1 环境准备
* **操作系统**：Windows 10 / Windows 11 64 位（任意已安装 .NET Framework 4.0/4.5+ 的系统，Win10/11 默认自带）；
* **编译器路径**：`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`；
* **GAC 依赖程序集路径**：
  * `C:\Windows\Microsoft.NET\assembly\GAC_MSIL\`（包含 `WindowsBase.dll`、`PresentationFramework.dll`、`System.Xaml.dll`）；
  * `C:\Windows\Microsoft.NET\assembly\GAC_64\`（包含 `PresentationCore.dll`）。

### 4.2 常用构建与验证命令

#### 1. 一键全量编译免安装单文件应用（最常用）
在终端（PowerShell 或 Bash）中执行：
```powershell
powershell -ExecutionPolicy Bypass -File "D:\app\build_source\build.ps1"
```
* **输出结果**：直接在 `D:\app\` 根目录下生成 `90HX Studio.exe`（单文件，约 56.5MB）；
* **构建耗时**：约 3~5 秒；
* **检查验证**：
```powershell
Get-Item "D:\app\90HX Studio.exe" | Format-Table Name, Length, LastWriteTime -AutoSize
```

#### 2. 将编译好的 EXE 同步部署至当前桌面
```powershell
powershell -ExecutionPolicy Bypass -Command "$desktop = [System.IO.Path]::Combine([System.Environment]::GetFolderPath('Desktop'), '90HX Studio.exe'); Copy-Item -Path 'D:\app\90HX Studio.exe' -Destination $desktop -Force; Write-Host 'Deployed to Desktop successfully!'"
```

#### 3. 全页面无头测试渲染（用于在无显示器/AI环境下检查 UI 是否有截断与错位）
```powershell
powershell -ExecutionPolicy Bypass -File "D:\app\build_source\render.ps1"
```
* **输出结果**：在 `D:\app\build_source\` 下输出 9 个选项卡的真实 1:1 像素截图：
  * `render_tab0_sensors.png`（硬件信息）
  * `render_tab1_gpu_rename.png`（显卡型号伪装）
  * `render_tab2_overlay.png`（NVIDIA 覆盖）
  * `render_tab3_pcie.png`（全系解锁矩阵）
  * `render_tab4_dualgpu.png`（双卡调度）
  * `render_tab5_dlss.png`（DLSS 3 帧生成）
  * `render_tab6_tools.png`（硬件与电源）
  * `render_tab7_logs.png`（审计日志）
  * `render_tab8_sponsor.png`（赞助与支持）

#### 4. 单独渲染验证苹果风启动动画（Splash Screen）
```powershell
powershell -ExecutionPolicy Bypass -File "D:\app\build_source\render_splash.ps1"
```
* **输出结果**：生成 `D:\app\build_source\render_splash_preview.png`。

#### 5. 重新打包底层固件与工具包容器 (`core.pkg`)
> **注意**：仅当修改了 `mining_unlock_toolkit/` 下的原生固件或脚本时才需要执行。
```powershell
powershell -ExecutionPolicy Bypass -File "D:\app\build_source\pack.ps1"
```

---

## 5. 代码规范与已知限制 (Conventions & Constraints)

接手开发的 AI 助手或开发者**必须严格遵守以下军规**，否则会导致编译崩溃或破坏用户环境：

### 5.1 C# 5.0 语法硬约束（绝对红线）
由于构建脚本调用的是系统自带的 `.NET Framework 4.0/4.5` 编译器（`csc.exe v4.8` 运行在 C# 5.0 兼容模式下），**严禁使用任何 C# 6.0 及以上的新语法**：

| 严禁语法 (C# 6.0+) | 替代合规语法 (C# 5.0) |
| :--- | :--- |
| **字符串插值**：`$"Count: {count}"` | **严格使用**：`string.Format("Count: {0}", count)` 或 `+` 拼接 |
| **空条件运算符**：`obj?.Method()` | **严格使用**：`if (obj != null) obj.Method();` |
| **nameof 关键字**：`nameof(MyProperty)` | **严格使用**：硬编码字串 `"MyProperty"` |
| **表达式体属性/方法**：`int Age => 18;` | **严格使用**：传统属性 `get { return 18; }` |
| **模式匹配**：`if (item is Button btn)` | **严格使用**：`var btn = item as Button; if (btn != null)` |
| **只读自动属性**：`public int Id { get; } = 1;` | **严格使用**：`public int Id { get; set; }` 或带私有字段 |
| **局部函数**：在方法内定义 `void Helper()` | **严格使用**：类的私有静态方法或 lambda 委托 |

### 5.2 XAML 与 UI 规范
1. **纯原生零依赖**：不要引入任何三方 UI 库（如 MaterialDesign 或外部 MahApps），所有现代视觉效果（圆角、阴影、微弥散、毛玻璃、Switch 开关）必须通过纯原生 WPF 控件模板 + XAML 样式实现。
2. **样式隔离与防变白机制**：
   * 输入框全局定义了 `<Style TargetType="TextBox">`；
   * 对于深色容器内的输入框（如审计日志 `TxtLogBox`），**必须显式绑定 `<Style x:Key="TerminalLogBoxStyle">`**，设置 `OverridesDefaultStyle="True"` 并彻底剥离 `IsMouseOver` 的浅色触发器，防止鼠标滑过变白。
3. **空引用防御**：XAML 中的控件 Name 与 `App.cs` 中的 `FindName` 字段必须严格对应；在事件绑定或操作前必须使用 `if (_ctrl != null)` 进行防御性保护。
4. **禁止重新引入问号小徽章**：用户已明确要求界面极致清爽，不要在卡片标题旁添加多余的 `?` 帮助小徽章或冗长的原理 ToolTip。

### 5.3 硬件操作与安全红线
1. **严禁破坏性清盘操作**：
   * 严禁无备份直接删除 ESP 分区或 BCD 项；在操作引导前必须通过 `NativeCore` 保留出厂官方还原点。
2. **权限要求与提权引导**：
   * 涉及 `HKLM` 注册表写入、修改 `C:\Windows\INF` 文件所有权（`takeown`/`icacls`）、硬件 PnP 设备热重载（`pnputil`）等操作必须要求管理员权限；
   * 在执行关键写操作前调用 `EnsureAdminPrivilege()`，未提权时温和弹窗引导用户以管理员身份重启，不得静默失败。
3. **PnP 热重载规范**：
   * 修改显卡名称后，必须调用 `SoftRestartGpuDriverAsync()` 执行真正的硬件热重载，严禁仅依靠杀死 `dwm.exe` 做表面假象。

### 5.4 日志与记忆持久化要求
* 完成任何实质性开发任务后（修改核心逻辑、修复 Bug、新增功能），必须在 `D:/app/.workbuddy/memory/YYYY-MM-DD.md` 中以条目形式追加精简的技术笔记，确保多轮会话之间具备完美的上下文连续性。

---

## 6. 快速交接清单与验证 Checklist

当新接手的 AI 助手开展工作时，请按顺序完成以下快速自检：
- [ ] 检查并确认系统拥有 `csc.exe`（执行 `Get-Command csc.exe` 或检查路径）；
- [ ] 执行 `powershell -ExecutionPolicy Bypass -File "D:\app\build_source\build.ps1"` 验证当前工程是否 0 错误编译通过；
- [ ] 执行 `powershell -ExecutionPolicy Bypass -File "D:\app\build_source\render.ps1"` 检查生成的 9 个页面渲染图是否排版完美；
- [ ] 确认理解 C# 5.0 语法约束，开始认领第 3 节中的 `TASK-01` ~ `TASK-05` 任务。
