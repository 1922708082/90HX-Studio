# HX Studio 6.0 迁移方案（V1 · 待评审）

> 文档性质：**给第三方评审/优化用的完整方案文本**
> 版本：V1.0　日期：2026-10-20
> 适用对象：独立软件「HX家族多功能工具箱 / 90HX Studio」，Windows 桌面 GPU 工具
> 阅读要求：第 1-2 章是**不可争议的事实与约束**，请勿在优化时假设它们可以变；第 5 章是**我的置信度自评**，请重点打这里

---

## 0. 这份文档要解决什么问题

现有程序（v5.0）是一个 WPF 单文件桌面工具，功能完整但**工程形态与界面都已落后**。
现有一套外部产出的高保真 HTML 界面原型（v6.0），视觉质量显著优于现状。
本方案要回答三件事：

1. 能不能把 HTML 界面接进这个程序？（技术路线）
2. 接进去的代价和风险是什么？（能不能落地）
3. 分几步做、每步怎么验收？（工程执行）

**请评审方重点审第 4、5、6 章**：架构选型是否成立、我的置信度是否有高估、风险是否有遗漏。
第 7 章列出了我主动交出的 12 个未决问题，其中任何一条被推翻，方案都要改。

---

## 1. 项目事实基线（实测数据，非估计）

> 全部来自 2026-10-20 对 `D:\app` 的静态审计。这些数字是评审的公共前提，不可改动。

### 1.1 代码规模

| 项 | 实测 |
| :--- | :--- |
| 参与构建的源码 | 16 个文件，**21,610 行**（13 个 .cs + 3 个 .xaml） |
| `NativeCore.cs` | **8,193 行**，一个 `public static class`，**132 个 public static 方法** |
| `App.cs` | **4,112 行**，单个 `class App : Application`，**250 个私有字段**，**238 处 `_window.FindName("...")`**，`BindControls` 539 行，`InitEvents` **1,190 行** |
| `MainWindow.xaml` | **2,664 行**，12 个功能页全在一个文件，运行时 `XamlReader.Load` 加载（无编译期校验） |
| 5 个 `*Manager.cs` | 共 105 个公开方法，**94 个是 `return NativeCore.XXX()` 纯转发**（假分层） |
| `ScriptEngine.cs` | 146 行，**0 引用**（死代码） |
| 根目录未参与构建的 .cs | 14 个文件，2,109 行 |
| AI 助手模块 | 4 个文件 **3,870 行**（`AiAssistantEngine` 1,413 / `AiAssistantWindow` 1,243 / `.xaml` 740 / `AiAgentActionRegistry` 474） |

### 1.2 质量指标

| 指标 | 实测 |
| :--- | :--- |
| `catch` 总数 / 其中空 catch | 397 / **224（56%）** |
| 全局异常处理 | `App.cs:505` `e.Handled = true` —— 所有 UI 异常被吞 |
| XAML 硬编码色值 | **215 处，89 种不同颜色**，无 Token |
| 硬编码本机路径 | `D:\app\...` 13 处，`C:\Users\<用户名>` 4 处 |
| UI 线程同步子进程 | `App.cs:2685` 在 `Dispatcher.Invoke` 内调 `QueryGpuTempThresholds()`（同步 `nvidia-smi`），每 1.5 秒一次 |
| 阻塞式异步 | `.Result` / `.Wait()` 共 13 处 |

### 1.3 构建与交付

| 项 | 实测 |
| :--- | :--- |
| 构建方式 | `build.ps1` 手写 13 个文件路径调用 `csc.exe`（C# 5.0） |
| 工程文件 | **无 .sln、无 .csproj、无 NuGet、无 app.config** |
| 目标框架声明 | **无**（`AssemblyInfo.cs` 里没有 `TargetFrameworkAttribute`） |
| 构建参数 | `/warn:4 /warnaserror+`（有警告即失败） |
| 构建路径 | 全量绝对路径 `D:\app\...`，换目录即失败 |
| 产物 | 单 exe **48.6 MB**，未签名（`NotSigned`），清单 `requireAdministrator` |
| 内嵌资源 | `assets.zip` 47 MB 压缩 / 100 MB 解压 / 55 个二进制条目 |
| 资源入库 | `assets.zip` 与 `mining_unlock_toolkit/` **均在 .gitignore 中** |
| 可复现性 | **新机器 clone 后无法构建** |

### 1.4 版本控制现状

| 项 | 实测 |
| :--- | :--- |
| Git 提交数 | **15 次**（覆盖 v4.1 → v5.0） |
| 未提交的核心文件 | **8 个**（`App.cs`、`NativeCore.cs`、`MainWindow.xaml` 等全在里面） |
| `src/` 下 `.bak*` 文件 | **21 个** |
| 仓库体积 | **1.8 GB**（9 个备份 exe + 6 份 assets.zip） |

### 1.5 新界面原型实测

| 项 | 实测 |
| :--- | :--- |
| 文件 | `hx_studio_6_0.html`，**3,662 行**（CSS 1,917 / HTML 1,116 / JS 618），150 KB |
| 外部依赖 | **0**（无 CDN、无 webfont、无 `<img>`，全 SVG/CSS 手绘） |
| 页面数 | **6**（硬件总览 / 固件解锁 / 显卡伪装 / 动态节能 / DLSS5 / 系统维护） |
| 交互 | 27 button、40 onclick、7 select、3 canvas |
| 动效 | 17 组 `@keyframes`、28 处 3D 变换、72 处 `box-shadow`、6 处 `backdrop-filter` |
| 数据 | **全部写死**（`616.92`、`0000:01:00.0`、`v3.7.10`、`共 2 个还原点`） |
| 业务逻辑 | **零**。所有 JS 函数均为动画模拟（`executeSimulatedAction` / `simulateRollback` 等） |

**页面覆盖度：6 / 12。** 缺整页：游戏覆盖、双卡协同、审计日志、软件更新；解锁页只有 90HX（缺 50HX/40HX 与通道 B/C/D）；伪装型号仅 5 款（现程序 76 款）。

---

## 2. 硬约束（优化时不可假设可变）

| # | 约束 | 原因 | 可否松动 |
| :-- | :--- | :--- | :--- |
| C1 | **必须是单文件 exe，免安装** | 目标用户是矿卡玩家，靠网盘/群文件传播，不接受安装器 | 否 |
| C2 | **必须能离线使用**（界面与已解锁功能） | 用户机器可能在无网环境；仅有下载类功能需要网络 | 否 |
| C3 | **必须请求管理员权限** | 要改 HKLM 注册表、BCD 引导项、重载显卡驱动、注册计划任务 | 否 |
| C4 | **不能要求用户额外装运行库** | 现状是"双击就能跑"，任何前置安装都会造成流失 | 尽量 |
| C5 | **界面视觉以 v6.0 原型为基准** | 用户已明确认可该视觉，不允许降级为"接近" | 否 |
| C6 | **业务逻辑（132 个能力）不改语义** | 这些是试出来的硬件操作序列，改错会砖 | 否 |
| C7 | **硬件行为无法在 CI 验证** | 需要真机 90HX/50HX/40HX | 否（接受） |
| C8 | 允许升级构建工具链 | C# 5.0 是历史遗留，非产品要求 | **可**（见 Q4） |
| C9 | 允许拆分多进程 | 会影响启动体验 | **可**（见 Q1） |
| C10 | 允许增加约 2~3 MB 体积 | 单文件从 48.6 MB 增至约 51 MB，用户无感 | 可 |

---

## 3. 候选方案与取舍

| 方案 | 做法 | 视觉还原 | 单文件 | 依赖 | 工作量 | 结构性收益 |
| :--- | :--- | :---: | :---: | :--- | ---: | :--- |
| **A1** | WebView2 全窗口宿主，**单进程 + 管理员** | **100%** | 保持 | WebView2 Runtime | 约 18 人日 | 消灭 `MainWindow.xaml` + 全部 FindName 绑定 |
| **A2** | WebView2 UI 进程（**非提权**）+ 提权 Broker 进程 | 100% | 保持 | WebView2 Runtime | 约 23 人日 | 同上 + 最小权限 + 文件对话框正常 |
| **B** | 把 HTML 移植为 XAML | 约 75% | 保持 | 无 | 约 30 人日 | 同上，但要重写 12 页 |
| **C** | WPF 框架（标题栏/侧栏）+ WebView2 内容区 | 约 90% | 保持 | WebView2 Runtime | 约 25 人日 | 两套渲染栈并存 |
| **D** | 整体改为 Electron / Tauri | 100% | **否** | 巨大 | 约 20 人日 | 全部 C# 硬件逻辑要重写 |

### 为什么排除 B

`backdrop-filter`（毛玻璃）、`transform-style: preserve-3d`（3D 显卡）、CSS 粒子场，这三项是 v6.0 视觉的核心，**WPF 里没有对应能力**：

- WPF 只有 `BlurEffect`（模糊元素自身），**没有背景模糊**，毛玻璃做不出来
- 3D 只能用 `Viewport3D` 重建，或退化为序列帧/静态图
- 粒子可用 `CompositionTarget.Rendering` 手绘，但 60fps 下 CPU 占用高

结论：移植到 XAML 只能得到"同一个布局的另一个版本"，不是同一个界面。**花 30 人日换 75% 效果，不如花 18 人日换 100%。**

### 为什么排除 D

132 个能力的实现全部在 C# 里（注册表、BCD、`nvidia-smi`、COM），换成 Electron 等于把这部分整体重写，且重写无法验证。**风险不可接受。**

### 为什么推荐 A1 而不是 A2

**A2 才是微软推荐架构**（WebView2 宿主应运行在标准权限；提权操作隔离到独立进程）。但在本项目里：

- A1 的已知缺陷（无法弹出 HTML 文件上传/保存对话框）**可以绕开**：本方案所有路径选择都走 C# 侧 `FolderBrowserDialog`，HTML 里不使用 `<input type="file">`
- A2 要多处理：UAC 时序、Broker 崩溃重连、命名管道权限、单实例互斥、双进程退出顺序 —— 对单人维护的项目是新增的不确定性
- **采用"接口先行"**：业务能力统一走 `IHardwareService`，A1 用进程内实现，A2 只是换一个 `PipeHardwareService` 实现。**这条让 A1→A2 的迁移成本降到约 3 人日**，因此先 A1 不构成技术债

---

## 4. 推荐架构（A1）

### 4.1 进程与图层

```
HXStudio.exe  (requireAdministrator, .NET Framework 4.6.2+)
│
├─ ShellWindow            WPF 窗口：WindowStyle=None + WindowChrome(CaptionHeight=0)
│   │                     AllowsTransparency=False   ← 必须，否则 HwndHost 无法渲染
│   └─ WebView2           Dock=Fill
│       └─ https://hx.local/index.html   ← 内嵌资源经虚拟主机映射
│
├─ Bridge                 CoreWebView2.WebMessageReceived / PostWebMessageAsJson
│   └─ ActionRouter       解析 cmd → 权限校验 → 分发
│       └─ IHardwareService
│            └─ InProcHardwareService  → 复用现有 NativeCore（A1）
│               (PipeHardwareService → Broker 进程)  （A2 预留）
│
└─ NativeCore (8,193 行)  本次**不动逻辑**，只做接口包装
```

### 4.2 窗口与拖拽

| 需求 | 实现 | 备注 |
| :--- | :--- | :--- |
| 无边框 | `WindowStyle=None` | |
| 可缩放 | `WindowChrome.ResizeBorderThickness = 6` | WPF 原生处理边缘命中 |
| 拖动 | 优先 CSS `-webkit-app-region: drag` | **需实测**（见 R3） |
| 拖动备选 | WPF 顶部 44px 透明 `Border` + `DragMove()` | 若上面的不生效 |
| 最小化/最大化/关闭 | JS → `window.min/max/close` → WPF | 不用 HTML 伪造 |
| 圆角 | 由 WPF Window 不做圆角，交给 HTML 自绘 | 避免 Win10/Win11 差异 |
| 阴影 | HTML 自绘（原型已有） | |

### 4.3 WebView2 初始化要点

```csharp
// 1. 环境（用户数据目录必须可写，且不要放网络盘）
var env = await CoreWebView2Environment.CreateAsync(
    browserExecutableFolder: null,                          // 用系统 Evergreen Runtime
    userDataFolder: Path.Combine(NativeCore.GetDataDirectory(), "webview2", "udf"));

// 2. 虚拟主机映射（避免 NavigateToString 的 2MB 限制与编码差异）
webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
    "hx.local", webAssetDir, CoreWebView2HostResourceAccessKind.DenyCors);

// 3. 加载
webView.CoreWebView2.Navigate("https://hx.local/index.html");
```

### 4.4 必须关闭的默认能力（安全基线）

| 设置 | 值 | 理由 |
| :--- | :--- | :--- |
| `AreDevToolsEnabled` | `false` | 不给用户开 F12 |
| `AreDefaultContextMenusEnabled` | `false` | 去右键菜单 |
| `AreBrowserAcceleratorKeysEnabled` | `false` | 禁 F5 / Ctrl+R / Ctrl+Shift+I |
| `IsStatusBarEnabled` | `false` | 去左下角链接提示 |
| `IsZoomControlEnabled` | `false` | 防止误缩放破坏布局 |
| `IsPasswordAutosaveEnabled` / `IsGeneralAutofillEnabled` | `false` | 无表单需求 |
| `NavigationStarting` | 非 `hx.local` 一律 `Cancel` | 防止被诱导跳转 |
| `NewWindowRequested` | `Handled = true; e.Handled = true` | 禁弹新窗 |
| `WebResourceRequested` | 拦截外部域名 | 离线保证 |

**内容侧配套硬规则**（写入前端规范）：所有动态文本一律 `textContent`，**禁止 `innerHTML` 拼接外部数据**。理由是：进程是管理员权限，任何 HTML 注入等于管理员代码执行。

### 4.5 资源与版本

- `index.html` + 依赖全部内嵌为资源，**首次运行解压到** `%ProgramData%\CMP90HX_Toolbox\web\{version}\`
- 版本号变化时重新解压，旧目录清理
- 这样后续**只换 HTML 就能更新界面**，不用重编 exe（对高频迭代是决定性优势）
- C# 侧维护 `webVersion`，与 exe 版本解耦；`app.init` 时上报，便于排查"用户跑的哪版界面"

### 4.6 WebView2 Runtime 缺失时的三级兜底

| 级别 | 措施 |
| :--- | :--- |
| 1 | 内嵌 **Evergreen Bootstrapper**（约 1.5 MB），检测缺失时静默执行 `/silent /install` |
| 2 | 静默安装失败（离线/被拦截）→ 弹窗：说明原因 + 打开官方下载页 + 提供"离线安装包"链接 |
| 3 | 用户拒绝安装 → **不进入程序**，明确提示"本版本界面需要 WebView2 运行时"，并保留 v5.0 旧版下载入口 |

> **不采用**"保留完整旧 WPF 界面做降级"：等于维护两套 UI，且旧界面的 `MainWindow.xaml`(2,664 行) 正是本次要消灭的对象。
> **需评审确认**：这个取舍是否可接受（见 Q10）。

### 4.7 与现有代码的接缝

| 现有资产 | 处理 |
| :--- | :--- |
| `NativeCore`（8,193 行） | **不动**。外面包一层 `IHardwareService`，内部仍调它 |
| 5 个 `*Manager.cs`（假分层） | **删除**，路由直接指向 `NativeCore` |
| `App.cs` 的 UI 绑定层 | **删除**（`BindControls` / `InitEvents` / 238 处 `FindName` / 250 字段） |
| `MainWindow.xaml` | **删除**（2,664 行） |
| `AiAssistant*` 4 个文件 | **删除**（3,870 行，用户已确认不要） |
| `ScriptEngine.cs` | **删除**（死代码） |
| 根目录 14 个游离 .cs | **归档到 `_archive/`**，不入构建 |
| `SystemDetector` / `NativeCore` 的硬件能力 | 保留，改为被 `ActionRouter` 调用 |

**预期代码量变化：21,610 → 约 14,000 行**（删约 7,600 行界面/绑定/AI/门面，新增约 1,500 行宿主与桥接）。

---

## 5. 可行性置信度自评（请重点审这一章）

> 分三档：**高** = 有文档依据或已在同类项目验证；**中** = 原理成立但本机未验证；**低** = 存在实质性不确定性，可能推翻方案

| # | 命题 | 置信度 | 依据 | 若不成立的影响 |
| :-- | :--- | :---: | :--- | :--- |
| F1 | WebView2 能 100% 还原该 HTML 的视觉（含毛玻璃、3D、粒子） | **高** | 它就是 Chromium；HTML 零外部依赖 | 方案失去主要理由 |
| F2 | `postMessage` 双向桥接足以支撑 36 个 action | **高** | 官方标准机制，消息量级很低（KB 级） | 需换 host object，改动小 |
| F3 | 单文件 exe + 内嵌 HTML 可行 | **高** | 现程序已在用 `/resource:` + 运行时解压 | 需退回"exe + 文件" |
| F4 | .NET Framework 4.6.2 是硬门槛，需补 `TargetFrameworkAttribute` 与 app.config | **高** | 微软文档明确要求 4.6.2+；现项目**两者都没有** | 需补配置，工作量小 |
| F5 | C# 5.0 + `csc.exe` 可以引用 WebView2 SDK 编译 | **中** | SDK 面向 4.6.2，语法不冲突；但未实际编译验证 | 需升级到 msbuild/csproj（Q4） |
| F6 | **管理员进程中 WebView2 可稳定运行** | **中** | 微软**建议**非提权，但未禁止；社区存在"管理员下文件对话框失效"的报告 | 需转 A2（+3~5 人日） |
| F7 | 绕过 HTML 文件对话框限制（全部路径选择走 C#） | **高** | 本方案协议已把 `dialog.browse` 放在宿主侧 | — |
| F8 | `-webkit-app-region: drag` 在 WebView2 内可用 | **中** | Chromium 支持，但依赖版本与控制器设置 | 用 WPF 透明拖拽条替代，影响小 |
| F9 | **CMP 矿卡上 Chromium 能启用 GPU 加速** | **低** | Chromium 有驱动黑名单；矿卡未被正常识别，可能落软件渲染 | **动效可能只有 15~25fps**，需强烈降级或换路线 |
| F10 | 目标用户机器上 WebView2 Runtime 覆盖率高 | **中** | Win11 自带、Win10 多已推送；但**官方要求开发者自行检测** | 已有三级兜底，影响可控 |
| F11 | 首屏 < 400ms | **中** | 本地文件 + 已预解压，理论可行；内存 VM 启动有冷启动成本 | 放宽到 800ms + 骨架屏 |
| F12 | 该架构能根治"UI 线程跑子进程"的卡顿 | **高** | 采集转后台 + 推送，UI 不再直接调用 | — |

**自评结论：12 条里 6 条"高"、5 条"中"、1 条"低"。**
**唯一可能推翻方案的是 F9（矿卡 GPU 加速），必须在写一行代码之前验证。**

---

## 6. 风险登记（含验证方法）

| # | 风险 | 等级 | 验证方法（必须先做） | 缓解 |
| :-- | :--- | :---: | :--- | :--- |
| R1 | **矿卡 GPU 加速被禁 → 动效卡** | **高** | 用 30 行最小 WebView2 demo 加载原型，在 90HX 真机看 `chrome://gpu` 与实测帧率 | ①`perf-low` 全降级 ②粒子/3D 改静态 ③最坏退回 B 方案 |
| R2 | 管理员进程下 WebView2 异常 | 中 | 同一 demo 以管理员身份运行，测加载、输入、拖拽、缩放、最小化还原 | 转 A2 双进程 |
| R3 | 拖拽/缩放失效 | 中 | demo 里实测 `app-region: drag` | WPF 透明条 + `DragMove` |
| R4 | Runtime 缺失导致白屏 | 中 | 卸载 Runtime 后启动 | 三级兜底（4.6） |
| R5 | WebView2 进程崩溃 → 界面空白 | 中 | 手动结束 `msedgewebview2.exe` | `ProcessFailed` 监听 + 重建一次 |
| R6 | 高 DPI 下布局错乱 | 中 | 125% / 150% 截图对照 | 全部尺寸用 `rem`/相对单位 |
| R7 | 杀软误报加剧 | 中 | 提交 VirusTotal 看命中率 | 代码签名（本次建议一并做） |
| R8 | 界面与 C# 协议对不上 | 中 | 交付 `BRIDGE-CONTRACT.md` 逐条核对 | 契约先行，前端按规范写 |
| R9 | 界面热更新破坏旧版兼容 | 低 | 版本号配对校验 | `webVersion` + 契约版本号，不匹配拒绝加载 |
| R10 | 重构期间丢代码 | 中 | 开工前 git commit + tag + 冷备份 | 强制前置动作 |

---

## 7. 我主动交出的 12 个未决问题（请逐条给出结论）

> 这些问题我**没有把握**或**存在更优解**，请给出结论 + 依据。若你认为某条我判断错了，直接指出并说明证据。

| # | 问题 | 我的倾向 |
| :-- | :--- | :--- |
| Q1 | A1（单进程提权）还是 A2（双进程分离）？考虑到微软"建议非提权"的立场，A1 是否应该被否决？ | A1 先行，接口预留 A2 |
| Q2 | 若不升 A2，管理员进程内的 WebView2 长期稳定性有无已知坑？ | 未找到明确证据 |
| Q3 | 提权时机：启动即 UAC，还是先起非提权 UI 再按需提权？ | 启动即提权（保持现状行为） |
| Q4 | 是否借这次机会从 `csc.exe` 迁到 `.csproj` + msbuild？代价与风险？ | 建议迁，顺带解决"不可复现构建" |
| Q5 | 单文件怎么保住？DLL 内嵌解压 vs 放同目录 vs 静态链接 loader？ | 内嵌解压（沿用现有机制） |
| Q6 | 桥接协议 `cmd/res/evt/prg` 是否够用？缺不缺"取消 / 超时 / 并发互斥 / 幂等"语义？ | 需补取消与超时 |
| Q7 | 长任务执行中用户关闭窗口，应如何保证不半途中断？ | 需明确（当前无方案） |
| Q8 | 界面与 C# 的版本如何配对？HTML 热更新会不会打破契约？ | 契约版本号 + 拒绝不匹配 |
| Q9 | CMP 矿卡上如何让 Chromium 走硬件加速？有无厂商/参数解法？ | 未知，需实测 |
| Q10 | Runtime 缺失时"不降级、直接提示安装"，会不会流失过多用户？ | 倾向不维护双 UI |
| Q11 | 是否应该把"改 BCD / 改注册表"这类高危能力从界面隐藏，改为配置项开启？ | 值得考虑 |
| Q12 | 本次是否必须一并做代码签名？成本与收益？ | 建议做（哈希变化会重置 SmartScreen） |

---

## 8. 实施阶段与验收门禁

> 每个阶段结束都必须产出**可运行的 exe**，不允许长周期大分支。

| 阶段 | 内容 | 验收门禁 |
| :--- | :--- | :--- |
| P0 可行性验证 | 30 行 WebView2 demo + 真机实测 R1/R2/R3 | **帧率达标 + 管理员下正常 + 拖拽可用**，否则方案重选 |
| P1 保护 | git commit + tag + 冷备份 + 原型入库 | `git status` 干净，可回退 |
| P2 拆除 | 删 AI 4 文件 / 5 个门面 / `ScriptEngine` / 归档游离 .cs | 编译通过，行为不变 |
| P3 宿主 | ShellWindow + WebView2 + 资源解压 + Runtime 兜底 | 十种环境场景（首次/无 Runtime/高 DPI/多屏/远控…）全过 |
| P4 桥接 | 协议实现 + 36 个 action + 错误码 + 进度 | 每个 action 有真实响应，无"点了没反应" |
| P5 接入 | 12 个业务能力接入真实 `NativeCore` | 六条链路（伪装/解锁/省电/覆盖/DLSS/更新）真机通过 |
| P6 删除旧界面 | 删 `MainWindow.xaml` + `App.cs` 绑定层 | 代码量降至约 14,000 行，无死引用 |
| P7 打磨 | 性能、降级、签名、体积 | 首屏/内存/帧率达标 |
| P8 发布 | 回归 + 灰度 | 旧版并行保留 |

**P0 是硬门禁**：R1 不过，本方案作废，回到 B 或 C 重新评估。这一点必须在投入前说清楚。

---

## 9. 失败模式与回退

| 失败形态 | 回退动作 |
| :--- | :--- |
| WebView2 在矿卡上性能不可用 | 退回 C（WPF 框架 + WebView 仅内容区）或 B |
| 管理员进程下 WebView2 不稳定 | 转 A2 双进程（接口已预留，约 3~5 人日） |
| Runtime 覆盖率过低 | 内嵌 Bootstrapper 强制安装 + 离线包 |
| 硬件回归失败 | 旧 exe 并行保留，一键回退 |

---

## 10. 复核标准（优化版回来后，我会按这 12 条打分）

1. **是否回答了第 7 章全部 12 个问题**，且每题有依据而非"建议采用最佳实践"
2. **是否承认或反驳了 F9（矿卡 GPU 加速）**，给出了可执行的验证步骤
3. **是否处理了"管理员 + WebView2"的官方立场冲突**（不是回避）
4. **是否保住了 C1~C6 六条硬约束**（单文件、离线、提权、免装库、视觉、逻辑不变）
5. **是否给出了 P0 门禁的通过/失败判据**（可量化，不是"感觉可以"）
6. **方案是否可增量交付**（每阶段有可运行 exe）
7. **是否明确了"界面与 C# 的契约版本"机制**
8. **是否覆盖了 Runtime 缺失 / 进程崩溃 / 热更新破坏兼容 三种异常**
9. **是否对"删除 7,600 行"给出了依赖检查方法**（防删漏、防死引用）
10. **是否处理了安全边界**：管理员进程 + 网页内容 + 桥接，三者组合的攻击面
11. **是否给出了真机回归清单**（六条链路，可勾选）
12. **工期与工作量的估算是否拆到人日级**，且识别出关键路径

**通过标准：12 条中至少 10 条通过，且第 2、3、5、10 条必须通过。**

---

## 11. 一句话总结

> 这条路技术上走得通，**唯一真正的不确定性是"矿卡上 Chromium 愿不愿意用 GPU"**。
> 在验证这一条之前，任何细化到页面级的设计都是在赌。

---

RainCandy Technology · 迁移方案 V1（待评审） · 2026-10-20
