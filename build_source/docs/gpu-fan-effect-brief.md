# 显卡 3D 风扇气流特效 · 交接说明 + 生成提示词

> 用途：把本文档整段发给另一个 AI，让它产出「风扇旋转 + 气流流动」特效代码，
> 再由我们接进 HX Studio 的主界面。

---

## 一、技术背景（给对方 AI 看的上下文）

- 运行环境：**WebView2（Chromium 内核，默认最新版）**，单文件内嵌 HTML，**不联网、不能引 CDN**
- 页面已经是暗色科技风（深蓝黑底 + 青色高光），主色 `#38BDF8`，辅助 `#22D3EE`
- 现有 3D 显卡模型是**纯 CSS 3D**（`transform-style: preserve-3d` + 多层 `translateZ`），
  不是 WebGL、不是 Three.js。**新特效必须沿用这套技术路线**，不要引入任何库

## 二、现有结构（集成点）

三风扇所在容器（简化版）：

```html
<div class="gpu-3d-stage" id="gpu3dStage">
  <div class="gpu-3d-card" id="gpu3dCard">
    <div class="gpu3-fans-row">

      <!-- 风扇 1（左） -->
      <div class="gpu3-fan-well">
        <div class="gpu-3d-fan">
          <svg class="fan-svg" viewBox="0 0 100 100"><use href="#hxTripleFanRotor"/></svg>
        </div>
        <div class="gpu3-fan-hub"><span id="gpu3dBadgeBrand">HX</span></div>
      </div>

      <!-- 风扇 2（中，反向旋转） -->
      <div class="gpu3-fan-well fan-well-center">
        <div class="gpu-3d-fan" style="animation-direction: reverse;">
          <svg class="fan-svg" viewBox="0 0 100 100"><use href="#hxTripleFanRotor"/></svg>
        </div>
        <div class="gpu3-fan-hub hub-center"><span id="gpu3dBadgeModel">90HX</span></div>
      </div>

      <!-- 风扇 3（右） -->
      <div class="gpu3-fan-well">
        <div class="gpu-3d-fan">
          <svg class="fan-svg" viewBox="0 0 100 100"><use href="#hxTripleFanRotor"/></svg>
        </div>
        <div class="gpu3-fan-hub"><span id="gpu3dBadgeChip">GA102</span></div>
      </div>

    </div>
  </div>
</div>
```

现有风扇样式（**必须保持的基线**）：

```css
.gpu-3d-fan {
  width: 54px;
  height: 54px;
  border-radius: 50%;
  background: radial-gradient(circle at 50% 50%,
    rgba(15, 23, 42, 0.25) 0%,
    rgba(56, 189, 248, 0.14) 58%,
    rgba(203, 213, 225, 0.28) 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  animation: fanSpinSlow 6s linear infinite;
}
.gpu-3d-fan .fan-svg { width: 52px; height: 52px; }

@keyframes fanSpinSlow {
  0%   { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}
```

## 三、这次要做的效果

在**不改变现有布局尺寸**的前提下，叠加「旋转 + 气流」两层视觉：

1. **旋转层**：扇叶转起来要有真实的高速感（运动中带轻微动态模糊/拖影），
   中置风扇保持反向旋转（这是现有设计，别改）
2. **气流层**：从扇叶中心向外扩散的**环形气流涟漪 / 螺旋涡流**，
   冷色调（青→蓝白），半透明，向外逐渐淡出
3. **氛围层**：扇孔边缘有呼吸式辉光；转速越高辉光越亮、气流越密

参考观感（不要照搬）：高端散热器宣传片里风扇转动时那种「无声但明显在转」的质感，
**克制、精密、工业感**，不是 RGB 游戏风。

---

## 四、档位联动（硬性契约）

页面会在 `<body>` 上打标记，特效必须**只读这些标记**，不许自己写死速度：

```html
<body data-power-preset="sleep | adaptive | turbo">
```

| 档位 | 语义 | 期望转速 | 气流强度 | 辉光 |
| :--- | :--- | :--- | :--- | :--- |
| `sleep` 极致静音 | 锁 210MHz 十几瓦 | 慢（约 6s/圈） | 几乎无（只留一丝） | 极弱 |
| `adaptive` 智能动态 | 常态全频 | 中（约 2.4s/圈） | 中等、断续呼吸 | 中等 |
| `turbo` 满血狂暴 | 300W 放开 | 快（约 0.9s/圈） | 强、连续涡流 | 强 |

实现方式**必须**用 CSS 变量驱动，便于我们后续接实时数据：

```css
:root {
  --fan-dur: 2.4s;        /* 一圈耗时，越小越快 */
  --airflow-opacity: .45; /* 0 ~ 1 */
  --fan-glow: .5;         /* 0 ~ 1 */
}
body[data-power-preset="sleep"]    { --fan-dur: 6s;   --airflow-opacity: .10; --fan-glow: .18; }
body[data-power-preset="adaptive"] { --fan-dur: 2.4s; --airflow-opacity: .45; --fan-glow: .5;  }
body[data-power-preset="turbo"]    { --fan-dur: .9s;  --airflow-opacity: .85; --fan-glow: 1;   }
```

→ 转速变化必须**平滑过渡**（`transition: --fan-dur` 不生效，改用
`animation-duration` 的过渡替代方案，或把旋转改成 `requestAnimationFrame` 驱动角度并做缓动插值）。
**绝不允许档位切换时转速瞬间跳变。**

---

## 四·五、主题适配（两套配色，效果必须两套做法）

页面有两套主题，通过 `<body>` 的 class 切换，**同时存在**：

```html
<body class="theme-dark">   <!-- 或 theme-light -->
```

两套主题的强调色**不一样**，不是同一套颜色调透明度：

| 变量 | 暗色主题 | 亮色主题 |
| :--- | :--- | :--- |
| `--accent` | `#38bdf8`（亮青） | `#0284c7`（深蓝） |
| `--accent-secondary` | `#818cf8` | `#4f46e5` |
| `--accent-glow` | `rgba(56,189,248,.28)` | `rgba(2,132,199,.22)` |
| `--card-bg`（风扇所在底色） | `rgba(20,26,38,.88)` | `#ffffff` |
| `--text-sub` | `#94a3b8` | `#475569` |

### 关键差异（必须分别处理）

| 项 | 暗色主题 | 亮色主题 |
| :--- | :--- | :--- |
| 辉光做法 | **叠加发光**（`screen` / `plus-lighter` 混合），可以叠很亮 | **禁止发光**——白底上叠加高亮会被冲掉。改用**柔和投影 + 深色描边**（`box-shadow` 带负扩散、`filter: drop-shadow`） |
| 气流颜色 | 青白 `#38bdf8` → `#e0f2fe`，高透明度 | 深蓝 `#0284c7`，**低透明度**（.06~.18），靠**加深**而不是提亮来形成流动感 |
| 气流混合模式 | `mix-blend-mode: screen` | `mix-blend-mode: multiply` 或 `normal` |
| 扇叶拖影 | 明亮残影 | 灰蓝短残影，透明度减半 |
| 整体强度 | `--airflow-opacity` 按档位取值不变 | 在暗色基础上 **× 0.45**，否则白底会显得脏 |

### 实现要求

两套主题的差异**必须用 CSS 变量 + 选择器分别声明**，不允许在 JS 里判断主题分支：

```css
body.theme-dark {
  --fan-air-flow: rgba(56, 189, 248, .9);
  --fan-air-blend: screen;
  --fan-glow-shadow: 0 0 10px rgba(56,189,248,.55);
}
body.theme-light {
  --fan-air-flow: rgba(2, 132, 199, .18);
  --fan-air-blend: multiply;
  --fan-glow-shadow: 0 1px 3px rgba(2,132,199,.28), 0 0 0 1px rgba(2,132,199,.12);
}
```

⚠️ 交付时**两套主题都要给完整赋值**，不能只给暗色然后说「亮色沿用」。
如果某个效果在亮色下不成立（比如发光），必须给出**替代方案**而不是直接关掉。

---

## 五、技术约束（违反即返工）

| # | 约束 |
| :-- | :--- |
| 1 | **纯 CSS + 内联 SVG**，不引入任何第三方库、不联网、不用 WebGL / Three.js |
| 2 | 不改变 `.gpu3-fan-well` / `.gpu-3d-fan` 的**尺寸与位置**（54×54，三列布局不变） |
| 3 | 气流特效允许溢出到风扇外（`overflow: visible`），但不能遮挡中心徽标文字 |
| 4 | 必须支持 `body.perf-low`（低性能模式）：该状态下**完全停用**气流层，只保留最简旋转 |
| 5 | 必须支持 `@media (prefers-reduced-motion: reduce)`：停止一切动画，静态展示 |
| 6 | 目标 60fps；单帧合成层不超过 3 层；只用 `transform` / `opacity` 做动画 |
| 7 | 不使用 emoji 作图标 |
| 8 | 配色只用青蓝系（`#38BDF8` `#22D3EE` `#0EA5E9` 及其中性灰），**禁止紫→粉渐变** |
| 9 | 不硬编码颜色到 JS 里，全部走 CSS 变量 |
| 10 | 代码需带简短注释，说明每个变量/关键帧的作用 |

---

## 六、交付格式

请分三段输出，**不要合并成一个大文件**：

1. **HTML 片段**：需要插入到 `.gpu3-fan-well` 内部的额外节点（若有）
2. **CSS 片段**：新增的 keyframes、变量、档位选择器（附在各段前加注释说明挂载位置）
3. **JS 片段**（仅当实现平滑转速过渡必须用 raf 时提供）：读取 `body[data-power-preset]`
   并平滑插值 `--fan-dur`，函数名用 `initGpuFanEffect()`，挂到 `window` 上

---

## 七、可直接复制的提示词

> 下面这段直接发给另一个 AI。

```
你是一个擅长「纯 CSS 3D + SVG 动效」的前端工程师。请为一个 Electron/WebView2 桌面工具
的主界面，设计一段显卡三风扇的「旋转 + 气流」特效代码。

=== 运行环境 ===
WebView2（Chromium 最新内核），单文件内嵌 HTML，离线运行，不能引任何 CDN 或第三方库。
页面是暗色科技风：深蓝黑底 + 青色高光，主色 #38BDF8，辅助 #22D3EE。
现有显卡模型是纯 CSS 3D（preserve-3d + translateZ），不是 WebGL，不要用 Three.js。

=== 现有结构 ===
三个风扇并排，每个风扇是：
<div class="gpu3-fan-well">
  <div class="gpu-3d-fan">
    <svg class="fan-svg" viewBox="0 0 100 100"><use href="#hxTripleFanRotor"/></svg>
  </div>
  <div class="gpu3-fan-hub"><span>90HX</span></div>   <!-- 中心徽标，不能遮挡 -->
</div>

现有样式：
.gpu-3d-fan { width:54px; height:54px; border-radius:50%;
  background: radial-gradient(circle at 50% 50%,
    rgba(15,23,42,.25) 0%, rgba(56,189,248,.14) 58%, rgba(203,213,225,.28) 100%);
  animation: fanSpinSlow 6s linear infinite; }
@keyframes fanSpinSlow { 0%{transform:rotate(0)} 100%{transform:rotate(360deg)} }
中间那个风扇额外带 style="animation-direction: reverse"，必须保持反向。

=== 要做的效果 ===
在不改变 54×54 尺寸与三列布局的前提下，叠加三层：
1) 旋转层：扇叶高速旋转要有动态模糊/拖影的质感
2) 气流层：从扇心向外扩散的环形气流涟漪 + 螺旋涡流，青→蓝白，半透明，向外淡出
3) 氛围层：扇孔边缘呼吸式辉光，转速越高越亮
观感要「精密工业感」，克制、高级，不要 RGB 电竞风。

=== 档位联动（硬性要求）===
页面会在 body 上打标记 <body data-power-preset="sleep|adaptive|turbo">，
特效只能用 CSS 变量读取，不许写死速度：

sleep    → 约 6s/圈，气流几乎无，辉光极弱
adaptive → 约 2.4s/圈，气流中等且断续呼吸
turbo    → 约 0.9s/圈，气流强、连续涡流，辉光强

用这些变量承载：
  --fan-dur          一圈耗时
  --airflow-opacity  0~1
  --fan-glow         0~1
并给出 body[data-power-preset="..."] 的三档赋值。

档位切换时转速必须【平滑过渡】，不能瞬间跳变。
（animation-duration 无法过渡，可用 requestAnimationFrame 驱动角度 + 缓动插值，
  或在切换时做一次 800ms 的速度缓冲；给出你认为最稳的方案并说明理由。）

=== 双主题（重要，必须两套做法）===
页面有两套主题，通过 <body class="theme-dark"> / <body class="theme-light"> 切换，
两套的强调色不一样，不是同一套颜色调透明度：

  暗色：--accent #38bdf8（亮青）  风扇所在底色 rgba(20,26,38,.88)
  亮色：--accent #0284c7（深蓝）  风扇所在底色 #ffffff

关键差异：
- 暗色：辉光用叠加发光（screen 混合），可以叠得很亮
- 亮色：禁止发光——白底上叠加高亮会被冲掉。改用柔和投影 + 深色描边
        （box-shadow 负扩散 / drop-shadow），气流用 multiply 混合、低透明度，
        靠"加深"而不是"提亮"形成流动感
- 亮色的整体气流强度要在暗色基础上 ×0.45 左右，否则白底会显脏

两套主题的差异必须用 CSS 变量 + 选择器分别声明，
禁止在 JS 里做 if(theme==='dark') 之类的分支。

必须两套都给完整赋值。若某效果在亮色下不成立（例如发光），
要给替代方案，不能直接关掉。

=== 硬性约束 ===
- 纯 CSS + 内联 SVG，零依赖、不联网、无 WebGL
- 不改 .gpu-3d-fan / .gpu3-fan-well 的尺寸和位置
- 气流可溢出，但不能遮住中心徽标文字
- 支持 body.perf-low：该状态下停用气流层，只留最简旋转
- 支持 @media (prefers-reduced-motion: reduce)：全部静止
- 60fps，只用 transform / opacity 做动画
- 不用 emoji 作图标
- 配色只用青蓝系（#38BDF8 / #22D3EE / #0EA5E9）+ 中性灰，禁止紫→粉渐变
- 颜色不硬编码进 JS，全部走 CSS 变量

=== 输出格式 ===
分三段，不要合并：
1) HTML 片段（需要额外插入的节点，若无需新增则明确说明「不需要」）
2) CSS 片段（含 keyframes、变量、三档选择器、**两套主题选择器**，每段前注释挂载位置）
3) JS 片段（仅当需要 raf 做平滑变速时提供，函数名 initGpuFanEffect()，挂到 window）
每段代码配 2~4 行说明：为什么这么写、性能上注意什么。
最后单独列一节「两套主题差异说明」，讲清亮色下用什么替代了暗色的发光。
```

---

## 八、我们这边的接入计划（收到产物后）

1. 新建 `web/css/06-fan-effect.css`，把生成的 CSS 放进独立文件，避免和现有样式打架
2. HTML 片段插到 `pages/device.html` 的 `.gpu3-fan-well` 内
3. JS 片段放进 `js/app.js` 末尾，在 `boot()` 后调用 `initGpuFanEffect()`
4. 在 `renderPowerPage()` 里补一行：把当前档位写到 `document.body.dataset.powerPreset`
   （✅ 已完成，commit 80dc573）
5. **两套主题各验一遍**：暗色看发光是否过曝，亮色看气流是否发灰/看不见；
   亮色下若出现「白底上一团脏灰」，说明混合模式或透明度没按 ×0.45 收
6. 用 `body.perf-low` 与 `prefers-reduced-motion` 做双保险
7. 编译验证：三档切换时转速应平滑变化，低性能模式下气流消失
