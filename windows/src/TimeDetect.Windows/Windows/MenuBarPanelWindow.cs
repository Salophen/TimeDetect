using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using TimeDetect.Interop;
using TimeDetect.Net;
using TimeDetect.UI;

namespace TimeDetect.Windows;

/// <summary>托盘旁弹出的详情面板（无边框，失焦自动收起），等价 macOS 版 NSPopover。</summary>
public sealed class MenuBarPanelWindow : Window
{
    public MenuBarPanelWindow(PhaseStore store, DeepSeekStatusManager statusManager, DeepSeekBalanceManager balanceManager)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Width = 456;
        SizeToContent = SizeToContent.Height;
        Topmost = true;

        Content = new PanelContent(store, statusManager, balanceManager);
        Effect = new DropShadowEffect
        {
            Color = Colors.Black,
            Direction = 270,
            ShadowDepth = 10,
            BlurRadius = 28,
            Opacity = 0.46
        };

        Deactivated += (_, _) => Hide();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeMethods.ApplyToolWindowStyle(new WindowInteropHelper(this).Handle, noActivate: false);
    }

    /// <summary>在任务栏（默认托盘位置）上方弹出。</summary>
    public void ShowNearTray()
    {
        if (!IsVisible)
        {
            Show();
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 8;
            Top = area.Bottom - ActualHeight - 8;
        }
        Activate();
    }
}
