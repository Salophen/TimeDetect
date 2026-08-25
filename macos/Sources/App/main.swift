import AppKit

// 纯 swiftc 构建的入口：手工创建 NSApplication 并挂上 delegate。
// 用 main.swift 顶层代码而不是 @main struct App，是为了不依赖 Xcode 的 App 生命周期。
//
// 顶层代码在当前 Swift 版本下不是 MainActor 隔离的，但它确实运行在主线程上，
// 所以用 assumeIsolated 显式桥接。delegate 存为全局变量以保持强引用
//（NSApplication.delegate 是 weak）。
let app = NSApplication.shared
let delegate = MainActor.assumeIsolated { AppDelegate() }
app.delegate = delegate
app.run()
