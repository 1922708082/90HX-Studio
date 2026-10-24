# HX 家族多功能工具箱 · 交接文档（v6.2）

> 交接日期：2026-10-24
> 对应版本：v6.2.0（Release 已发布）
> 仓库：https://github.com/1922708082/90HX-Studio
> 本文档面向"接手继续开发的人"，读完应该能独立改代码、编译、发版。

---

## 一、一页纸速览

| 项目 | 内容 |
|---|---|
| 是什么 | NVIDIA CMP 全系矿卡（30HX/40HX/50HX/70HX/90HX/170HX）的 Windows 桌面工作台 |
| 干什么 | 固件解锁、型号伪装、双卡绑定、动态省电、快照回滚、恢复出厂、游戏覆盖 |
| 技术栈 | C# / .NET Framework 4.x（csc 直接编译）+ WPF 外壳 + WebView2 承载 HTML 界面 |
| 代码规模 | C# 约 22,600 行（21 个文件）；前端 app.js 3,961 行 + CSS 9,466 行（5 个文件） |
| 产物 | 单个 50MB exe，**无安装包、无同目录依赖** |
| 界面入口 | `index.html` 以资源形式内嵌，运行时通过 `https://hx.local/index.html` 加载 |
| 数据目录 | `C:\ProgramData\CMP90HX_Toolbox\`（settings.ini / logs / toolkit / webview2_udf） |
| 权限 | 清单要求 `requireAdministrator`，必须管理员运行 |
| 发布方式 | GitHub Release，附件 `HX-Toolbox-v{版本}.exe` |

**一句话理解架构**：一个 C# 单体进程，前面挂 WebView2 显示 HTML，前后端用 postMessage 传 JSON 信封；后端做所有硬件/注册表/引导操作，前端只负责渲染和发命令。

---

## 二、代码地图

```
D:\app\
├─ build_source\
│  ├─ build.ps1                 ← 唯一构建入口（合并前端 + 编译）
│  ├─ scripts\audit_contract.py ← 契约审计（前端会发但后端没实现的命令）
│  ├─ src\                      ← 全部 C# 源码
│  ├─ web\                      ← 前端源码（不是产物）
│  │  ├─ index.template.html    ← 外壳模板，含 {{CSS}} / {{PAGES}} / {{JS}} 占位
│  │  ├─ pages\                 ← 10 个页面片段
│  │  ├─ css\                   ← 01-base / 02-layout / 03-components / 04-pages / 05-overrides
│  │  ├─ js\app.js              ← 全部前端逻辑（3961 行，单文件）
│  │  └─ index.html             ← 【构建产物，不要手改】
│  ├─ assets\                   ← 图标、二维码
│  ├─ lib\webview2\             ← WebView2 托管 dll + Loader（内嵌进 exe）
│  └─ assets.zip                ← 工具包/EFI 载荷/DLSS 运行库（47.5MB，内嵌）
├─ tools\sign-exe.ps1           ← 代码签名脚本
├─ screenshots\                 ← README 配图
├─ 90HX Studio.exe              ← 构建产物（D:\app 根目录，也在桌面）
├─ HX家族多功能工具箱.exe        ← 同一个文件的中文名副本
└─ README.md / HANDOVER*.md
```

### C# 文件职责（按行数排序）

| 文件 | 行数 | 职责 |
|---|---|---|
| `NativeCore.cs` | 8,972 | 硬件操作总仓库：PCIe 解锁、注册表、NVAPI、BCD、显卡重载、DLSS 部署 |
| `App.cs` | 4,212 | WPF 外壳：窗口、闪屏、托盘、旧版界面、提权重启 |
| `WebShell.cs` | 2,205 | **WebView2 宿主 + 命令分发中心**，改功能最先看这里 |
| `VirusOverlay.cs` | 1,097 | 游戏覆盖补丁 |
| `UpdateInstaller.cs` | 982 | 更新包下载、sha256/PE 校验、替换自身 |
| `UpdateChecker.cs` | 829 | 版本检查：GitHub Release / 自建清单 / 限流降级 |
| `Diagnostics.cs` | 753 | 诊断与快照（创建/回滚/删除） |
| `HardwareBridge.cs` | 671 | 事件推送给前端（evt 信封） + 口令/版本 |
| `DlssPack.cs` | 615 | DLSS 解锁包下载（多镜像） |
| `SystemDetector.cs` / `AppSettings.cs` / `AssemblyInfo.cs` | 其余 | 硬件探测 / 配置读写 / 版本号 |

### 关键概念

- **action（命令）**：前端 → 后端，如 `dual.bind`。在 `WebShell.cs` 的 `if (action == "...")` 链里处理，共 36 个。
- **evt（事件）**：后端 → 前端，如 `device.snapshot`、`update.state`。统一走 `HardwareBridge.*Evt()` 构造。
- **组锁**：`TryEnterGroup(action, id)`，引导类操作共用 `_lockPcie`，防止并发写 BCD。
- **结果三件套**：`ReplyOk` / `ReplyFail(code, msg)` / `ReplyDataJson(json)`，会自动释放组锁。

---

## 三、构建与发布

### 构建

```powershell
cd D:\app\build_source
powershell -ExecutionPolicy Bypass -File build.ps1
```

build.ps1 做四件事：

1. 合并前端：把 5 个 CSS、10 个页面片段、app.js 填进 `index.template.html` → 生成 `web/index.html`
2. 跑契约审计 `audit_contract.py`：前端发了但后端没实现的命令必须为 0（会打印明细）
3. 用 `csc.exe` 编译（`/warnaserror+`，警告即错误）
4. 输出 `D:\app\90HX Studio.exe`，复制成中文名，再同步一份到桌面

**版本号在 `src/AssemblyInfo.cs:12-13`**（`6.2.0.0`），改版本只改这里。

### 发布流程（下次发新版照做）

1. 改 `AssemblyInfo.cs` 版本号
2. `build.ps1` 编译
3. 提交推送：`git add -A && git commit && git push`
4. 建 Release：tag 用 `v6.3.0`，标题写版本，正文写更新日志
5. 上传附件，命名必须是 `HX-Toolbox-v6.3.0.exe`
6. 软件内的"检查更新"会自动读 `releases/latest`，**不需要额外维护 version.json**

> 附件命名不用严格匹配 tag，但保持一致便于人看。软件是按"最新 release 的 assets 里第一个 .exe"取的。

### 代码签名

`tools/sign-exe.ps1` 可用本机证书签名。当前 Release **未做商业证书签名**，用户首次运行可能看到 SmartScreen 提示。要彻底消除需 CA 签发的 EV 证书（参考同类软件用的是 Certum EV）。

---

## 四、前端架构

### 合并机制（最容易踩坑的地方）

`index.template.html` 里有三个占位符：`{{CSS}}`、`{{PAGES}}`、`{{JS}}`，由 build.ps1 字符串替换。

**坑**：`{{PAGES}}` 曾经被写在 HTML 注释里，而页面片段自己带 `<!-- PAGE: xxx -->`，导致外层注释提前闭合，每页底部多出一行正文「构建时按 pages/ 顺序注入 -->」。现在占位符放在注释外，旁边有注释说明，不要挪回去。

### 事件协议

前端 `sendCmd(action, payload, target)` 发命令；后端回 `res` 信封；后端主动推 `evt` 信封。
所有信封带 `id`，前端按 id 匹配请求，迟到的回执不会重复追加（这个 bug 修过，别再改回去）。

### 页面与导航

10 个页面：`device`（概览）、`unlock`、`spoof`、`power`、`dlss`、`dualgpu`、`maintenance`、`logs`、`overlay`、`update`。

导航点击 `data-action="nav.switch"` + `data-page="xxx"`，`case 'nav.switch'` 里切 `.page.active`。进入 DLSS 页会弹一次内测提示，别删。

### 状态机

每个页面有 `xxxStatus: loading | ready | error | empty` 和 `xxxError`。渲染函数根据状态出骨架屏/内容/错误横幅。错误横幅支持 `×` 关闭和 12 秒自动收起，收起时只清仍是 error 的状态（迟到的定时器不会破坏已恢复的页面）。

### CSS 分层

| 文件 | 用途 |
|---|---|
| `01-base.css` | 变量、字体、重置 |
| `02-layout.css` | 顶栏/侧栏/页面骨架 |
| `03-components.css` | 按钮、卡片、表格、标签 |
| `04-pages.css` | 单页特有样式 + 亮色主题补丁 |
| `05-overrides.css` | 补丁层，**新改动优先写这里**，别去改前四层 |

规则：**禁止 `!important`**（现有几处是历史遗留，不要再加）；颜色必须用变量，不写死色值；不改宽高内边距去"凑"效果。

---

## 五、后端架构

### 命令分发（WebShell.cs）

```
WebMessageReceived
  → 解析信封 {id, action, payload}
  → if (action == "xxx") { ... }
  → ReplyOk / ReplyFail / ReplyDataJson
```

长任务一律 `Task.Run`，绝不能在 UI 线程做（历史事故：同步下载直接闪退）。

### 组锁

```csharp
if (!TryEnterGroup(action, id)) { ReplyFail(id, action, "BUSY", BusyMessage(action)); return; }
```

`factory.reset` / `unlock.install` / `unlock.rollback` 同组（都写引导），互斥。

### 硬件层（NativeCore.cs）

- 注册表：显卡 `DriverDesc`、`AdapterType`、`EnableMsHybrid`
- 引导：`bcdedit` 备份/还原、ESP 挂载写 EFI 载荷
- 显卡：PCIe 解锁、设备禁用启用、NVAPI 调功耗/频率
- 省电：P-State 压到 P8，注册 SYSTEM 计划任务

### 快照（Diagnostics.cs）

- 格式：文本文件，`schema 2` 才有 `[ROLLBACK]` 段（7 个可回滚键值）
- 旧快照仍可列出，但**回滚会明确拒绝**，这是刻意的，不要改成"尽力而为"

### 更新（UpdateChecker + UpdateInstaller）

三条链路，按顺序尝试：

1. GitHub API `repos/{owner}/{repo}/releases/latest`（正常）
2. API 403/429 → 页面降级：`/releases/latest` 跟随 302 取 tag → 从 `expanded_assets` 片段取附件直链
3. 都没有 → 兼容旧的 `version.json`

安全策略：有官方 digest 就强制 sha256；没有摘要时**只允许 github.com 直链** + PE 头 + 体积校验，界面显示「已下载 · 未校验哈希」，不得标成「校验通过」。

---

## 六、十条必须知道的坑

1. **`web/index.html` 是产物**，改它没用，下次构建会被覆盖。要改 `pages/` 或 `template`。
2. **应用运行时不能编译**，exe 被占用会报 CS0016。先关程序。
3. **PowerShell 脚本必须带 UTF-8 BOM**，否则 Windows PowerShell 5.1 下中文乱码导致解析失败。
4. **`AppSettings.Load()` 有 `_loaded` 缓存**，运行期改 `settings.ini` 不重读。需要最新值用 `Reload()`。
5. **`settings.save` 只写提交的字段**。以前 null 一律当空串写回，导致保存下载源时把清单地址清空——这个问题修过，改这个 case 时注意别回退。
6. **GitHub API 在共享出口 IP 上会 403**（60 次/小时/IP）。降级链路是必须的，别删。
7. **合并远程分支要小心**：远程 main 曾经是 v5.0 的老提交，合并带进来一行调用本地不存在的类，直接 CS0103。合并后必须编译验证。
8. **前端 `escapeHtml` 必须用于所有用户路径**（双卡绑定列表的 `data-exe-path` 就是），否则路径里的引号会破坏 DOM。
9. **写引导的操作必须进组锁**，否则并发会写坏 BCD。
10. **不要用假成功**：这个项目的历史 bug 大半是"失败了仍回成功"。新增功能一律"回读校验后再回成功"。

---

## 七、已知限制与技术债

### 功能限制（写进 README 了，别再宣传成支持）

- 固件解锁只有 **90HX / 40HX** 可用，50HX 有入口但标注"暂未验证"
- 70HX / 30HX / 170HX **没有解锁页**，只有识别、伪装、省电
- 旧快照不能回滚（只能新建快照后再回滚）

### 技术债

| 项 | 影响 | 建议 |
|---|---|---|
| 单文件 `app.js` 3,961 行 | 难维护 | 按页面拆模块（需改构建合并逻辑） |
| `NativeCore.cs` 8,972 行 | 同上 | 按域拆分（注册表/引导/显卡/省电） |
| 10 处硬编码 `D:\app\...` 兜底路径 | 目标机恰好有同名目录时会误读 | 改为统一数据目录 |
| 84 个 CSS 类零引用 | 死代码 | 可用脚本批量清理，但要逐条核对 3D 视图 |
| `ExtractPayloadString` 不做 JSON 反转义 | 路径类 payload 靠 Win32 规范化侥幸可用 | 全局修（目前只有 `dual.*` 内部反转义） |
| 无自动化测试 | 全靠手测 | 至少给 `UpdateChecker` 的解析加单元测试 |

### 风险提示

- 应用以管理员运行，能改引导和注册表，测试时**不要在自己主力机上随便点恢复出厂**
- 改 `DriverDesc` 后如果驱动认不到，需要在设备管理器禁用/启用显卡
- 解锁后必须**完全关机断电 10 秒**，普通重启无效（快速启动会跳过引导）

---

## 八、改完必测清单

改动后至少跑一遍：

```
[ ] build.ps1 通过（契约审计 0 缺口、无编译警告）
[ ] 双击 exe 能起界面（不是白屏）
[ ] 侧边栏 10 个页面都能切
[ ] 日志页能加载、能筛选、能复制
[ ] 更新页点「检查更新」有返回（当前版本 6.2.0 会显示已最新）
[ ] 亮色/暗色主题切换后，边框和文字都可见
[ ] 双卡页能绑定/解绑（改注册表，小心）
[ ] 维护页能创建快照（不点恢复出厂）
```

硬件相关操作（解锁/回滚/恢复出厂）**只在测试机上做**。

---

## 九、交接清单

### 需要交接的东西

- [ ] GitHub 仓库权限（当前 owner：`1922708082`）
- [ ] 本机 `D:\app` 目录（含 `assets.zip`、webview2 dll、构建脚本）
- [ ] 代码签名证书（目前是自签名 `CN=CMP90HX Unlock Signing`，指纹 `C6813F4D…`，2036 到期）
- [ ] 一张 CMP 卡用于验证解锁流程（没有卡就只能改 UI/逻辑，不能验硬件路径）

### 环境要求

| 依赖 | 说明 |
|---|---|
| .NET Framework 4.x | csc 编译器随系统，`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` |
| Node.js | 只用于前端语法检查 `node --check app.js`（可选） |
| WebView2 Runtime | 运行时依赖，Win11 自带 |
| PowerShell 5.1 | 跑构建脚本 |

### 接手后建议的第一步

1. 跑一次 `build.ps1`，确认能出包
2. 空目录里只放 exe，启动一次（验证资源确实全内嵌）
3. 打开 `WebShell.cs` 看 `update.check` 这一段，理解前后端协议
4. 改一个不影响硬件的小地方（比如文案），走一遍完整发布流程

---

## 十、快速定位表

| 想改什么 | 去哪里 |
|---|---|
| 加一个新功能按钮 | `pages/*.html` + `app.js` 的 `switch(action)` + `WebShell.cs` 的 `if (action == ...)` |
| 改界面样式 | `web/css/05-overrides.css`（优先） |
| 改某个页面的数据展示 | `app.js` 里对应 `renderXxxPage()` |
| 改硬件操作逻辑 | `NativeCore.cs` |
| 改更新检查 | `UpdateChecker.cs` + `UpdateInstaller.cs` |
| 改版本号 | `src/AssemblyInfo.cs` |
| 改打包内容 | `build.ps1` 的 `/resource:` 列表 |
| 加一个内嵌资源 | 放 `assets/`，在 build.ps1 加 `/resource:`，代码里用 `GetManifestResourceStream` |

---

有任何拿不准的地方，先看 `git log`——这个仓库的提交信息写得比较细，每个改动都写了"为什么"，比代码本身更能说明问题。
