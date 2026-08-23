# TimeDetect

TimeDetect 是一个无第三方依赖的 macOS 菜单栏与桌面悬浮小挂件，用北京时间显示 DeepSeek 峰谷时段，并轻量监测官方服务状态与 API 账户余额。

1.1.1 新增北京时间谷时通知与提前提醒、桌面/始终置顶显示层级，以及 macOS 13+ 原生登录自动启动。通知和登录项均由系统 API 管理，不增加后台轮询 Timer。

## DeepSeek 服务与 API 账户

- **官方服务状态**：主 App 优先读取 DeepSeek 当前使用的 Flashcat 官方状态页公开数据，展示整体状态、API Service、Chat Service 与进行中的简短事件；同时保留迁移前 Statuspage JSON 的兼容回退。Flashcat 的结构化 JSON 嵌在服务端渲染页面中，App 会校验 DeepSeek 页面 ID 与域名后再解析，不会仅因 HTML 可访问就判定服务正常。数据无法验证时显示“状态暂不可用”，不会把本机网络问题误报为 DeepSeek 宕机。
- **API 余额**：配置用户自己的 DeepSeek API Key 后，主 App 通过官方 `GET https://api.deepseek.com/user/balance` 展示 CNY、USD 等接口实际返回的币种、充值余额、赠送余额及 `is_available` 状态；不做汇率换算，也不调用聊天接口测试余额。
- **安全策略**：API Key 保存在本机 App 的 UserDefaults 中，仅随余额请求发送至 `api.deepseek.com`。为避免启动时触发 macOS 钥匙串密码认证，App 不再依赖 Keychain 读取 API Key；旧版本遗留的 Keychain 项目会以禁止交互的方式兼容迁移/清理。Key 不写入项目文件或日志；Widget Extension 不读取 API Key，也不请求余额。App 启动时直接恢复 Key，并从 UserDefaults 恢复最近一次余额快照。账户区域可永久删除已有 Key；确认后会停止相关请求并清空内存状态、余额缓存和本地凭据。

网络刷新保持低频：App 启动立即查询服务状态，此后每 60 秒查询；打开 Popup 与手动按钮也可触发刷新。已配置 API Key 时，App 重启会先恢复最近一次余额并自动查询最新值，此后每 5 分钟查询；打开 Popup 时只有数据超过 60 秒才刷新。重复的同类在途请求会被忽略。

## 时段规则

规则固定使用 `Asia/Shanghai`，不随 Mac 的本地时区改变：

- 工作日峰时：`09:00–12:00`、`14:00–18:00`，显示 `梁文锋 · 1.0x 原价`
- 工作日谷时：`00:00–09:00`、`12:00–14:00`、`18:00–24:00`，显示 `梁文谷 · 0.5x 半价`
- 周六、周日全天为谷时。

所有端点采用左闭右开区间，所以工作日的 `09:00`、`14:00` 立即进入峰时，`12:00`、`18:00` 立即进入谷时。规则依据 DeepSeek 官方价格页的 UTC 峰时（周一至周五 `01:00–04:00`、`06:00–10:00 UTC`）换算为北京时间。

峰谷计算完全在本地完成，不依赖联网。峰谷时钟使用一个带 tolerance 的 1 秒本地 Timer；服务状态和余额使用可取消的低频 async polling，不绑定该 Timer。

## 构建与运行

需要 macOS 和 Apple Swift 工具链：

```sh
cd /path/to/TimeDetect
./Scripts/build.sh
open TimeDetect.app
```

构建后会在项目根目录生成 `TimeDetect.app`。可以直接在 Finder 中双击它，无需打开终端。

App 是纯菜单栏应用，不进入 Dock。双击后会立即显示一个简洁的菜单栏运行图标和右上角 `240 × 240` 桌面悬浮挂件；菜单栏不显示名字或时间。点击图标可以打开中尺寸详情面板，右键可以快速开关挂件或退出。如果运行期间隐藏了挂件，再次双击 `TimeDetect.app` 会重新显示挂件，不会启动第二个进程。

## 测试

```sh
cd /path/to/TimeDetect
./Scripts/test.sh
```

测试覆盖峰谷切换、通知与 Widget Timeline，以及 Statuspage 状态映射、组件名称解析、余额 Decimal/多币种解析、HTTP 错误分类和 mock Key 存储。网络测试使用 mock JSON，不请求 DeepSeek；普通测试不访问用户系统钥匙串。

## WidgetKit 源码

`Sources/Widget/WidgetExtension.swift` 是 WidgetKit 扩展 target 的完整入口，支持 `systemSmall` 和 `systemMedium`。它使用 `WidgetTimelinePlan` 预排当前时间起未来几天工作日的 `09:00 / 12:00 / 14:00 / 18:00` 状态切换；周末不生成峰谷切换；时钟使用 WidgetKit 托管的 `Text(date, style: .time)`，不会要求扩展进程每秒常驻运行。

联网功能只编译进主 App。WidgetKit 继续只负责本地峰谷时间显示，不接触 API Key、Keychain 或余额接口。

当前工程保留纯 `swiftc` 菜单栏 App 架构，因此 Widget 扩展需要在 Xcode 中新建 macOS Widget Extension target，并将 `Sources/Shared/*.swift` 与 `Sources/Widget/WidgetExtension.swift` 加入该 target。菜单栏 App 的 `Scripts/build.sh` 会刻意排除 `Sources/Widget`，避免两个 `@main` 入口混入同一个可执行文件。
