# HX Studio 6.0 · 前端交付规范（任务书）

> 版本：1.0　日期：2026-10-20
> 交付目标：把现有的 `hx_studio_6_0.html` 原型，扩展成可被 Windows 宿主程序直接接管 UI 的**完整生产版前端**
> 阅读顺序：第 1-2 章是约束（先看），第 3-5 章是你要产出的东西，第 6-8 章是验收

---

## 0. 一句话说明你要做什么

> 现在这个 HTML 是一个"能点动的视觉原型"。你要把它变成 **10 个功能页、所有数据由宿主注入、所有操作由宿主执行** 的**真实界面层**。
>
> 判断标准：**这个文件里不能再有任何一行写死的硬件数字，也不能再有一个函数是自己模拟出来的动画结果。**

---

## 1. 运行环境（决定了所有技术选型）

| 项 | 值 |
| :--- | :--- |
| 宿主 | Windows 桌面程序（WPF），**全窗口无边框**，不显示系统标题栏 |
| 渲染引擎 | Microsoft Edge **WebView2**（Chromium 内核，桌面端 / Edge 内核版本，不得依赖具体版本特性） |
| 窗口尺寸 | 最小 **1160 × 660**，默认 **1280 × 800**，用户可自由缩放 |
| 缩放 | 100% / 125% / 150% DPI 均需正常 |
| 网络 | **完全离线**。页面加载后不允许任何网络请求 |
| 文件形态 | **单个 .html 文件**，双击即可在浏览器打开预览 |
| 构建工具 | **无**。不引入 npm / webpack / vite / Tailwind CDN / React / Vue / TypeScript 编译 |
| 允许的库 | **零第三方库**（现有原型做得好，继续保持纯手写） |
| 禁止的 API | `localStorage` / `sessionStorage` / `fetch` / `XMLHttpRequest` / `IndexedDB` / `alert` / `confirm` / `prompt` / `window.open` |

> 状态持久化（主题、上次所在页）**由宿主负责**，页面启动时通过消息接收，不要自己存。

---

## 2. 硬性规则（违反任何一条即退回重做）

1. **禁止 emoji 作功能图标**。所有图标必须是内联 `<svg>`，统一 1.8px 描边、`stroke-linecap: round`。你现在这套做得对，保持，不要为了省事换成字符。
2. **禁止任何外链资源**：`<img src="http...">`、`@import`、CDN、webfont、远程图片。全部用 SVG / CSS 渐变 / CSS 绘制。
3. **禁止写死任何硬件数据**：型号名、驱动版本、PCI ID、显存容量、版本号、计数、"已启用"状态。全部改为空占位，等宿主填。
4. **禁止紫→粉渐变作主视觉**。现行青蓝体系（`--accent: #38bdf8` / 亮色 `#0284c7`）是正确方向，不要改动主色。
5. **颜色必须走 CSS 变量**。你现在有 `--accent` / `--card-bg` 等，继续保持；新增颜色一律加变量，不要出现裸色值（`#xxx` 直接写在属性里）。
6. **禁止用 `onclick="simulateXxx()"` 直连假函数**。所有交互改为 `data-action` + 统一事件委托（见第 4 章）。
7. **禁止 fake green（假绿灯）**。任何"已启用 / 已就绪 / 已解锁"状态，必须来自宿主数据；没有数据时显示"未知"，不要默认显示成功色。
8. **禁止自创字段名**。第 5 章的数据契约里，字段名一个字母都不能改；你可以选择不显示某个字段，但不能改名、不能自己加。

---

## 3. 页面清单（6 页 → 10 页）

保留原有 6 页的视觉，补齐 4 个缺失页，并扩展现有页。**侧栏导航按下面顺序排列。**

| # | page id | 侧栏名称 | 状态 | 核心内容 |
| :-- | :--- | :--- | :--- | :--- |
| 1 | `device` | 硬件总览 | 改造 | 3D 显卡模型、显卡信息 tags、传感器仪表、硬件明细表 |
| 2 | `unlock` | 固件解锁 | 扩展 | 卡型切换（90HX/50HX/40HX）× 通道切换（A/B/C/D）、引导链拓扑、执行与回滚 |
| 3 | `spoof` | 显卡伪装 | 扩展 | 型号下拉（**动态注入，可 76 项**）、当前卡信息、伪装预览、还原 |
| 4 | `power` | 动态节能 | 改造 | 反应堆动画、三档预设、实时功耗波形、守护进程状态 |
| 5 | `dlss` | DLSS 5 / 帧生成 | 改造 | 运行库版本、镜像选择、部署前置检查、参数调优、部署/移除、进度 |
| 6 | `overlay` | **游戏覆盖** | **新增** | NVIDIA App 覆盖状态、Alt+R / Alt+Z、补丁状态、缓存清理、一键还原 |
| 7 | `dualgpu` | **双卡协同** | **新增** | 渲染卡/显示卡拓扑、程序绑定列表、常见游戏扫描、HAGS 状态 |
| 8 | `maintenance` | 系统维护 | 改造 | 快照时间线、创建快照、回滚、出厂重置（危险区） |
| 9 | `logs` | **审计日志** | **新增** | 日志流、级别过滤、搜索、导出、清空 |
| 10 | `update` | **软件更新** | **新增** | 版本对比、更新说明、下载进度、校验状态、应用更新 |

**关于第 2 页（重点）**：原型的"固件解锁"只画了 90HX 一条链。实际有 **3 种卡型 × 4 条引导通道**：

- 卡型：`CMP 90HX`（GA102）、`CMP 50HX`（TU102）、`CMP 40HX`（TU106）
- 通道：A = UEFI 直启（`NVPermissiveEFI.efi` 写入 ESP）；B = UEFI Shell 链启（老主板）；C = U 盘引导（全新建）；D = 恢复官方引导
- 通道 B / C 需要额外参数：目标 U 盘盘符（下拉，由宿主提供列表）
- 引导链拓扑图要能体现"当前处于哪条通道"，未安装时节点显示灰色未激活态

**关于第 3 页**：型号列表必须由 `evt: spoof.models` 消息注入，**不要在 HTML 里写 `<option>`**。列表结构见第 5.6 节，会按芯片架构分组（GA102 / GA104 / TU102 / TU106 / TU116 / GA100）。

**关于第 8 页**：原型的"共 2 个可还原点"和两条快照时间线是假的。真实数据由 `evt: maintenance.snapshots` 提供，可能是 0 个、也可能是 20 个，**必须处理好空态**。

---

## 4. 桥接协议（最重要，必须一字不差）

### 4.1 通道

- **页面 → 宿主**：`window.chrome.webview.postMessage(object)`
- **宿主 → 页面**：`window.chrome.webview.addEventListener('message', e => handle(e.data))`
- 在普通浏览器里预览时，`window.chrome.webview` 不存在 —— 必须做存在性判断，否则页面会直接报错白屏。提供 `mock` 分支让浏览器预览可用（见 4.6）。

### 4.2 统一信封

所有消息都是 JSON 对象，统一 4 个字段：

```json
{
  "kind": "cmd" | "res" | "evt" | "prg",
  "id": "唯一字符串（cmd/res 必填，evt/prg 可空）",
  "action": "点分命名，如 spoof.apply",
  "payload": { }
}
```

| kind | 方向 | 用途 | 是否配对 |
| :--- | :--- | :--- | :--- |
| `cmd` | 页面 → 宿主 | 发起一个操作 | 有 `id`，等 `res` |
| `res` | 宿主 → 页面 | 操作结果 | 带同一个 `id` |
| `evt` | 宿主 → 页面 | 主动推送状态（无需请求） | 无 |
| `prg` | 宿主 → 页面 | 长任务进度 | 带 `id` |

### 4.3 响应格式

```json
{
  "kind": "res",
  "id": "c-0017",
  "action": "spoof.apply",
  "payload": {
    "ok": true,
    "data": { },
    "error": null
  }
}
```

失败时：

```json
{
  "kind": "res",
  "id": "c-0017",
  "action": "spoof.apply",
  "payload": {
    "ok": false,
    "data": null,
    "error": { "code": "GPU_NOT_FOUND", "message": "未检测到目标显卡" }
  }
}
```

**错误码必须原样展示给用户**（可以翻译成中文，但不能吞掉）。宿主会返回这些 code：`NO_ADMIN` / `GPU_NOT_FOUND` / `DRIVER_REJECT` / `FILE_LOCKED` / `NETWORK` / `CHECKSUM_MISMATCH` / `NEED_REBOOT` / `NOT_SUPPORTED` / `INTERNAL`。

### 4.4 进度

```json
{ "kind": "prg", "id": "c-0017", "payload": { "percent": 42, "stage": "下载中", "detail": "12.4 MB / 29.5 MB" } }
```

`percent` 为 `-1` 表示不确定进度（用循环动画，不要显示百分比）。

### 4.5 完整 action 清单

**页面 → 宿主（cmd）**

| action | payload | 说明 |
| :--- | :--- | :--- |
| `app.init` | `{}` | 页面加载完成后第一件事，宿主收到后回推全部初始状态 |
| `device.refresh` | `{}` | 强制刷新硬件与传感器 |
| `unlock.install` | `{ cardType, channel, usbDrive? }` | 执行引导注入；`cardType` ∈ `90HX/50HX/40HX`，`channel` ∈ `A/B/C/D` |
| `unlock.rollback` | `{ cardType }` | 恢复官方引导 |
| `unlock.scan` | `{}` | 重新检测当前引导状态 |
| `spoof.apply` | `{ gpuSlot, targetName }` | 伪装；`gpuSlot` 为整数（从 0 起） |
| `spoof.restore` | `{ gpuSlot }` | 还原出厂名 |
| `power.enable` | `{ idleSeconds }` | 开启系统级省电守护 |
| `power.disable` | `{}` | 关闭守护 |
| `power.setPreset` | `{ preset }` | `preset` ∈ `sleep/adaptive/turbo`（仅调整策略，不直接下发） |
| `dlss.preflight` | `{ gameDir, proxyName, injectReFramework }` | 部署前置检查 |
| `dlss.deploy` | `{ gameDir, proxyName, injectReFramework, options }` | 部署 |
| `dlss.remove` | `{ gameDir }` | 移除补丁 |
| `dlss.checkUpdate` | `{ mirrorIndex }` | 检查运行库更新 |
| `overlay.unlock` | `{}` | 解除 NVIDIA App 覆盖白名单 |
| `overlay.restore` | `{}` | 还原 |
| `overlay.cleanCache` | `{}` | 清理 CefCache |
| `dual.bind` | `{ exePath }` | 绑定程序到高性能卡 |
| `dual.unbind` | `{ exePath }` | 解除绑定 |
| `dual.scanGames` | `{}` | 自动扫描常见游戏 |
| `snapshot.create` | `{ label }` | 创建快照 |
| `snapshot.restore` | `{ snapshotId }` | 回滚到快照 |
| `snapshot.delete` | `{ snapshotId }` | 删除快照 |
| `factory.reset` | `{ confirmText }` | 出厂重置（`confirmText` 必须是用户手输的确认字串） |
| `logs.query` | `{ offset, limit, level }` | 分页取日志 |
| `logs.export` | `{}` | 导出日志（宿主弹保存框） |
| `logs.clear` | `{}` | 清空日志 |
| `update.check` | `{}` | 检查更新 |
| `update.download` | `{}` | 下载 |
| `update.apply` | `{}` | 应用并重启 |
| `dialog.browse` | `{ kind }` | `kind` ∈ `dir/file`，宿主弹系统选择框并回传路径 |
| `theme.set` | `{ theme }` | `theme` ∈ `dark/light` |
| `window.min` / `window.max` / `window.close` | `{}` | 窗口控制 |
| `app.ready` | `{ ms }` | 首屏渲染完成耗时，宿主用于统计 |

**宿主 → 页面（evt，主动推送）**

| action | 说明 |
| :--- | :--- |
| `device.snapshot` | 硬件 + 传感器全量快照 |
| `sensors.tick` | 传感器增量，1~2 秒一次 |
| `gpu.list` | 多卡列表 |
| `unlock.state` | 解锁状态 |
| `spoof.models` | 可选型号清单（分组） |
| `spoof.state` | 当前各卡伪装状态 |
| `power.state` | 省电状态 + 功耗范围 |
| `dlss.state` | 运行库现状 |
| `dlss.preflight` | 前置检查结果 |
| `overlay.state` | 覆盖补丁状态 |
| `dual.state` | 双卡拓扑 + 绑定列表 |
| `maintenance.snapshots` | 快照列表 |
| `logs.append` | 追加日志行（流式） |
| `update.state` | 更新状态 |
| `app.toast` | `{ level, text }` 宿主发起的提示 |
| `app.busy` | `{ busy, text }` 全局忙碌遮罩开关 |
| `theme.apply` | `{ theme }` 宿主回传当前主题 |

### 4.6 无宿主时的降级（必须实现）

```js
const hasHost = !!(window.chrome && window.chrome.webview);
```

没有宿主时，用内置 `MOCK` 数据走同一套消息管道，让浏览器预览依然可点。**MOCK 数据必须集中在文件末尾一个对象里，并用醒目注释标记「演示数据，仅浏览器预览用」**，方便后续清理。

`app.init` 发出后 **800ms 内**没收到任何 `evt`，视为宿主无响应，切到 MOCK。这条要防止宿主假死时界面永远白屏。

---

## 5. 数据契约（字段名不可改）

> 全部 **camelCase**。字符串字段一律给空串 `""` 兜底，不要给 `null`；数字给 `0`；布尔给 `false`。**没有数据的字段要能显示"未知"，不要显示 0 或空。**

### 5.1 `device.snapshot`

```json
{
  "gpu": {
    "name": "NVIDIA CMP 90HX",
    "model": "CMP 90HX",
    "chip": "GA102",
    "deviceId": "10DE:220D",
    "vram": "10GB GDDR6X",
    "driverVersion": "616.92",
    "bus": "0000:01:00.0",
    "pcieGen": "2",
    "pcieWidth": "16",
    "pcieFormatted": "PCIe x16 2.0 (全速已解锁)",
    "isSpoofed": false,
    "displayName": "NVIDIA CMP 90HX"
  },
  "sensors": {
    "valid": true,
    "temp": 77,
    "fanPercent": 62,
    "powerDraw": 297.1,
    "powerLimit": 300,
    "powerMinLimit": 0,
    "powerDefaultLimit": 250,
    "powerMaxLimit": 1048.57,
    "memUsedMB": 228,
    "memTotalMB": 10240,
    "utilPercent": 0,
    "coreClock": 1800,
    "coreClockMax": 2100,
    "memClock": 9501,
    "memClockMax": 9501,
    "pstate": "P8",
    "tempSlowdownC": 95,
    "tempShutdownC": 98
  },
  "system": {
    "cpuName": "AMD Ryzen 9 5900X",
    "cpuCores": "12 核 24 线程",
    "cpuClock": "3.70 GHz",
    "motherboard": "B550M",
    "bios": "1.2.0.7",
    "memory": "32 GB",
    "os": "Windows 11 26100",
    "uefi": true,
    "secureBoot": false,
    "hags": true,
    "uptime": "2 天 3 小时"
  },
  "flags": {
    "isAdmin": true,
    "nvAppInstalled": true,
    "isPowerSaverRunning": false
  }
}
```

> **`bus` 字段说明**：宿主侧目前没有读取总线地址，我会补 `LocationInformation` 的读取。**你在界面上正常显示这个字段即可，不要自己拼 `0000:01:00.0`。** 拿不到时显示"未知"。
>
> **`sensors.valid` 为 `false` 时，所有仪表必须显示为"不可用"状态（灰色、无指针、无数值）**，不要显示 0 ℃ / 0 W。

### 5.2 `sensors.tick`（增量）

```json
{ "temp": 78, "fanPercent": 64, "powerDraw": 301.2, "utilPercent": 3, "pstate": "P0", "coreClock": 1950, "memClock": 9501, "memUsedMB": 512 }
```

只推变化的字段。页面必须能接受**任意字段缺失**的增量。

### 5.3 `gpu.list`

```json
{
  "gpus": [
    {
      "slot": 0,
      "detectedModel": "CMP 90HX",
      "chip": "GA102",
      "vram": "10GB GDDR6X",
      "currentDesc": "NVIDIA CMP 90HX",
      "targetName": "NVIDIA GeForce RTX 3080",
      "isSpoofed": false,
      "isComputeUnlocked": false,
      "isPcieUnlocked": true
    }
  ]
}
```

### 5.4 `unlock.state`

```json
{
  "mode": "Mode1_DirectBoot",
  "modeLabel": "模式 1 · UEFI 直启",
  "detail": "已注入 NVPermissiveCompat.efi 到 ESP",
  "hasBackup": true,
  "isUefi": true,
  "secureBoot": false,
  "espMounted": true,
  "channels": [
    { "id": "A", "label": "UEFI 直启", "available": true, "installed": true },
    { "id": "B", "label": "UEFI Shell 链启", "available": true, "installed": false },
    { "id": "C", "label": "U 盘引导", "available": true, "installed": false },
    { "id": "D", "label": "恢复官方引导", "available": true, "installed": false }
  ],
  "usbDrives": [{ "letter": "E:\\", "label": "Kingston 32GB" }]
}
```

`mode` ∈ `None` / `Mode1_DirectBoot` / `Mode2_UefiShell` / `DirtyState`。`DirtyState` 表示"检测到半完成状态"，**必须用警示色提示用户先修复**，不要显示成功。

### 5.5 `spoof.state` / `spoof.models`

```json
{ "gpus": [{ "slot": 0, "detectedModel": "CMP 90HX", "chip": "GA102", "currentDesc": "NVIDIA CMP 90HX", "isSpoofed": false, "targetName": "NVIDIA GeForce RTX 3080" }] }
```

```json
{
  "groups": [
    {
      "chip": "GA102",
      "label": "GA102（90HX / RTX 30 系）",
      "models": ["NVIDIA GeForce RTX 3090 Ti", "NVIDIA GeForce RTX 3090", "NVIDIA GeForce RTX 3080 Ti", "NVIDIA GeForce RTX 3080"]
    },
    { "chip": "GA104", "label": "GA104（70HX / RTX 30 系）", "models": [] },
    { "chip": "TU102", "label": "TU102（50HX / RTX 20 系）", "models": [] },
    { "chip": "TU106", "label": "TU106（40HX / RTX 20 系）", "models": [] },
    { "chip": "TU116", "label": "TU116（30HX / GTX 16 系）", "models": [] },
    { "chip": "GA100", "label": "GA100（170HX / 计算卡）", "models": [] }
  ],
  "recommended": "NVIDIA GeForce RTX 3080"
}
```

**用 `<optgroup>` 渲染分组。** 不要写死任何型号字符串。

### 5.6 `power.state`

```json
{
  "enabled": true,
  "running": true,
  "preset": "adaptive",
  "idleSeconds": 8,
  "currentPstate": "P8",
  "powerRange": { "min": 0, "default": 250, "max": 1048.57, "current": 300 },
  "taskInstalled": true,
  "lastTrigger": "2026-10-20 17:22:31"
}
```

### 5.7 `dlss.state` / `dlss.preflight`

```json
{
  "localVersion": "0.4.0",
  "latestVersion": "0.4.2",
  "hasUpdate": true,
  "runtimeFound": true,
  "runtimePath": "C:\\ProgramData\\CMP90HX_Toolbox\\dlss_runtime",
  "mirrors": [{ "name": "GitHub", "latencyMs": 320, "ok": true }],
  "proxyCandidates": ["nvngx.dll", "dxgi.dll", "winmm.dll"],
  "gameDir": ""
}
```

```json
{
  "passed": false,
  "blockingCount": 1,
  "items": [
    { "key": "gameDir", "label": "游戏目录是否存在", "ok": true, "blocking": true, "detail": "已找到" },
    { "key": "runtime", "label": "DLSS 运行库是否就绪", "ok": false, "blocking": true, "detail": "本地版本缺失，请先下载" },
    { "key": "writeable", "label": "目录是否可写", "ok": true, "blocking": true, "detail": "" }
  ]
}
```

`ok: false` 且 `blocking: true` 时，**部署按钮必须置灰**，并把该条用危险色标出。

### 5.8 `overlay.state`

```json
{
  "nvAppInstalled": true,
  "nvAppPath": "C:\\Program Files\\NVIDIA Corporation\\NVIDIA app\\CEF\\NVIDIA app.exe",
  "patched": false,
  "backupExists": true,
  "cefCacheSize": "182 MB",
  "altRReady": false,
  "altZReady": false
}
```

### 5.9 `dual.state`

```json
{
  "hasRenderGpu": true,
  "renderGpuName": "NVIDIA CMP 90HX",
  "renderGpuPciId": "10DE:220D",
  "hasDisplayGpu": true,
  "displayGpuName": "AMD Radeon Graphics",
  "displayGpuPciId": "1002:1638",
  "hasVirtualGpu": false,
  "virtualGpuName": "",
  "boundGamesCount": 3,
  "hagsActive": true,
  "boundApps": [{ "exePath": "D:\\Games\\xxx\\game.exe", "name": "game.exe", "addedAt": "2026-10-18" }]
}
```

### 5.10 `maintenance.snapshots`

```json
{
  "items": [
    { "id": "snap-20261018-131338", "label": "伪装前自动备份", "createdAt": "2026-10-18 13:13:38", "sizeBytes": 245760, "kind": "auto", "restorable": true }
  ]
}
```

`items` 可能是空数组 —— **必须有空态设计**（"暂无还原点，执行危险操作前会自建备份"）。

### 5.11 `logs.append` / `logs.query`

```json
{ "at": "2026-10-20 17:22:31", "level": "info", "text": "已更新系统驱动类描述为 NVIDIA GeForce RTX 3080" }
```

`level` ∈ `info / warn / error / audit`。日志区需要：自动滚底（用户往上翻时**停止**自动滚底）、级别过滤、关键字搜索、复制、导出。

### 5.12 `update.state`

```json
{
  "currentVersion": "6.0.0",
  "latestVersion": "6.0.2",
  "hasUpdate": true,
  "releaseNotes": "1. 修复...",
  "downloadUrl": "",
  "assetName": "HXStudio.exe",
  "sha256": "a1b2c3...",
  "verified": false
}
```

**`verified` 为 `false` 时，应用更新按钮必须禁用**，并显示"正在校验"或"校验失败"。这是安全硬要求，不要为了好看默认放行。

---

## 6. 交互与状态规范

### 6.1 每个数据区块都要有三态

| 态 | 表现 |
| :--- | :--- |
| **加载中** | 骨架屏（灰块微光扫过）或 `--` 占位。**不要用转圈铺满整页** |
| **空态** | 一句话说明 + 一个可点的下一步动作（如"暂无还原点" + "立即创建"） |
| **错误态** | 错误码 + 中文说明 + "重试"按钮。**不要只显示红字** |

### 6.2 危险操作

以下操作**必须**走二次确认 Modal（自绘，不用 `confirm`）：

- `unlock.install` / `unlock.rollback`
- `spoof.apply`
- `snapshot.restore`
- `factory.reset`
- `dlss.remove`
- `overlay.restore`

Modal 内容必须包含：**会改什么**、**会影响什么**、**能不能撤销**、**是否需要重启**。`factory.reset` 还要求用户手动输入 `RESET` 才能点确认。

### 6.3 长任务

- 执行期间相关按钮进入 loading 态并禁用，**不要锁死整个界面**
- 顶部或页面内显示进度条 + 阶段文案
- 完成后 Toast 提示，失败时 Toast 用危险色并附错误码
- 提供"查看日志"跳转

### 6.4 键盘

- `Esc` 关闭 Modal
- `Ctrl+F` 在日志页聚焦搜索
- `Tab` 焦点顺序合理，焦点环可见（`outline` 不要去掉）

### 6.5 无鼠标可用性

所有 `hover` 才显示的信息，都必须有点击/聚焦的等价入口。3D 显卡的"悬停旋转"，要支持点击切换姿态。

---

## 7. 性能预算（会被实测）

| 指标 | 目标 | 底线 |
| :--- | ---: | ---: |
| 首屏渲染 | < 400ms | 800ms |
| 切页响应 | < 120ms | 200ms |
| 动画帧率 | 60fps | 30fps |
| 页面内存 | < 80MB | 120MB |
| 空闲 CPU | < 2% | 5% |

**必须实现的降级机制**：

给 `<body>` 加 `perf-low` 类，一键关闭：粒子场（`#ambientParticleCanvas`）、CSS 3D 旋转、`backdrop-filter` 毛玻璃（换成不透明背景）。这三项是性能杀手，**降级后界面必须依然完整可用，不能塌**。

另需提供：

- 页面不可见时（`document.visibilityState === 'hidden'`）暂停所有 `requestAnimationFrame`
- 窗口最小化时暂停传感器动画
- 用户开启"减少动态效果"（`prefers-reduced-motion`）时自动降级

---

## 8. 交付物与验收

### 8.1 你要交付的文件

| 文件 | 内容 |
| :--- | :--- |
| `hx_studio_6_1.html` | 单文件，双击可预览，无外部依赖 |
| `BRIDGE-CONTRACT.md` | 你实现的 action 清单 + 每个 payload 的实际字段（便于我逐条核对） |
| `CHANGELOG.md` | 相对 6.0 改了什么、删了什么、加了什么 |
| 截图 | `device` / `spoof` / `power` / `logs` 四页，暗色 + 亮色各一组 |

### 8.2 自查清单（交付前逐条打勾）

- [ ] 全文搜索 `616.92`、`0000:01:00.0`、`v3.7.10`、`共 2 个`、`已启用`、`CMP 90HX` —— 除 MOCK 区外**零命中**
- [ ] 全文搜索 `http://`、`https://`、`<img`、`@import` —— **零命中**
- [ ] 全文搜索 `onclick="simulate` —— **零命中**
- [ ] 全文搜索 `alert(`、`confirm(`、`prompt(` —— **零命中**
- [ ] 全文搜索 emoji 正则 —— 除文案外**零命中**
- [ ] 断网打开文件，全部页面可切换、无报错、无裂图
- [ ] 浏览器控制台**零 error**
- [ ] 1160×660 与 2560×1440 两个极端尺寸下，无横向滚动条、无文字重叠
- [ ] 125% / 150% DPI 下按钮文字不溢出
- [ ] 移除 `MOCK` 对象后，页面**不白屏**（走空态）
- [ ] 10 个页面全部有三态（加载/空/错误）的实现
- [ ] 所有"已启用/已解锁"字样的来源都是变量，不是写死
- [ ] 降级模式 `perf-low` 下界面完整

### 8.3 我会怎么验收

1. 用我自己的宿主程序加载，逐页点一遍，看是否有未接线的按钮
2. 造 5 种异常数据（0 显卡 / 传感器无效 / 解锁半完成 / 快照为空 / 更新校验失败），看界面是否误导
3. 断网 + 拔掉 WebView2 运行时，看降级是否成立
4. 在 CMP 90HX 真机上跑一遍，看帧率

---

## 9. 如果你不确定，按这些默认值做（不要停下来问）

| 问题 | 默认做法 |
| :--- | :--- |
| 侧栏图标用哪套 | 沿用现有内联 SVG 风格，1.8px 描边，24×24 viewBox |
| 字体 | 系统字体栈 `-apple-system, "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif`，数字用 `font-variant-numeric: tabular-nums` |
| 主色 | 暗色 `#38bdf8`，亮色 `#0284c7`，不改 |
| 圆角 | 卡片 12px，按钮 8px，标签 999px |
| 卡片间距 | 12px |
| 是否保留顶部"主题切换"演示条 | **删除**。主题切换收进设置/维护页，因为这是真实软件不是展厅 |
| 是否保留"TITANIUM PRO / 3D & PARTICLE ENGINE"这类展厅文案 | **删除**，改为产品名与真实版本号 |
| 页面切换动画 | 保留，但 140ms 以内，`ease-out` |
| 日志页默认显示多少条 | 100 条，向上滚动加载更多 |
| 3D 显卡模型 | 保留，但必须可降级为静态图 |

---

## 10. 一句话记住

> **你负责"看起来对"，我负责"真的能做"。**
> 界面上每一个数字都必须有来源，每一个按钮都必须有去处，每一个"成功"都必须是真的成功。

---

RainCandy Technology · 前端交付规范 v1.0 · 2026-10-20
