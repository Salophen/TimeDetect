using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using TimeDetect.Core;
using TimeDetect.Interop;
using TimeDetect.Net;
using TimeDetect.UI;

namespace TimeDetect.Windows;

/// <summary>桌面悬浮挂件窗口：无边框透明，可拖动，桌面/置顶两种层级。</summary>
public sealed class FloatingWidgetWindow : Window
{
    private readonly PhaseStore _store;
    private readonly SmallPhaseCard _card;

    public FloatingWidgetWindow(PhaseStore store, DeepSeekStatusManager statusManager, DeepSeekBalanceManager balanceManager)
    {
        _store = store;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Width = 268;
        SizeToContent = SizeToContent.Height;

        _card = new SmallPhaseCard();
        _card.SetAccessory(new DeepSeekOverview(statusManager, balanceManager));
        _card.Update(store.Snapshot);
        _card.HorizontalAlignment = HorizontalAlignment.Stretch;
        Content = _card;

        _card.PreviewMouseLeftButtonDown += (_, _) =>
        {
            try { DragMove(); } catch { /* 忽略拖拽期间的异常 */ }
        };

        ApplyMode(store.FloatingWindowMode);

        store.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PhaseStore.Snapshot))
                _card.Update(store.Snapshot);
            else if (e.PropertyName == nameof(PhaseStore.FloatingWindowMode))
                ApplyMode(store.FloatingWindowMode);
        };
    }

    private void ApplyMode(FloatingWindowMode mode) => Topmost = mode == FloatingWindowMode.AlwaysOnTop;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeMethods.ApplyToolWindowStyle(new WindowInteropHelper(this).Handle, noActivate: true);
    }
}
