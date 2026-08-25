using System;
using System.Threading;
using System.Windows;
using TimeDetect.Net;
using TimeDetect.Services;
using TimeDetect.UI;
using TimeDetect.Windows;

namespace TimeDetect;

/// <summary>
/// WPF 应用入口，对应 macOS 版 main.swift + AppDelegate：
/// 建立托盘图标、管理桌面悬浮挂件与详情面板，并启动各低频轮询。
/// </summary>
public partial class App : Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;
    private PhaseStore? _store;
    private TrayIcon? _trayIcon;
    private FloatingWidgetWindow? _floatingWindow;
    private MenuBarPanelWindow? _panel;
    private DeepSeekStatusManager? _statusManager;
    private DeepSeekBalanceManager? _balanceManager;
    private NotificationService? _notificationService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // 单实例：重复启动直接退出，不创建第二个进程。
        _mutex = new Mutex(true, "TimeDetect.SingleInstance", out bool createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
            Shutdown();
            return;
        }

        var settings = new SettingsStore();
        _store = new PhaseStore(settings);
        _statusManager = new DeepSeekStatusManager();
        _balanceManager = new DeepSeekBalanceManager();
        _trayIcon = new TrayIcon();

        _trayIcon.LeftClick += TogglePanel;
        _trayIcon.ToggleWidgetRequested += ToggleWidget;
        _trayIcon.QuitRequested += ShutdownApp;

        _notificationService = new NotificationService(_store, _trayIcon);

        _floatingWindow = new FloatingWidgetWindow(_store, _statusManager, _balanceManager);
        _panel = new MenuBarPanelWindow(_store, _statusManager, _balanceManager);

        _store.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PhaseStore.FloatingWidgetVisible))
                ApplyWidgetVisibility();
        };

        _store.Start();
        _statusManager.Start();
        _balanceManager.Start();
        _notificationService.Start();

        ApplyWidgetVisibility();
    }

    private void ApplyWidgetVisibility()
    {
        if (_store == null || _floatingWindow == null) return;
        if (_store.FloatingWidgetVisible) _floatingWindow.Show();
        else _floatingWindow.Hide();
    }

    private void TogglePanel()
    {
        if (_panel == null) return;
        if (_panel.IsVisible) _panel.Hide();
        else _panel.ShowNearTray();
    }

    private void ToggleWidget()
    {
        if (_store != null) _store.FloatingWidgetVisible = !_store.FloatingWidgetVisible;
    }

    private void ShutdownApp() => Shutdown();

    protected override void OnExit(ExitEventArgs e)
    {
        _notificationService?.Stop();
        _store?.Stop();
        _statusManager?.Stop();
        _balanceManager?.Stop();
        _trayIcon?.Dispose();
        if (_ownsMutex)
        {
            try { _mutex?.ReleaseMutex(); } catch { }
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
