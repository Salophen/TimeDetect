# TimeDetect

TimeDetect 用北京时间显示 DeepSeek 峰谷时段，并轻量监测官方服务状态与 API 账户余额。

本仓库是**双平台单仓库**：macOS 与 Windows 两个版本各自独立成文件夹，避免互相混淆。

| 平台 | 目录 | 技术栈 | 状态 |
|---|---|---|---|
| macOS | [`macos/`](macos/) | Swift + SwiftUI（纯 `swiftc` 构建，无第三方依赖） | v1.2.0 |
| Windows | [`windows/`](windows/) | C# + WPF（.NET 8，零第三方依赖） | v1.2.0 |

## 两个版本的共同事实源

峰谷规则、时区、DeepSeek 端点与文案在两个版本中必须保持一致，见 [`shared/rules.md`](shared/rules.md)；用于双版本对齐测试的样例 JSON 放在 [`shared/fixtures/`](shared/fixtures/)。

## 快速开始

- **macOS**：见 [`macos/README.md`](macos/README.md)
- **Windows**：
  ```powershell
  cd windows
  dotnet build TimeDetect.Windows.sln
  dotnet run --project src\TimeDetect.Windows
  dotnet test
  ```
