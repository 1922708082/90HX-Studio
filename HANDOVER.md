# HX家族多功能工具箱 v5.0 — 开发交接文档
## 一、项目概况

- 名称：HX家族多功能工具箱（原 90HX Studio / CMP 90HX 控制中心）
- 版本：v5.0（界面标识 "Pro Studio v5.0"）
- 技术栈：WPF + C# 5.0（.NET Framework 4.x），无第三方 UI 库，XAML 以嵌入资源加载
- 编译：build.ps1 调用系统 csc.exe 单文件编译（/warnaserror+，有警告即失败）
- 产物：
  - D:\app\90HX Studio.exe
  - D:\app\HX家族多功能工具箱.exe
  - 桌面同步：C:\Users\19227\Desktop\ 下的同名两个文件
- 体积约 48.6 MB（assets.zip 等资源内嵌）
- 程序清单要求管理员权限（app.manifest），非提权启动会被系统拒绝

构建命令：

powershell.exe -ExecutionPolicy Bypass -File "D:\app\build_source\build.ps1"

日志位置：

- C:\ProgramData\CMP90HX_Toolbox\logs\crash.log（未处理异常）
- C:\ProgramData\CMP90HX_Toolbox\logs\startup.log（启动阶段诊断，临时加入）
- C:\ProgramData\CMP90HX_Toolbox\logs\ai_assistant.log（AI 异常）

---

## 二、界面版本说明（务必先看）

用户认可的布局基线是：

D:\app\build_source\src\MainWindow.xaml.bak_before_studio_merge

特征：顶部 42px 标题栏（红黄绿三点 + HX 深色徽标 + 标题 + Pro Studio v5.0 胶囊 + 右侧温控胶囊）；左侧 220px 分组侧栏；默认页"硬件信息"含三张信息卡、详细信息表、右侧"链路与覆盖状态 / 核心传感器·实时 / 快捷动作"；底部有"呼出 AI 硬件助手"。

不要再用 MainWindow.xaml.studio_base（那是"驾驶舱遥测"布局，用户已否决）。

备份对照：

- MainWindow.xaml.bak_before_studio_merge：当前目标基线
- MainWindow.xaml.bak_agent1：12 页功能内容最完整，可作页面内容参考
- MainWindow.xaml.bak_apple_studio：更早的 Apple Studio 模板，侧栏与顶栏不同
- MainWindow.xaml.studio_base：驾驶舱布局，已否决

---

## 三、已完成的工作

### 1. 窗口与性能

- AllowsTransparency 设为 False，并启用 WindowChrome（CaptionHeight 42），恢复 Direct3D 硬件加速，解决此前的界面卡顿
- 顶栏 TopDragBar、侧栏 SidebarBorder 均挂接了 DragMove()，可自由拖动
- 最小尺寸 1160×660

### 2. 启动与稳定性

- 修复了旧版因 XAML 命名重复导致的 XamlParseException
- 修复 RefreshSystemStatus 中未判空控件导致的 NullReferenceException（整段 try/catch + 逐控件判空）
- 单实例互斥体：Global\CMP90HX_Toolbox_SingleInstance_App_Mutex
- 启动动画 StartupAnimWindow 保留，用户明确要求不要改动

### 3. 动态硬件数据（本次重点）

原先写死的数据已改为从系统/驱动读取：

- NativeCore.GpuSensorSnapshot 扩展为 18 个字段：实时功耗、当前功耗墙、power.min_limit、power.default_limit、power.max_limit、显存已用/总量、利用率、当前/最大核心频率、当前/最大显存频率、P-State、PCIe 代数与宽度
- 新增 NativeCore.QueryGpuPowerRange()：返回当前卡的真实功耗上下限
- 新增 NativeCore.QueryGpuTempThresholds()：从 nvidia-smi -q -d TEMPERATURE 读取降频/关机温度（本机 90HX 实测：降频 95℃，关机 98℃）
- NativeCore.EffectiveIdlePowerLimit() 优先采用驱动的真实最低功耗墙，不再套用其他型号数值
- App.RefreshSensors() 中温度环、功耗环、进度条量程全部使用实时值，移除 250W / 10240MB / 90℃ 的写死兜底
- SystemDetector.MachineHardwareProfile 构造函数中原先的示例值（ASUS 主板、致态硬盘、180Hz 屏等）改为"检测中..."，实际值由 DetectPeripherals 覆盖

本机验证命令：

nvidia-smi --query-gpu=name,temperature.gpu,power.draw,power.limit,power.min_limit,power.default_limit,power.max_limit,memory.used,memory.total --format=csv

示例输出：NVIDIA CMP 90HX, 77, 297.07, 300.00, 0.00, 250.00, 1048.57, 228, 10240

### 4. 显卡型号伪装

- 新增 NativeCore.GetSafeDefaultSpoofTarget(detectedModel, chipArchitecture)：90HX/GA102→RTX 3080，70HX/GA104→RTX 3070，50HX/TU102→RTX 2080 Ti，40HX/TU106→RTX 2060 SUPER，30HX/TU116→GTX 1660 SUPER，170HX/GA100→A100-PCIE-40GB
- 新增 NativeCore.NormalizeGpuDisplayName() 统一 SUPER/Ti 写法
- "一键伪装当前卡"改为使用下拉中选择的目标型号（不再固定 3080/5090）
- "一键全机伪装"改为逐卡按安全目标伪装，不再强制走引导部署
- 伪装页 UI 中写死的显卡名、架构、PCI ID 已替换为"检测中..."，运行时填充真实值

### 5. AI 助手

- 品牌标签由"AI 伴侣"改为"硬件 Agent"
- 默认 System Prompt 改为"可执行硬件 Agent"人格
- 新增 build_source\src\AiAgentActionRegistry.cs：
  - AiAgentAction 动作模型（Id / Title / Description / IsDestructive / Executor）
  - Match(userText) 按关键词匹配可执行动作，覆盖：显卡伪装、显卡解锁、游戏覆盖解锁、首选高性能卡、驱动热重启、系统级省电
  - BuildRealtimeFacts() 生成实时硬件事实并注入模型上下文
- AI 回复下方会出现"执行此操作"按钮，点击后真实调用底层功能
- Markdown 星号问题：新增 StripMarkdownArtifacts() 与 RenderRichText()，助手气泡改为 Inlines 富文本渲染；System Prompt 追加"不要使用 Markdown 星号/井号/反引号"
- 滚动体验：新增 SlimScrollBar / SlimScrollThumb / SmoothChatScroll 样式（4px 细圆角）；聊天区滚轮每次约 46px；MainWindow.xaml 也加入了全局细滚动条样式

### 6. 其他

- 覆盖与 DLSS 页面内容完整保留（PageOverlay 与 PageDlss 并存）
- 赞助页二维码容器放大至 316×316
- AI 网关：https://api.muyaa.top/hx-assistant/v1，模型 gemini-3.8-flash，客户端头 X-HX-Code: HX_STUDIO_PUBLIC_2026

---

## 四、未完成 / 待办

### P0 — AI 助手"真正干活"尚未完成

当前仅实现"识别意图 → 弹出执行按钮 → 用户点击后执行"。距离用户要的桌面 Agent 还差：

1. 多步任务链：例如"解锁 90HX 并伪装成 3080"目前只匹配第一个动作，需支持顺序执行
2. 失败后的自主排障：动作失败只把错误文本交给模型，没有结构化回传日志、设备错误码（Code 43 等）并继续尝试替代方案
3. 执行前能力校验：未检查管理员权限、PCIe 通道、Secure Boot 等前置条件
4. 模型侧工具调用：目前是本地关键词匹配，没有把动作列表作为 tool schema 传给模型，复杂中文表达可能匹配不到
5. 执行日志展示：AddActionCard 中的 logger 目前被丢弃，用户看不到执行细节
6. GenerateLocalHardwareKnowledge() 离线知识库仍含写死建议值（如"建议限制为 240W"、"1800~1920MHz"），需改为引用实时数据

### P0 — 导航结构改造（用户明确要求）

把 90HX / 50HX / 40HX 三个解锁入口合并为一级菜单 + 二级子菜单并做图标化。当前仍是三个平铺 RadioButton：NavItemMatrix / NavItemUnlock50 / NavItemUnlock40。

注意：App.cs 中 _navItems 与 _pageHosts 按下标强对应。改导航结构必须同步修改这两个数组与 SwitchPage() 映射，否则会串页（当前各 12 项）。

需要做：

- 一级项"固件解锁"，展开含 90HX / 50HX / 40HX 三个子项
- 双卡协同页内容过长，改为分段/下拉折叠
- "DLSS 3 与覆盖"改名为"DLSS 5 帧生成"

### P1 — 软件更新流程

- 更新检测需要动画反馈
- 检测到新版本后自动跳转到"软件更新"页面
- 更新页 UI 需要重做

相关：App.StartAutoUpdateCheck()（启动后 4 秒检测一次）、PageUpdate（MainWindow.xaml 约 2389 行起）

### P1 — 覆盖与 DLSS 页面验收

用户反馈"打开是白的"。换成 bak_before_studio_merge 后该页内容完整，但尚未实际点击验收：PageOverlay（游戏内覆盖）、PageDlss（DLSS/帧生成）。

这两页历史合并时出现过重复命名：TxtNvAppPath、TxtOverlayPatchState、TxtOverlayBakState、BtnUnlockOverlay、BtnRestoreOverlay、BtnCleanCefAndRestart。改动时务必保持唯一。

### P1 — UI 细节

- 部分页面视觉粗糙，需逐页打磨
- 用户提到要移除"顶部垃圾/底部垃圾"，但随后又要求保留目标版布局，需要再确认具体指哪一块
- 启动动画保留
- 赞助页文案与二维码需最终确认

### P2 — 清理项

- build_source\src\_pages.json
- build_source\src\_page_dlss_block.tmp
- build_source\src\_page_dlss_body.tmp
- build_source\RenderOnePage.cs
- build_source\_initnav_old.txt
- D:\app\stderr.txt
- D:\app\stdout.txt

App.cs 中为排查启动问题加入的 LogStartup() 调用，确认稳定后可移除。

---

## 五、已知坑

1. 管理员权限：exe 清单要求提权，Start-Process 直接启动会报"请求的操作需要提升"，这不是程序 bug。测试请用：
   $psi = New-Object System.Diagnostics.ProcessStartInfo
   $psi.FileName = "D:\app\HX家族多功能工具箱.exe"
   $psi.UseShellExecute = $true
   $psi.Verb = "runas"
   [System.Diagnostics.Process]::Start($psi)

2. 单实例：已有实例运行时，第二次启动会激活旧窗口后退出，表现为"启动即退出"。测试前先结束所有 HX家族多功能工具箱 进程。

3. XAML 命名唯一：XamlReader.Load 对同名 Name 极敏感，重复即整个窗口加载失败。修改后建议扫描重复 Name。

4. 数组下标耦合：_navItems、_pageHosts 必须一一对应且等长。

5. C# 5.0 限制：不能用字符串插值 $"..."、不能用 nameof、不能用现代语法糖。

6. 编译警告即错误：/warnaserror+。

7. build.ps1 在输出文件被占用时，Copy-Item 会失败但仍打印"已同步"，需给 Copy-Item 加错误检查。

---

## 六、关键文件索引

- build_source\build.ps1：唯一构建入口
- build_source\src\MainWindow.xaml：主窗口布局（12 个功能页）
- build_source\src\App.cs：初始化、事件绑定、页面切换、传感器刷新
- build_source\src\NativeCore.cs：注册表、BCD、nvidia-smi、省电、伪装、更新
- build_source\src\SystemDetector.cs：硬件/系统探测
- build_source\src\GpuHardwareManager.cs：显卡枚举与驱动热重载
- build_source\src\GpuSpoofManager.cs：型号伪装
- build_source\src\PcieUnlockManager.cs：PCIe/引导解锁
- build_source\src\EcoPowerManager.cs：省电服务
- build_source\src\EcosystemPatchManager.cs：NVIDIA App 覆盖 / DLSS 补丁
- build_source\src\AiAssistantEngine.cs：AI 后端
- build_source\src\AiAssistantWindow.xaml / .cs：AI 悬浮助手
- build_source\src\AiAgentActionRegistry.cs：新增，AI 可执行动作注册表
- build_source\src\VirusOverlay.cs：启动动画（保留，勿改）

---

## 七、建议推进顺序

1. 导航二级菜单改造（同步改 _navItems / _pageHosts）
2. 补完 AI 多步执行与执行日志展示
3. 逐页点击验收，修复空白页
4. 重做软件更新页与检测动画
5. 清理临时文件与诊断代码
6. 全量回归（改名、解锁、省电、AI 问答、更新）
