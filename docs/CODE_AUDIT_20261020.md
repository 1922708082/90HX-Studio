# HX家族多功能工具箱 / 90HX Studio — 代码审计与重构评估

> 审计对象：`D:\app`
> 审计日期：2026-10-20
> 审计方式：静态审计（结构、依赖、缺陷模式、安全基线、版本控制），未执行构建（避免覆盖现有 exe），未在真实硬件上运行解锁流程
> 结论状态：**值得重构，但不建议推倒重写** —— 建议绞杀者（Strangler Fig）分层重构

---

## 一、关键文件与整体结构

### 1.1 实际参与构建的文件（16 个，21,610 行）

| 文件 | 行数 | 职责 | 健康度 |
| :--- | ---: | :--- | :--- |
| `build_source/src/NativeCore.cs` | 8,193 | 上帝类：注册表 / BCD / nvidia-smi / 省电 / 伪装 / 更新 / COM 任务 / DLSS / 下载 | 严重超标 |
| `build_source/src/App.cs` | 4,112 | 启动、XAML 加载、控件绑定、事件绑定、页面切换、传感器刷新、弹窗 | 严重超标 |
| `build_source/src/MainWindow.xaml` | 2,664 | 12 个功能页全部塞在一个文件里 | 超标 |
| `build_source/src/AiAssistantEngine.cs` | 1,413 | AI 网关、手写 JSON 序列化、离线 FAQ 库 | 偏高 |
| `build_source/src/AiAssistantWindow.cs` | 1,243 | AI 悬浮窗逻辑 | 偏高 |
| `build_source/src/VirusOverlay.cs` | 1,097 | 启动动画窗口跟踪（窗口钩子 + 区域绘制） | 偏高 |
| `build_source/src/AiAssistantWindow.xaml` | 740 | AI 窗口布局 | 可接受 |
| `build_source/src/SystemDetector.cs` | 565 | WMI 硬件探测 | 偏高 |
| `build_source/src/AiAgentActionRegistry.cs` | 474 | AI 动作关键词匹配 + 执行 | 中等 |
| `build_source/src/SplashWindow.xaml` | 276 | 启动画面 | 正常 |
| `build_source/src/ScriptEngine.cs` | 146 | PowerShell 调用封装 | **完全死代码（0 引用）** |
| `build_source/src/EcosystemPatchManager.cs` | 142 | NVIDIA App 覆盖补丁 | **门面（25/27 纯转发）** |
| `build_source/src/PcieUnlockManager.cs` | 132 | PCIe / 引导解锁 | **门面（24/25 纯转发）** |
| `build_source/src/GpuHardwareManager.cs` | 123 | 显卡枚举 / 驱动重启 | **门面（22/23 纯转发）** |
| `build_source/src/EcoPowerManager.cs` | 122 | 省电 | **门面（17/23 纯转发）** |
| `build_source/src/GpuSpoofManager.cs` | 42 | 型号伪装 | **门面（6/7 纯转发）** |
| `build_source/src/AssemblyInfo.cs` | 15 | 程序集信息 | 正常 |

### 1.2 不参与构建的遗留代码（14 个文件，2,109 行）

`build.ps1` 只编译 `src/*.cs`，根目录下的 `UniversalInstaller.cs`(390)、`src_installer.cs`(306)、`UniversalInstallerWpf.cs`(275)、`Uninstaller.cs`(168)、`Packager.cs`(162)、`RenderAllTabs*.cs` 等 14 个文件**全部游离在构建之外**，是历史版本的残留。

### 1.3 构建与产物

| 项 | 现状 |
| :--- | :--- |
| 构建方式 | `build_source/build.ps1` 手写 13 个文件路径调用 `csc.exe`（C# 5.0 / .NET Framework 4.x） |
| 工程文件 | **无 `.sln`、无 `.csproj`**，无 NuGet，无 IDE 智能提示与重构支持 |
| 构建参数 | `/warn:4 /warnaserror+`（任何警告即失败） |
| 路径耦合 | 全量**绝对路径** `D:\app\...`，仓库换位置即无法构建 |
| 产物 | `90HX Studio.exe` 48.6MB，`HX家族多功能工具箱.exe`（MD5 完全相同，重复占用 97MB） |
| 资源内嵌 | `assets.zip` 47MB 压缩 / **100MB 解压，55 个二进制条目**（50HXInstaller.exe、dinput8.dll、fg_kernels.dll、bootmgfw.efi 等） |
| 签名 | **未签名（NotSigned）**，但清单要求 `requireAdministrator` |
| 可复现性 | `assets.zip` 与 `mining_unlock_toolkit/` 均在 `.gitignore` 中 —— **新机器 clone 后无法构建** |

### 1.4 目标分层（从代码实际调用关系还原）

```
MainWindow.xaml (运行时 XamlReader.Load)
        │  ← 238 处 string FindName，控件靠 Name 字符串对齐
        ▼
App.cs (4,112 行, 250 个私有字段)
        │  直接持有全部 UI 引用 + 全部业务编排
        ├──► 5 个 Manager（105 个方法 / 94 个纯转发）──► NativeCore
        └──────────────────────────────────────────► NativeCore (8,193 行 / 132 个 public static)
                                                            │
                                                            ├── Registry (HKLM Class / Video / Enum)
                                                            ├── nvidia-smi 子进程
                                                            ├── WMI (4 个查询)
                                                            ├── BCD / bcdedit
                                                            ├── Task Scheduler COM (反射晚期绑定)
                                                            └── PowerShell 脚本生成 + 执行
```

**关键事实：`Manager` 层是假的。** 105 个公开方法里 94 个只是 `return NativeCore.XXX(...)`。代码"看起来"分了层，实际所有逻辑仍在 `NativeCore` 一个静态类里。

### 1.5 主要功能（12 个页面 / 132 个公开能力）

| 页面 | 功能 |
| :--- | :--- |
| 硬件信息 | WMI 探测 + 实时传感器（温度/功耗/显存/利用率/P-State/PCIe 代数与宽度） |
| 显卡伪装 | 76 款 GeForce 型号注册表注入、出厂名还原 |
| 游戏覆盖 | NVIDIA App 白名单补丁 + CefCache 双缓存清理 |
| 固件解锁 ×3 | 90HX / 50HX / 40HX 三套独立解锁入口（平铺 RadioButton） |
| 双卡协同 | MS-Hybrid 调度、进程绑定、常见游戏自动扫描 |
| DLSS | 运行库镜像测速、DLSS-G 注入、ReFramework 集成 |
| 系统省电 | P8 压频守护进程、计划任务注册、功耗墙自适应 |
| 日志 / 赞助 / 更新 | 审计日志、二维码、自更新 |
| AI 助手 | 悬浮窗、云端网关、离线 FAQ、6 类可执行动作 |

---

## 二、技术栈判定

| 层 | 实际技术 | 评价 |
| :--- | :--- | :--- |
| 语言 | C# 5.0（`csc.exe` v4.0.30319） | **被编译器锁死**：无字符串插值、无 `nameof`、无 `async/await` 之外的现代语法、无 `?.`、无值元组、无 `Span` |
| UI | WPF + 手写 XAML，运行时 `XamlReader.Load` | 放弃编译期检查（无 BAML、无编译校验、无设计器） |
| 架构 | 无（God Class + 字符串绑定 + 静态全局状态） | — |
| 数据 | 注册表 + 无数据库 | 合理（工具类） |
| 外部交互 | 子进程（nvidia-smi / reg / schtasks / powershell）、WMI、COM 反射 | 大量字符串拼接 |
| 网络 | `HttpWebRequest`（手写）+ 手写 JSON 序列化 | 重复造轮子 |
| 打包 | 单文件 exe + 内嵌 zip 资源 | 体积 48.6MB |

**技术栈本身没错**（原生 WPF 单文件、免安装、零依赖，对这类"发绿软给矿卡用户"的场景是正确选择）。错的是**组织方式**。

---

## 三、检查结论：关键缺陷

按"是否直接导致你现在遇到的问题"排序。

### P0-1 全局关闭 TLS 证书校验 —— 已构成远程代码执行链

`App.cs:439`：

```csharp
ServicePointManager.SecurityProtocol = ...;
ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
```

这行把**整个进程**的 HTTPS 证书校验关掉了。叠加下面三条，形成完整攻击链：

1. 自更新只校验文件头 `data[0] != 0x4D || data[1] != 0x5A`（MZ），**无哈希、无签名校验**（`NativeCore.cs:5185`）
2. 下载源包含第三方加速镜像 `https://ghfast.top/`（`NativeCore.cs:5168`）
3. 程序清单 `requireAdministrator`，更新后直接 `Start-Process $exe` **以管理员身份运行**

后果：任意中间人 / 被入侵的镜像 / 被接管的 GitHub Release，都能让任意代码以管理员权限在你机器上执行。这是当前最该优先处理的一条。

### P0-2 UI 线程上同步调用 nvidia-smi 与 WMI —— 卡顿、假死的直接原因

证据链：

| 位置 | 问题 |
| :--- | :--- |
| `App.cs:2685` | `NativeCore.QueryGpuTempThresholds()` 被写在 `Dispatcher.Invoke` **内部**，而该方法是 `ReadToEnd()` + `WaitForExit()` 的同步子进程调用 |
| `App.cs:2644-2655` | 上面这段每 **1.5 秒**执行一次（`DispatcherTimer`） |
| `NativeCore.cs:5698-5738` | `QueryGpuTempThresholds` 每次都新起一个 `nvidia-smi -q -d TEMPERATURE`，无缓存 |
| `App.cs:2863` | `RefreshSystemStatus()` 是同步方法，内部 `SystemDetector.DetectAll()`（4 个 WMI 查询 + 显卡探测 + 注册表遍历），被 14+ 个点击回调在 UI 线程直接调用 |
| `App.cs:621/623` | 启动时 `RefreshSystemStatus()` 与 `UpdateStatusBar()`（内部再调一次 `DetectAll`）**各跑一遍**，重复采集 |

`nvidia-smi` 在矿卡/驱动异常时单次可阻塞数百毫秒到数秒。1.5 秒一次 + UI 线程同步等待，界面必然会周期性顿住。这解释了"点一下没反应""界面卡"这类无法稳定复现的问题。

### P0-3 控件靠字符串 `FindName` 绑定 —— 功能静默失效

`App.cs` 中有 **238 处** `_window.FindName("Xxx")`，全部是字符串 + 强制转换 + 手写判空，全部字段登记在 **250 个私有字段**里。由此产生三类必然故障：

1. XAML 改名 → `FindName` 返回 `null` → 代码里 `if (x != null)` 静默跳过 → **功能没了但不报错**
2. `_navItems[12]` 与 `_pageHosts[12]` **按下标强对应**，任何一边增删就串页（HANDOVER 第 123 行已明确记录这个坑）
3. XAML 中同名 `Name` 会让 `XamlReader.Load` 整体失败（HANDOVER 第 177 行记录过）

代码里已经留下了这个缺陷的"化石"，`App.cs:2870-2872`：

```csharp
// 把 SystemDetector 采集的硬件信息填进「硬件概览」页
// （此前 _txtHw* 这一批 TextBlock 只 FindName 没赋值，界面一直显示 XAML 里的示例数据）
```

这就是典型的"界面显示假数据、代码不报错、排查靠猜"。

### P0-4 异常被系统性吞掉 —— 问题不可观测

| 指标 | 数量 |
| :--- | ---: |
| `catch` 总数 | 397 |
| **空 catch / 仅注释的 catch** | **224（56%）** |
| `DispatcherUnhandledException` 中 `e.Handled = true` | 1（`App.cs:505`）—— 所有 UI 异常被标记为已处理，程序继续运行在未知状态 |

`e.Handled = true` 意味着：任何未预料的 UI 异常都不会崩、不会弹窗、不会提示，只是**某个功能从此不工作**。加上 56% 的空 catch，导致"用着用着就不对了"这类问题无法定位。

### P0-5 `.Result` / `.Wait()` 嵌套调用 —— 线程池饥饿风险

`AiAgentActionRegistry.cs` 中 7 处、`NativeCore.cs` 中 6 处：

```csharp
return Task.Factory.StartNew(() =>
{
    bool ok = GpuSpoofManager.ApplyGpuRenameAsync(finalTarget, log).Result;
    if (ok) GpuHardwareManager.SoftRestartGpuDriverAsync(log).Wait();
    ...
});
```

在 `StartNew` 里阻塞等待另一个 Task，等于占用一个线程池线程去等另一个线程池线程，最多只能占到 5 处并发就会开始排队。叠加 P0-2 的同步子进程调用（每个都长时间占线程），这是"批量操作时整个程序像死了一样"的结构性原因。

### P0-6 用"备份副本"替代版本控制 —— 源码与二进制必然漂移

| 指标 | 现状 |
| :--- | :--- |
| Git 提交数 | **15 次**（对应 v4.1 → v5.0 十几轮迭代） |
| 未提交的核心文件 | `App.cs`、`NativeCore.cs`、`MainWindow.xaml`、`AiAssistantEngine.cs`、`AiAssistantWindow.*`、`SystemDetector.cs`、`build.ps1` —— **8 个，全是核心** |
| `src/` 下 `.bak*` 文件 | **21 个**（`App.cs.bak_agent1/2/3`、`MainWindow.xaml.bak_before_studio_merge` 等） |
| 被 Git 跟踪的备份目录 | **28 个文件**（`_backup_refactor_20261014/`、`_backup_before_slim/`） |
| 项目体积 | **1.8 GB**（`build_source` 693MB、`.git` 75MB、9 个备份 exe、6 份 `assets.zip`） |

HANDOVER 里那句"用户认可的布局基线是 `MainWindow.xaml.bak_before_studio_merge`"就是最直接的证据：**唯一可信的版本信息存在文件名里，而不是版本库里**。一旦某个 `.bak` 被覆盖，就没有任何回退手段。

### P1-1 上帝类与假分层

- `NativeCore`：8,193 行 / **132 个 public static** / 一个静态类里塞了注册表、BCD、子进程、COM、HTTP、下载、解压、正则、UI 弹窗判断
- `App`：4,112 行 / 250 字段 / `BindControls` 539 行 / `InitEvents` **1,190 行**（一个方法里塞了 12 个页面的全部事件与部分业务逻辑）
- 5 个 Manager：**90% 是纯转发**，不提供任何抽象、不含任何逻辑

静态类 + 全局状态 = 无法单测、无法替换、无法并行（多卡场景下互相覆盖全局变量）。

### P1-2 硬编码路径与个人环境泄漏

`src/` 内 **13 处** `D:\app\...` 硬编码，全部作为"资源找不到时的回退路径"：`NativeCore.cs:99/160/3606/3977/7868`、`App.cs:555/589/1894`。另外 `build.ps1` 有 4 处 `C:\Users\19227\Desktop`。

后果：这行代码在你自己机器上是"兜底成功"，在任何其他机器上是"兜底失败 + 无提示"。它掩盖了资源打包的真实问题。

### P1-3 构建不可复现

`assets.zip`（47MB，含 100MB 二进制）和 `mining_unlock_toolkit/` 都在 `.gitignore` 里，`build.ps1` 用绝对路径引用它们。**仓库 + 新电脑 ≠ 能构建出同一个 exe**。

### P1-4 100MB 不透明二进制内嵌，无来源与完整性记录

`assets.zip` 55 个条目，包含：

| 条目 | 大小 | 性质 |
| :--- | ---: | :--- |
| `re_framework/dinput8.dll` | 22.0 MB | 第三方 DLL |
| `dlss/dlssg_sm86/kernels/fg_kernels.dll` | 17.5 MB | NVIDIA 运行库 |
| `50hx/50HXInstaller.exe` | 7.7 MB | 第三方安装器 |
| `usb90/bootmgfw.efi` | 2.9 MB | 引导程序 |
| `50hx/50HXUNLK.EFI` | 1.5 MB | 引导程序 |

没有哈希清单、没有版本号、没有来源 URL、没有许可证记录。一旦某个上游工具更新或投毒，无法审计、无法定位、无法回滚。

### P1-5 视觉层无设计令牌

`MainWindow.xaml` 中 **215 处硬编码色值，89 种不同颜色**；`App.cs` 中还有 21 处。同一语义（如边框灰）出现 `#E5E5EA / #D8D8DC / #EDEDF0 / #C7C7CC` 四种。改主题 = 全文件搜索替换。

### P1-6 死代码与残留

| 项 | 说明 |
| :--- | :--- |
| `ScriptEngine.cs` | 146 行，**0 引用** |
| 根目录 14 个 `.cs` | 2,109 行，**不进构建**（旧安装器/打包器/渲染器） |
| `build_source/` 根目录 | 60+ 张渲染测试 PNG、20+ 个 `.ps1` 一次性脚本混在源码目录 |
| 内存中 48MB 资源 | 启动时若走 `/resource:assets.zip` 解压路径，会有额外 IO 与内存压力 |

---

## 四、代码质量评分

| 维度 | 评分 | 依据 |
| :--- | :---: | :--- |
| 功能完整度 | 8 / 10 | 132 个能力、六款卡型覆盖，功能密度确实高 |
| 领域知识含量 | 9 / 10 | 注册表键位、PCI ID、NVAPI 复位链、BCD 通道，这些是真实踩坑资产 |
| 代码组织 | 2 / 10 | 8,193 行上帝类 + 假分层 + 250 字段窗口 |
| 可维护性 | 2 / 10 | 21 个 .bak、8 个核心文件未提交、字符串绑定、无 csproj |
| 可测试性 | 0 / 10 | **零测试**；静态类 + UI 线程 + 硬件依赖，无法插桩 |
| 健壮性 | 3 / 10 | 56% 空 catch、`e.Handled = true`、`.Result` 阻塞 |
| 安全性 | 2 / 10 | TLS 关闭、更新无签名、未签名 exe + 管理员、客户端密钥硬编码 |
| 可观测性 | 3 / 10 | 有 crash.log/startup.log/ai_assistant.log，但异常被吞，日志只记"崩了"不记"为什么" |
| 可复现构建 | 2 / 10 | 无 csproj、绝对路径、资源未入库 |

**综合：4.0 / 10** —— 一个"领域价值很高、工程形态很原始"的项目。

---

## 五、是否值得重构

### 结论：**值得重构，但不要重写。**

**不该重写的理由（重要）**

1. **真正的资产在 `NativeCore` 的命令序列里**，不在架构里。注册表键位、`-rac/-rgc/-rmc` 复位链、`power.min_limit` 自适应、PCIe 重训缓冲 1200ms、`TASK_LOGON_INTERACTIVE_TOKEN` —— 这些是"试出来的"，不是"查文档查出来的"。重写等于把踩过的坑再踩一遍。
2. **硬件行为无法在 CI 验证**。没有 90HX/40HX/50HX 实卡就无法确认代码是否正确，重写团队只能靠盲写。
3. 你已经有一份**用户认可的 UI 基线**（`MainWindow.xaml.bak_before_studio_merge`），重写必然破坏它。
4. 单文件、免安装、零依赖的交付形态是对的，重写容易妥协成 Electron/Web 方案，体积与权限模型都会变坏。

**必须重构的理由**

1. P0-1 是真实可利用的安全漏洞，不重构就一直存在。
2. P0-2/P0-3/P0-4/P0-5 是"问题较多"的四个直接来源，且都在**组织方式**上，改架构才能根治。
3. P0-6 意味着**你现在没有可靠的退路**：8 个核心文件未提交，任何一次误覆盖都可能丢失几天工作。
4. 新增一个卡型要改 6 处硬编码 + 重新编译；这种速度撑不起"76 款型号 + 6 种卡 + 多代驱动"的迭代。

**因此：采用绞杀者模式（Strangler Fig）** —— 保留 `NativeCore` 的**实现**，替换它的**边界**；保留 UI 的**视觉**，替换它的**绑定方式**。

---

## 六、重构方案

### 6.1 重构目标（按可验收的方式写）

| 编号 | 目标 | 验收标准 |
| :--- | :--- | :--- |
| G1 | 恢复版本控制可信度 | 21 个 `.bak` 全部清除；`git status` 干净；每个交付 exe 都能用 tag 定位到唯一 commit |
| G2 | 构建可复现 | 任意目录 clone 后一条命令出包；无绝对路径；资源走 Git LFS 或 Release Asset + 哈希清单 |
| G3 | 安全基线达标 | TLS 校验不关闭；更新包校验 SHA-256 + 签名；exe 签名；客户端凭证不落在二进制里 |
| G4 | UI 线程零阻塞 | UI 线程上不存在任何 `Process.WaitForExit` / WMI / `.Result` / `.Wait()`；传感器刷新在后台线程 + 节流 |
| G5 | 控件绑定编译期可校验 | `FindName` 归零；页面拆为独立 `UserControl`，用 `x:Name` + 强类型字段 |
| G6 | 失败可观测 | 空 catch 归零（改结构化日志）；异常上抛到 UI 提示；关键操作为"操作-结果-回滚点"三元组 |
| G7 | 单文件职责 ≤ 400 行 | `NativeCore` 拆为 ≥ 8 个领域服务，每个 ≤ 600 行 |

### 6.2 优先级与排期

| 阶段 | 内容 | 工作量 | 交付物 |
| :--- | :--- | :--- | :--- |
| **P0 止血**（1-2 周） | G1 + G3 安全修复 + G4 卡顿修复 + G6 日志改造 | 小 | 可回滚的快照 + 安全补丁版本 v5.1 |
| **P1 拆骨**（3-5 周） | G2 建 `.csproj` / 拆 `NativeCore` 为领域服务 / 删死代码 | 中 | 结构清晰、可增量构建 |
| **P2 换壳**（4-6 周） | G5 XAML 拆分 + 编译期绑定 + 设计令牌 | 中大 | 12 个独立页面 UserControl |
| **P3 收口**（3-4 周） | MVVM + 测试补齐 + AI 层工具调用 + 打包工程化 | 中大 | 可持续迭代的工程形态 |

### 6.3 目标目录结构

```
HXToolbox.sln
├─ src/
│  ├─ HXToolbox.App/                    # WPF 壳：启动、导航、异常兜底、DI 容器
│  │   App.xaml / App.xaml.cs           #   ≤ 150 行
│  │   ShellWindow.xaml                 #   只含导航骨架 + ContentControl
│  │   Views/
│  │      HardwareView.xaml             #   每页一个 UserControl，独立 x:Name
│  │      SpoofView.xaml
│  │      OverlayView.xaml
│  │      Unlock90HxView.xaml
│  │      Unlock50HxView.xaml
│  │      Unlock40HxView.xaml
│  │      DualGpuView.xaml
│  │      DlssView.xaml
│  │      PowerView.xaml
│  │      LogsView.xaml / SponsorView.xaml / UpdateView.xaml
│  │   ViewModels/                      # 与 View 一一对应，无 UI 类型泄漏
│  │   Styles/Tokens.xaml               # 唯一色值来源（替换 89 种硬编码）
│  ├─ HXToolbox.Domain/                 # 纯逻辑，无 P/Invoke、无 IO
│  │   Models/   (GpuInfo, PowerProfile, UnlockPlan, SensorSnapshot)
│  │   Services/ (SpoofService, UnlockService, PowerService, PatchService, DlssService)
│  │   Abstractions/ (IRegistry, IProcessRunner, IGpuQuery, IClock, ILogger)
│  ├─ HXToolbox.Infrastructure/         # 所有副作用集中在这里
│  │   Registry/     RegistryStore.cs
│  │   Process/      ProcessRunner.cs   # 统一超时 / 退出码 / 输出捕获
│  │   Nvidia/       NvidiaSmiClient.cs # 单例缓存 + 后台刷新，杜绝 UI 线程调用
│  │   Wmi/          WmiQuery.cs
│  │   Boot/         BcdEditor.cs
│  │   Scheduling/   TaskSchedulerClient.cs  # 用 TaskScheduler Interop 替代反射 COM
│  │   Network/      HttpDownloader.cs  # 强制 TLS 校验 + SHA256 + 断点续传
│  │   Resources/    EmbeddedResourceStore.cs
│  └─ HXToolbox.Ai/                     # AI 助手独立工程
│      AssistEngine.cs / ActionRegistry.cs / Views/
├─ tests/
│  ├─ HXToolbox.Domain.Tests/           # 纯逻辑单测（伪装名映射、版本比较、INI 解析、型号判定）
│  └─ HXToolbox.Infrastructure.Tests/   # 注册表/进程用 mock 验证调用契约
├─ build/
│  ├─ build.ps1                         # 无绝对路径，参数化输出目录
│  └─ assets.manifest.json              # 55 个二进制条目的名称/版本/来源/SHA256
└─ docs/
   ├─ ADR/                              # 架构决策记录
   └─ REGRESSION.md                     # 硬件回归清单（哪些必须真机验证）
```

### 6.4 模块拆分建议（`NativeCore` 的切分映射）

| 目标模块 | 从 `NativeCore` 迁出 | 行数量级 | 备注 |
| :--- | :--- | ---: | :--- |
| `NvidiaSmiClient` | `QueryGpuSensors` / `QueryGpuPowerRange` / `QueryGpuTempThresholds` / `ReadPstate` / `FindNvidiaSmi` / `ReadGpuUtilization` | ~250 | **加缓存 + 后台刷新**，这是卡顿根因所在 |
| `SpoofService` | `ApplyGpuRenameAsync` / `RestoreMiningGpu*` / `NormalizeGpuDisplayName` / 型号表 | ~700 | 型号表移到 `models.json` 资源，不再硬编码 |
| `UnlockService` | `ApplyNvPermissiveUnlockAsync` / `InstallPcieMode*` / `RepairBootMenuAsync` / `Create90hxFirmwareUsbAsync` | ~1,200 | 保留全部时序与缓冲参数，只改边界 |
| `PowerService` | `ApplyGpuPowerSaveCore` / `RestoreGpuPowerSaveCore` / `LoadDynamicClockParams` / 计划任务注册 | ~500 | 计划任务改 `TaskScheduler` Interop |
| `PatchService` | `UnlockNvAppOverlayAsync` / `CleanCefCacheInternal` | ~400 | 补丁正则集中成配置 |
| `DlssService` | `InspectDlssRuntimeAt` / `PreflightDlssDeploy` / `InjectDlssG` / `PullAndReplaceDlssAsync` | ~900 | 镜像测速与下载分离 |
| `UpdateService` | `CheckAppUpdateAsync` / `DownloadAppUpdateAsync` / `ApplyAppUpdate` | ~350 | **加 SHA-256 + 签名校验** |
| `HardwareInventory` | `DetectAllMiningGpus` / `FindPnpInstanceIdByClassSubKey` / `GetDeviceProblemCode` | ~450 | 纯查询，可单测 |
| `AppPaths` / `ResourceStore` | `GetDataDirectory` 系列 / `GetPackagedResource` / `ExtractPackagedTree` | ~200 | 去掉 `D:\app` 回退 |
| `ProcessRunner` | 散落各处的 `ProcessStartInfo` + `WaitForExit` | ~150 | 统一超时、退出码、编码 |

切分原则：**先搬代码不改逻辑**（保持行为一致），再逐模块替换边界。每一刀切完立即真机回归一次，不留大爆炸式合并。

### 6.5 关键缺陷的具体修法

| 缺陷 | 修法 |
| :--- | :--- |
| TLS 全局关闭 | 删除 `ServerCertificateValidationCallback`；如确有自签需求，改为**按域名白名单**校验 |
| 自更新无校验 | Release 附 `SHA256SUMS` + `.sig`；下载后先验哈希再验签名，任一失败即中止并保留旧 exe |
| `nvidia-smi` 阻塞 UI | `NvidiaSmiClient` 后台线程每 2s 采集一次写入 `volatile` 快照；UI 只读快照，永不触发子进程；温度门限 60s 缓存 |
| `RefreshSystemStatus` 同步 | 改 `async Task`，`DetectAll` 放 `Task.Run`，UI 先渲染"检测中…"再回填；启动时只跑一次 |
| `FindName` 字符串绑定 | 每页拆 `UserControl`，`x:Name` + 生成字段；`DataContext` 绑定 ViewModel；删除 `_navItems/_pageHosts` 下标数组，改 `Dictionary<PageId, UserControl>` |
| `.Result / .Wait()` | 全链路 `async/await`；确需同步的入口用 `ConfigureAwait(false)` + `Task.Run` 隔离 |
| 空 catch | 统一 `ILogger`，至少记 `Warning` + 上下文；只有"确认可忽略"处保留并**写明原因** |
| `e.Handled = true` | 改为记录 + 提示用户"本次操作失败，其余功能不受影响"，不再静默 |
| 硬编码 `D:\app` | 全部删除；资源只从嵌入资源或 `AppPaths.Assets` 取 |
| 89 种硬编码颜色 | 抽 12 个语义 Token（`Surface` / `SurfaceAlt` / `Border` / `TextPrimary` / `TextSecondary` / `Accent` / `Success` / `Warning` / `Danger` …），全量替换 |
| 单文件 48MB | 资源拆「内置必需」(图标/字体/样式 ≈ 2MB) + 「首次运行按需下载」(DLSS/解锁工具包，带哈希)；exe 可降到 8MB 以内 |
| 无 csproj | 建 SDK-style `.csproj` + `dotnet build`；WPF 资源改 `Resource` 编译为 BAML（保留 `XamlReader` 只作为主题热替换） |

### 6.6 潜在风险点

| 风险 | 等级 | 说明与应对 |
| :--- | :---: | :--- |
| **驱动/引导操作无法自动验证** | 高 | 注册表伪装、BCD 通道、PCIe 模式只能真机验证。应对：每次切分后固定清单回归（伪装→设备管理器名称→重启→Code 43 检查→还原），并把清单写进 `docs/REGRESSION.md` |
| **重写解锁逻辑导致变砖式回归** | 高 | 保持"搬代码不改逻辑"，禁止在切分的同时"顺手优化"。任何逻辑改动单独提交、单独回归 |
| **杀软误报加剧** | 中高 | 现名 `VirusOverlay` + 窗口钩子 + 全局 `requireAdministrator` + 未签名，本就敏感。重构后 exe 哈希全变，SmartScreen/杀软信任归零。应对：**尽早做代码签名**，并把窗口跟踪模块改名、缩小行为面 |
| **UI 基线被破坏** | 中高 | 你已明确认可 `bak_before_studio_merge` 布局。应对：换壳阶段用"像素对照"（同一分辨率截图 diff），先只换绑定不改样式，样式改造放最后一阶段 |
| **单文件交付形态被妥协** | 中 | SDK-style + 多工程可能诱使引入运行时依赖。应对：锁定 `net48`（或 `net8-windows` 自包含单文件），禁止引入需要额外安装的组件 |
| **无测试基线的"重构"变味** | 中 | 零测试下重构容易变成"看起来更整齐但功能更少"。应对：先给纯逻辑（型号映射、版本比较、INI 解析、CSV 解析、注册表路径拼装）补测试，再动结构 |
| **当前未提交代码丢失** | 中 | 8 个核心文件未提交。应对：**重构第一步就是 `git add -A && git commit` 打 tag `pre-refactor-20261020`，并整目录冷备份一份** |
| **二进制资源来源不明** | 中 | 55 个条目无清单。应对：建 `assets.manifest.json` 逐个补来源与 SHA256；补不出来的标记 `unknown`，后续逐步替换 |
| **工作量被低估** | 中 | 8,193 行领域逻辑 + 12 页 UI。应对：按阶段交付可用版本，每阶段结束都是能跑的 exe，不做长周期大分支 |

### 6.7 建议的下一步（本周即可做的 7 件事）

1. `git add -A && git commit -m "chore: 重构前快照" && git tag pre-refactor-20261020`，并整目录复制一份冷备份
2. 删掉 `ServicePointManager.ServerCertificateValidationCallback`，更新流程加 SHA-256 校验（**半小时，风险最高收益最大**）
3. `QueryGpuTempThresholds` 移出 `Dispatcher.Invoke`，加 60 秒缓存（**消除周期性卡顿**）
4. `RefreshSystemStatus` 改 async，启动时只跑一次
5. 21 个 `.bak` 归档到一个 `_archive/`（不进 Git），此后禁止再用 `.bak` 命名做版本管理
6. 建 `HXToolbox.sln` + 一个 `net48` csproj，把 `src/*.cs` 原样纳入，`build.ps1` 改为调 `msbuild`——**先做到"能编译"，不动一行代码**
7. 写 `docs/REGRESSION.md`：把"伪装 / 解锁 / 省电 / 覆盖 / DLSS / 更新"六条链路的真机验证步骤固化成清单

---

## 附：本次审计的边界说明

- 未运行 `build.ps1`（会覆盖 `D:\app\90HX Studio.exe` 与桌面副本），因此**未采集编译器警告/错误实际数量**，仅依据 `/warnaserror+` 判断"当前源码在零警告状态下可编译"
- 未在真实硬件上执行解锁/伪装/省电流程，硬件行为正确性不在本次审计范围
- 未审计 `mining_unlock_toolkit/` 内的第三方 PowerShell 工具链（`nvidia_patcher_windows` 等，属外部代码）
- 行数、引用数、异常数、颜色数均为静态统计（`grep` / `wc`），可作为重构前后对比基线

---

RainCandy Technology · 审计报告 v1.0 · 2026-10-20
