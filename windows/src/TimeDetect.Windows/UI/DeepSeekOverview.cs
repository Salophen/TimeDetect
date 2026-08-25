using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TimeDetect.Net;

namespace TimeDetect.UI;

/// <summary>悬浮窗中的最小信息摘要：余额更醒目，服务状态作为辅助信息。</summary>
public sealed class DeepSeekOverview : Grid
{
    private readonly TextBlock _balance;
    private readonly Ellipse _dot;
    private readonly TextBlock _status;
    private readonly DeepSeekStatusManager _statusManager;
    private readonly DeepSeekBalanceManager _balanceManager;

    public DeepSeekOverview(DeepSeekStatusManager statusManager, DeepSeekBalanceManager balanceManager)
    {
        _statusManager = statusManager;
        _balanceManager = balanceManager;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Center;

        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _balance = Ui.Text("", 15, FontWeights.SemiBold, Colors.White.BrushWithAlpha(0.92));
        _balance.VerticalAlignment = VerticalAlignment.Center;
        _dot = new Ellipse { Width = 5, Height = 5, VerticalAlignment = VerticalAlignment.Center };
        _status = Ui.Text("", 9, FontWeights.Medium, Colors.White.BrushWithAlpha(0.58));
        _status.TextAlignment = TextAlignment.Right;
        _status.VerticalAlignment = VerticalAlignment.Center;

        var statusStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        _status.Margin = new Thickness(4, 0, 0, 0);
        statusStack.Children.Add(_dot);
        statusStack.Children.Add(_status);

        SetColumn(_balance, 0);
        SetColumn(statusStack, 2);
        Children.Add(_balance);
        Children.Add(statusStack);

        Refresh();
        statusManager.PropertyChanged += (_, _) => Refresh();
        balanceManager.PropertyChanged += (_, _) => Refresh();
    }

    private void Refresh()
    {
        var balance = _balanceManager.Balance;
        var info = balance == null
            ? null
            : (balance.Balances.FirstOrDefault(b => b.Currency == "CNY") ?? balance.Balances.FirstOrDefault());

        _balance.Text = info == null
            ? (_balanceManager.IsRefreshing ? "余额 …" : "余额 —")
            : "余额 " + info.TotalText;

        var health = _statusManager.Snapshot?.Overall;
        if (health == null)
        {
            _status.Text = _statusManager.IsRefreshing ? "DeepSeek 检测中" : "DeepSeek 状态未知";
            _dot.Fill = _statusManager.IsRefreshing ? Brushes.Blue : Colors.White.BrushWithAlpha(0.3);
            return;
        }

        _status.Text = "DeepSeek " + health.Value.Title();
        _dot.Fill = health.Value switch
        {
            ServiceHealth.Operational => Brushes.Green,
            ServiceHealth.Maintenance => Brushes.Yellow,
            ServiceHealth.Degraded => Brushes.Yellow,
            ServiceHealth.PartialOutage => Brushes.Red,
            ServiceHealth.MajorOutage => Brushes.Red,
            _ => Colors.White.BrushWithAlpha(0.3)
        };
    }
}
