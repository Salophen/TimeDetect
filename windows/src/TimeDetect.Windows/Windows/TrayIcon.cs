using System;
using System.Drawing;
using System.Windows.Forms;

namespace TimeDetect.Windows;

/// <summary>系统托盘图标，等价 macOS 版 NSStatusItem（左键开面板、右键菜单）。</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notify;

    public event Action? LeftClick;
    public event Action? ToggleWidgetRequested;
    public event Action? QuitRequested;

    public TrayIcon()
    {
        _notify = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = "TimeDetect 正在运行",
            Visible = true
        };

        _notify.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) LeftClick?.Invoke();
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("显示/隐藏桌面挂件", null, (_, _) => ToggleWidgetRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出 TimeDetect", null, (_, _) => QuitRequested?.Invoke());
        _notify.ContextMenuStrip = menu;
    }

    public void ShowBalloon(string title, string body)
    {
        _notify.BalloonTipTitle = title;
        _notify.BalloonTipText = body;
        _notify.BalloonTipIcon = ToolTipIcon.Info;
        _notify.ShowBalloonTip(4000);
    }

    public void Dispose() => _notify.Dispose();

    private static Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            // 谷时青绿圆点 + 白色波形，呼应 SF Symbol「waveform.path.ecg」。
            using var brush = new SolidBrush(Color.FromArgb(64, 212, 217));
            g.FillEllipse(brush, 1, 1, 14, 14);
            using var pen = new Pen(Color.White, 1.5f);
            var points = new PointF[]
            {
                new PointF(3, 8), new PointF(5, 8), new PointF(6.5f, 5), new PointF(8, 11),
                new PointF(9.5f, 7), new PointF(13, 8)
            };
            g.DrawLines(pen, points);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
