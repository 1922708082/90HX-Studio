# NVIDIA App 游戏内覆盖解锁教程

> 让矿卡 / 非白名单显卡也能用 Alt+R 性能浮窗与 Alt+Z 录屏控制台
> 适用：NVIDIA App（新版，非旧版 GeForce Experience）
> 整理：90HX Studio · 2026-10

---

## 一、这个功能是干嘛的

NVIDIA App 会给显卡做「硬件白名单」检查。像 **CMP 矿卡、部分专业卡、老架构显卡** 不在白名单里，
游戏内覆盖（In-Game Overlay）就被锁死，表现为：

- 游戏里按 Alt+Z 没反应，呼不出录屏控制台
- 按 Alt+R 看不到性能监控浮窗
- NVIDIA App 里「游戏内覆盖」开关点了自动弹回去

这个解锁就是**把门控关掉**，让覆盖功能正常可用。

---

## 二、原理（为什么能解）

NVIDIA App 是基于 CEF（Chromium 内核）的桌面程序，功能开关的判断逻辑**写在前端 JS 里**。
真正卡住你的是两处判断：

### ① ShadowPlay 支持标记

压缩后的代码大致长这样（变量名每次更新都会变）：

```js
const Se = "true" === fe.overallState && U?.ShadowPlaySupported
```

意思是「云端说是 true **并且** 硬件白名单也支持」才给开。我们直接改成恒真：

```js
const Se = !0
```

### ② 云端硬件白名单查询

功能列表回填时，会拿本机架构去比对白名单：

```js
overallState: this.cachedArchitecture === L._R ? "true" : ce.body.criteria.overallState
```

架构不在白名单就落到后半段（云端返回值 = false）。直接写死成 true：

```js
overallState: "true"
```

> **关键点：不能写死字符串去匹配。**
> NVIDIA 每次更新都会重新压缩混淆，变量名（Se / fe / U / L._R）必然变。
> 要用**正则匹配语法结构**，只认那些稳定的部分，才能跨版本复用。

---

## 三、准备工作

- 已安装新版 **NVIDIA App**（能正常打开）
- 能拿到**管理员权限**（改的是 Program Files 下的文件）
- 一个文本编辑器（VSCode / Notepad++ / Sublime，**别用记事本**，大文件会卡）

**文件位置：**

```
C:\Program Files\NVIDIA Corporation\NVIDIA App\www\main.<一串哈希>.js
```

这个 `main.*.js` 有 5MB 左右，**不要**整个打开用记事本编辑。

---

## 四、手动操作步骤

> ⚠️ **先备份！** 动手前把 `main.*.js` 复制一份改名 `main.*.js.bak`，这是唯一的退路。

### Step 1 · 关闭 NVIDIA App

任务管理器里结束这几个进程，确保文件不被占用：

```
NVIDIA App
NVIDIA Overlay
nvsphelper64
```

### Step 2 · 备份原文件

```cmd
copy "main.xxxxxxxx.js" "main.xxxxxxxx.js.bak"
```

### Step 3 · 定位两处代码

用编辑器打开 `main.*.js`，依次搜索这两个关键字：

| 搜索关键字 | 找到后长什么样 |
|---|---|
| `ShadowPlaySupported` | `const XX="true"===YY.overallState&&ZZ?.ShadowPlaySupported` |
| `cachedArchitecture` | `overallState:this.cachedArchitecture===XX?"true":YY.body.criteria.overallState` |

### Step 4 · 替换

**第一处** → 把整个 `const XX="true"===...ShadowPlaySupported` 换成：

```js
const XX=!0
```

（`XX` 保留原来那个变量名，别改）

**第二处** → 把整个 `overallState:this.cachedArchitecture===...criteria.overallState` 换成：

```js
overallState:"true"
```

### Step 5 · 保存 + 清缓存

保存文件，然后删掉 CEF 缓存目录（不删可能还是旧界面）：

```
%LOCALAPPDATA%\NVIDIA Corporation\NVIDIA App\CefCache
```

### Step 6 · 重启 NVIDIA App

重新打开 App，进「设置 → 功能 → 游戏内覆盖」，开关应该能保持在开启状态了。

> ✅ **验证方法**：进任意游戏（或全屏窗口），按 Alt+Z 能呼出控制台、
> 按 Alt+R 能看到帧率浮窗 = 成功。

---

## 五、懒得手动？用现成工具

如果是 CMP 矿卡用户，直接用 **90HX Studio**（本教程配套工具）：

1. 打开软件 → 左侧「游戏内覆盖」
2. 点「一键解锁游戏覆盖 (ShadowPlay)」
3. 等它自动备份、打补丁、清缓存、重启 App

工具内部用的是**正则匹配**而不是写死字符串，所以 NVIDIA App 更新后一般还能命中；
真遇到改得面目全非的版本，日志里会告诉你哪条规则没命中，方便反馈补规则。

---

## 六、常见问题

**Q：NVIDIA App 更新后失效了？**
正常。更新会覆盖 `main.*.js`，补丁自然没了，**重新执行一次**即可。
这也是为什么用正则比写死字符串强——新版大概率还能直接命中。

**Q：改完 App 打不开了 / 白屏？**
说明改坏了。把 `.bak` 备份还原回去：

```cmd
copy "main.xxxxxxxx.js.bak" "main.xxxxxxxx.js"
```

然后清一次 CEF 缓存重启。所以**备份绝对不能省**。

**Q：提示没有权限 / 保存不了？**
Program Files 目录要管理员权限。用管理员身份打开编辑器，
或者先把文件复制到桌面改完再拷回去。

**Q：解锁后录屏没声音 / 卡顿？**
跟这个补丁无关，是显卡编码器或驱动的问题。
矿卡本身编解码单元可能受限（部分魔改驱动明确不支持 NVDEC/NVENC）。

**Q：会被封号吗？动的是本地文件吗？**
只改本机的 App 前端文件，不联网、不改驱动、不注入游戏进程。
但**任何第三方修改都请自行评估风险**，本教程仅供学习交流。

---

## 七、一键还原

不想用了？把备份文件覆盖回去，清缓存，重启 App，就回到官方原版：

```cmd
copy "main.xxxxxxxx.js.bak" "main.xxxxxxxx.js"
```

然后删除 `CefCache` 目录，重启 NVIDIA App。

---

*本教程基于实测整理，仅用于学习与技术交流。修改前请务必备份，因未备份导致的问题无法恢复。*
*90HX Studio · CMP 矿卡调优套件*
