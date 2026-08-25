# TimeDetect 共同规则（单一事实源）

macOS 与 Windows 两个版本必须保持以下规则一致。任何改动需同步两个仓库文件夹。

## 峰谷时段（固定北京时间 `Asia/Shanghai`，不随本机时区变化）

- 工作日峰时：`09:00–12:00`、`14:00–18:00`，显示 `梁文锋 · 1.0x 原价`
- 工作日谷时：`00:00–09:00`、`12:00–14:00`、`18:00–24:00`，显示 `梁文谷 · 0.5x 半价`
- 周六、周日全天为谷时。

端点采用**左闭右开**区间：工作日 `09:00`、`14:00` 立即进入峰时，`12:00`、`18:00` 立即进入谷时。

官方 UTC 峰时（周一至周五 `01:00–04:00`、`06:00–10:00 UTC`）换算为北京时间。

## 时区标识

| 平台 | 标识 |
|---|---|
| macOS (Swift) | `Asia/Shanghai` |
| Windows (.NET) | `China Standard Time`（回退 `Asia/Shanghai` / `Asia/Chongqing`） |

## 切换节点

一天的峰谷切换点（分钟）：`09:00`(540)、`12:00`(720)、`14:00`(840)、`18:00`(1080)。
`24:00` 只是当天结束，不是额外切换；`18:00` 后的下一次切换是次日 `09:00`。

## DeepSeek 服务状态

- 首选：Flashcat 官方状态页 `https://statuspage.flashcat.cloud/deepseek`
  - 页面 `page_id = 6410630422455`，`name = "DeepSeek"`
  - 结构化 JSON 嵌在服务端渲染页面 `self.__next_f.push` 的 `initialData` 中
- 回退：`https://status.deepseek.com/api/v2/summary.json`（Atlassian Statuspage 格式）
- 官方页面：`https://status.deepseek.com`

状态聚合取最严重；「未知」不得误报为正常。

## API 余额

- 端点：`GET https://api.deepseek.com/user/balance`
- 认证：`Authorization: Bearer <apiKey>`
- 响应字段：`is_available`、`balance_infos[]`（`currency` / `total_balance` / `granted_balance` / `topped_up_balance`）
- 不做汇率换算；金额用 Decimal 解析；CNY→`¥`、USD→`$`。

## 文案

- 峰时：`梁文锋 · 1.0x 原价`
- 谷时：`梁文谷 · 0.5x 半价`
- 通知标题：`梁文谷上线` / `DeepSeek 即将进入谷时`

## 轮询节奏

- 峰谷时钟：1 个带 tolerance 的 1 秒本地 Timer
- 服务状态：启动立即查询，之后每 60 秒
- 余额：配置后启动恢复缓存并自动查询，之后每 5 分钟；面板打开超过 60 秒才刷新
