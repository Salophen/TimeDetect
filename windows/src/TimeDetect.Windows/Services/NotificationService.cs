using System;
using System.Windows.Threading;
using TimeDetect.Core;
using TimeDetect.UI;
using TimeDetect.Windows;

namespace TimeDetect.Services;

/// <summary>
/// 谷时通知：按北京时间在工作日 12:00 / 18:00（及可选的提前提醒）触发托盘气泡。
/// 由 1 秒本地定时器驱动，等价 macOS 版 UNCalendarNotificationTrigger 的重复计划。
/// </summary>
public sealed class NotificationService
{
    private readonly PhaseStore _store;
    private readonly TrayIcon _trayIcon;
    private DispatcherTimer? _timer;
    private string? _lastFiredKey;

    public NotificationService(PhaseStore store, TrayIcon trayIcon)
    {
        _store = store;
        _trayIcon = trayIcon;
    }

    public void Start()
    {
        if (_timer != null) return;
        _timer = new DispatcherTimer(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Background,
            (_, _) => Check(),
            Dispatcher.CurrentDispatcher);
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }

    private void Check()
    {
        if (!_store.OffPeakNotificationEnabled) return;
        var now = PeakEngine.ToBeijing(DateTimeOffset.Now);
        int dow = (int)now.DayOfWeek + 1; // 1=周日 .. 7=周六，与计划模型一致

        foreach (var plan in TimeDetectNotificationPlan.OffPeakPlans())
        {
            if (plan.Weekday == dow && plan.Hour == now.Hour && plan.Minute == now.Minute)
                Fire(plan.Identifier, plan.Title, plan.Body);
        }

        if (_store.AdvanceNotificationEnabled)
        {
            foreach (var plan in TimeDetectNotificationPlan.AdvancePlans(_store.AdvanceNotificationMinutes))
            {
                if (plan.Weekday == dow && plan.Hour == now.Hour && plan.Minute == now.Minute)
                    Fire(plan.Identifier, plan.Title, plan.Body);
            }
        }
    }

    private void Fire(string key, string title, string body)
    {
        if (_lastFiredKey == key) return; // 同一分钟内去重
        _lastFiredKey = key;
        _trayIcon.ShowBalloon(title, body);
    }
}
