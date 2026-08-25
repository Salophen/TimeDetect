# TimeDetect

TimeDetect 是一个面向 DeepSeek API 用户的轻量桌面工具，用北京时间直观显示当前处于峰时还是谷时、距离下一次价格时段切换还有多久，并集中展示 DeepSeek 官方服务状态和 API 账户余额。

> **v1.2.0 已正式支持 Windows。** 现在可以在 Windows 系统托盘或 macOS 菜单栏中使用 TimeDetect，并通过桌面悬浮挂件随时查看 DeepSeek 峰谷时段。

[下载 TimeDetect v1.2.0](https://github.com/Salophen/TimeDetect/releases/tag/v1.2.0)

## TimeDetect 能做什么

- **显示峰谷时段**：固定按照北京时间（`Asia/Shanghai`）计算，不受电脑当前时区影响。
- **显示下一次切换**：通过桌面挂件和详情面板查看当前倍率、当前时段及下一个切换时间。
- **监测官方服务状态**：展示 DeepSeek 整体服务、API 服务和网页对话服务的当前状态；数据无法确认时不会误报为正常。
- **查询 API 余额**：可选填自己的 DeepSeek API Key，查看官方余额接口返回的 CNY、USD 等币种余额和账户可用状态。
- **谷时通知**：支持谷时开始提醒，以及提前 5、10、15 或 30 分钟提醒。
- **桌面悬浮挂件**：可在桌面模式和始终置顶模式之间切换，也可随时隐藏。
- **登录时自动启动**：支持登录 Windows 或 macOS 后自动运行。
- **低干扰运行**：Windows 版本驻留系统托盘，macOS 版本驻留菜单栏；重复启动不会创建多个实例。

显示峰谷时段和查询公开服务状态都**不需要 API Key**。只有账户余额功能需要用户自行配置 API Key。

## v1.2.0：新增 Windows 支持

v1.2.0 是 TimeDetect 的首个 Windows 正式版本。Windows 版本使用 C#、WPF 和 .NET 8 实现，并与 macOS 版本共享同一套峰谷规则和接口行为。

Windows 版本包括：

- 系统托盘图标和详情面板；
- `240 × 240` 桌面悬浮挂件；
- 桌面模式与始终置顶模式；
- DeepSeek 峰谷时段、下一切换时间和价格倍率显示；
- DeepSeek 官方服务状态监测；
- API Key 配置、余额查询和余额缓存；
- 谷时通知与提前提醒；
- 登录 Windows 时自动启动；
- 单实例运行；
- self-contained `win-x64` 发布包，无需另外安装 .NET Runtime。

本次发布也将 macOS 包升级为同时包含 `arm64` 和 `x86_64` 的 Universal App。Windows 和 macOS 安装包均由 GitHub Actions 测试、构建并生成 SHA-256 校验文件。

## 峰谷规则

TimeDetect 当前采用以下北京时间规则：

| 日期 | 时段 | 显示 |
|---|---|---|
| 周一至周五 | `09:00–12:00`、`14:00–18:00` | 峰时 · `1.0x` 原价 |
| 周一至周五 | `00:00–09:00`、`12:00–14:00`、`18:00–24:00` | 谷时 · `0.5x` 半价 |
| 周六、周日 | 全天 | 谷时 · `0.5x` 半价 |

所有边界采用左闭右开区间：工作日 `09:00`、`14:00` 立即进入峰时，`12:00`、`18:00` 立即进入谷时。完整的跨平台规则见 [`shared/rules.md`](shared/rules.md)。

价格规则可能随 DeepSeek 官方策略调整；涉及实际费用时，请同时以 DeepSeek 官方价格页面和账户账单为准。

## 下载与安装

前往 [GitHub Releases](https://github.com/Salophen/TimeDetect/releases/tag/v1.2.0) 下载对应平台的软件包。每个 ZIP 都附带一个 `.sha256` 校验文件。

### Windows 10/11（64 位）

1. 下载 [`TimeDetect-windows-x64.zip`](https://github.com/Salophen/TimeDetect/releases/download/v1.2.0/TimeDetect-windows-x64.zip)。
2. 将 ZIP 完整解压到一个固定目录。
3. 运行 `TimeDetect.exe`。
4. 在系统托盘中找到 TimeDetect 图标；左键打开详情面板，右键可显示或隐藏挂件并退出程序。

Windows 包是 self-contained 应用，不需要预先安装 .NET。请保留 ZIP 中与 `TimeDetect.exe` 一起提供的原生 DLL，不要只移动 EXE 文件。

### macOS 13 或更高版本

1. 下载 [`TimeDetect-macos-universal.zip`](https://github.com/Salophen/TimeDetect/releases/download/v1.2.0/TimeDetect-macos-universal.zip)。
2. 解压得到 `TimeDetect.app`，并将其移动到“应用程序”目录。
3. 启动 TimeDetect，在菜单栏中找到运行图标。

该 Universal App 同时支持 Apple Silicon 和 Intel Mac。更详细的 macOS 使用、构建和 WidgetKit 说明见 [`macos/README.md`](macos/README.md)。

## 安全与隐私

- 峰谷时间完全在本地计算，不需要联网。
- 服务状态查询只访问 DeepSeek 使用的公开状态页面。
- API Key 只在余额请求中发送至官方端点 `https://api.deepseek.com/user/balance`，不会用于调用聊天接口。
- API Key 不会写入项目源码、GitHub Actions 日志或发布安装包。
- 删除 API Key 后，TimeDetect 会停止余额请求并清除本地凭据和余额缓存。

**v1.2.0 已知限制：** 为避免启动时出现系统凭据认证窗口，macOS 将 API Key 保存在本机 `UserDefaults`，Windows 将其保存在当前用户 `%APPDATA%\TimeDetect\api-key.json`。两者都不是加密的安全凭据库。如果电脑由多人共用或本地账户环境不可信，请不要在应用中保存 API Key。后续版本计划迁移到更安全且不打扰启动体验的系统凭据存储方案。

当前发布包未配置正式的 Windows 代码签名、Apple Developer ID 签名或 Apple notarization，因此首次启动时操作系统可能显示来源或安全提示。请只从本仓库的 GitHub Releases 下载，并使用随附的 SHA-256 文件核对完整性。

## 平台与技术栈

| 平台 | 源码目录 | 技术栈 | 发布架构 |
|---|---|---|---|
| Windows | [`windows/`](windows/) | C# + WPF + .NET 8，无第三方依赖 | `win-x64` self-contained |
| macOS | [`macos/`](macos/) | Swift + SwiftUI，纯 `swiftc` 构建，无第三方依赖 | Universal `arm64` + `x86_64` |

两个平台的共同规则与测试样例位于：

- [`shared/rules.md`](shared/rules.md)：峰谷时段、时区、端点、文案和轮询规则；
- [`shared/fixtures/`](shared/fixtures/)：跨平台解析测试使用的 JSON 样例。

## 从源码构建

### Windows

需要 Windows 和 .NET 8 SDK：

```powershell
cd windows
dotnet build TimeDetect.Windows.sln
dotnet run --project src\TimeDetect.Windows
dotnet test TimeDetect.Windows.sln
```

### macOS

需要 macOS 和 Apple Swift 工具链：

```sh
cd macos
./Scripts/test.sh
./Scripts/build.sh
open TimeDetect.app
```

## 仓库结构

```text
TimeDetect/
├─ macos/                  # macOS App、测试和构建脚本
├─ windows/                # Windows WPF App 与测试
├─ shared/                 # 双平台共同规则和测试样例
└─ .github/workflows/      # 双平台 CI 与 Release 自动发布流程
```

## 项目说明

TimeDetect 是独立开发的社区项目，不是 DeepSeek 官方客户端，也不代表 DeepSeek。服务状态、价格规则和余额数据最终均以 DeepSeek 官方信息为准。
