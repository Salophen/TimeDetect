# TimeDetect

TimeDetect 是一个无第三方依赖的 macOS 菜单栏与桌面悬浮小挂件，用北京时间显示 DeepSeek 的峰时/谷时状态。

## 时段规则

规则固定使用 `Asia/Shanghai`，不随 Mac 的本地时区改变：

- 峰时：`09:00–12:00`、`14:00–18:00`，显示 `梁文锋 · 1.0x 原价`
- 谷时：`00:00–09:00`、`12:00–14:00`、`18:00–24:00`，显示 `梁文谷 · 0.5x 半价`

所有端点采用左闭右开区间，所以 `09:00`、`14:00` 立即进入峰时，`12:00`、`18:00` 立即进入谷时。

## 构建与运行

需要 macOS 和 Apple Swift 工具链：

```sh
cd /Users/salophen/其他/TimeDetect
./Scripts/build.sh
open TimeDetect.app
```

构建后会在项目根目录生成 `/Users/salophen/其他/TimeDetect/TimeDetect.app`。可以直接在 Finder 中双击它，无需打开终端。

App 是纯菜单栏应用，不进入 Dock。双击后会立即显示一个简洁的菜单栏运行图标和右上角 `240 × 240` 桌面悬浮挂件；菜单栏不显示名字或时间。点击图标可以打开中尺寸详情面板，右键可以快速开关挂件或退出。如果运行期间隐藏了挂件，再次双击 `TimeDetect.app` 会重新显示挂件，不会启动第二个进程。

## 测试

```sh
cd /Users/salophen/其他/TimeDetect
./Scripts/test.sh
```

测试覆盖四个切换点、切换点前一秒、跨日倒计时、Widget Timeline 节点和倒计时格式化。

## WidgetKit 源码

`Sources/Widget/WidgetExtension.swift` 是 WidgetKit 扩展 target 的完整入口，支持 `systemSmall` 和 `systemMedium`。它使用 `WidgetTimelinePlan` 预排当前时间起未来几天的 `09:00 / 12:00 / 14:00 / 18:00` 状态切换；时钟使用 WidgetKit 托管的 `Text(date, style: .time)`，不会要求扩展进程每秒常驻运行。

当前工程保留纯 `swiftc` 菜单栏 App 架构，因此 Widget 扩展需要在 Xcode 中新建 macOS Widget Extension target，并将 `Sources/Shared/*.swift` 与 `Sources/Widget/WidgetExtension.swift` 加入该 target。菜单栏 App 的 `Scripts/build.sh` 会刻意排除 `Sources/Widget`，避免两个 `@main` 入口混入同一个可执行文件。