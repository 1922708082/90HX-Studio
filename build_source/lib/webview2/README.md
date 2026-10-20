# WebView2 SDK 引用程序集

本目录存放 Microsoft Edge WebView2 的官方引用程序集，供 `build.ps1` 编译时 `/r:` 引用。

## 版本与来源

| 项 | 值 |
| :--- | :--- |
| 包名 | `Microsoft.Web.WebView2` |
| 版本 | **1.0.4258.31**（stable） |
| 来源 | `https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/1.0.4258.31/microsoft.web.webview2.1.0.4258.31.nupkg` |
| 提取日期 | 2026-10-20 |
| 目标框架 | .NET Framework 4.6.2 |

## 文件说明

| 文件 | 大小 | 用途 |
| :--- | ---: | :--- |
| `Microsoft.Web.WebView2.Core.dll` | 698 KB | 托管核心：`CoreWebView2Environment` / `CoreWebView2` / 消息桥接 |
| `Microsoft.Web.WebView2.Wpf.dll` | 86 KB | 托管 WPF 控件：`Microsoft.Web.WebView2.Wpf.WebView2` |
| `x64/WebView2Loader.dll` | 195 KB | **原生**加载器，64 位 |
| `x86/WebView2Loader.dll` | 155 KB | **原生**加载器，32 位 |
| `LICENSE.txt` / `NOTICE.txt` | — | 微软原始许可与声明，随包分发，勿删 |

## 为什么这些 DLL 入库

项目原本的 `.gitignore` 忽略所有 `*.dll`。这会导致换机器、换目录后无法构建——而这正是审计报告里点名的"构建不可复现"问题（P1-3）。

因此对本目录做了白名单例外（见 `.gitignore`）：

```
!build_source/lib/webview2/*.dll
!build_source/lib/webview2/*/*.dll
```

提交后，仓库自包含 WebView2 引用，不再依赖本机 NuGet 缓存。

## 运行时注意

- **两个托管 DLL 不能只靠编译引用**：程序运行时需要它们可被加载。当前方案是把它们作为嵌入资源打进 exe，首次运行时解压到数据目录，再通过 `AppDomain.AssemblyResolve` 加载（P3 实现）。
- **`WebView2Loader.dll` 是原生库**，必须在 exe 同目录或已加入 DLL 搜索路径，否则会抛 `BadImageFormatException` / `DllNotFoundException`。按进程位数选 x64 或 x86。
- **WebView2 Runtime 是另一回事**：这是系统级安装的浏览器内核（Evergreen），不属于本目录。程序运行时需检测，缺失则引导安装。

## 升级方式

改版本时按上述 URL 重新下载，覆盖 5 个文件，并更新本文件的版本号与日期。升级后必须重跑 `build.ps1` 并回归 P0 帧率测试。
