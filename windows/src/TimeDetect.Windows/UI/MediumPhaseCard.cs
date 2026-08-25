using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TimeDetect.Core;

namespace TimeDetect.UI;

/// <summary>中尺寸宽条卡片：左侧主角，右侧时钟 / 价目 / 时间轴。</summary>
public sealed class MediumPhaseCard : Border
{
    private TextBlock _header = null!;
    private TextBlock _persona = null!;
    private TextBlock _latin = null!;
    private PhaseBadge _badge = null!;
    private TextBlock _clock = null!;
    private TextBlock _clockZone = null!;
    private TextBlock _multiplier = null!;
    private TextBlock _billing = null!;
    private DayTimelineBar _timeline = null!;
    private TextBlock _countdown = null!;
    private TextBlock _hint = null!;

    public MediumPhaseCard()
    {
        CornerRadius = new CornerRadius(18);
        BorderThickness = new Thickness(1);
        BorderBrush = Colors.White.BrushWithAlpha(0.10);
        Background = new LinearGradientBrush(
            Color.FromRgb(21, 39, 54),
            Color.FromRgb(10, 20, 31),
            new Point(0, 0), new Point(1, 1));
        Padding = new Thickness(18);
        Child = Build();
    }

    private UIElement Build()
    {
        _header = Ui.Text("TIMEDETECT  /  DEEPSEEK", 9, FontWeights.Bold, Color.FromRgb(127, 164, 180).Brush());
        _persona = Ui.Text("", 38, FontWeights.Bold);
        _latin = Ui.Text("", 9, FontWeights.Heavy, Color.FromRgb(111, 145, 160).Brush());
        _badge = new PhaseBadge();

        _clock = Ui.Text("", 34, FontWeights.Medium, Ui.TextPrimary, mono: true);
        _clockZone = Ui.Text("CST", 9, FontWeights.SemiBold, Ui.TextMuted);
        _multiplier = Ui.Text("", 14, FontWeights.Bold);
        _billing = Ui.Text("", 14, FontWeights.Bold);
        _timeline = new DayTimelineBar { Height = 16, BarHeight = 7 };
        _countdown = Ui.Text("", 11, FontWeights.Bold, Ui.TextSecondary, mono: true);
        _hint = Ui.Text("工作日峰时 09–12 / 14–18", 9, FontWeights.Medium, Ui.TextMuted);

        // 左列
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        left.Children.Add(_header);
        left.Children.Add(Ui.Spacer(12));
        left.Children.Add(_persona);
        _latin.Margin = new Thickness(0, 3, 0, 0);
        left.Children.Add(_latin);
        left.Children.Add(Ui.Spacer(8));
        left.Children.Add(_badge);

        // 右列
        var clockRow = new StackPanel { Orientation = Orientation.Horizontal };
        _clockZone.Margin = new Thickness(6, 0, 0, 0);
        _clockZone.VerticalAlignment = VerticalAlignment.Bottom;
        clockRow.Children.Add(_clock);
        clockRow.Children.Add(_clockZone);

        var priceRow = new StackPanel { Orientation = Orientation.Horizontal };
        priceRow.Children.Add(PriceItem("倍率", _multiplier));
        var billingItem = PriceItem("计费", _billing);
        billingItem.Margin = new Thickness(14, 0, 0, 0);
        priceRow.Children.Add(billingItem);

        var countRow = new DockPanel();
        DockPanel.SetDock(_hint, Dock.Right);
        _hint.VerticalAlignment = VerticalAlignment.Center;
        countRow.Children.Add(_hint);
        countRow.Children.Add(_countdown);

        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        right.Children.Add(clockRow);
        right.Children.Add(Ui.Spacer(8));
        right.Children.Add(priceRow);
        right.Children.Add(Ui.Spacer(14));
        right.Children.Add(_timeline);
        countRow.Margin = new Thickness(0, 6, 0, 0);
        right.Children.Add(countRow);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 2);
        var divider = new Border
        {
            Background = Colors.White.BrushWithAlpha(0.08),
            Width = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Grid.SetColumn(divider, 1);
        grid.Children.Add(left);
        grid.Children.Add(divider);
        grid.Children.Add(right);

        return grid;
    }

    private static FrameworkElement PriceItem(string title, TextBlock value)
    {
        var titleText = Ui.Text(title, 9, FontWeights.Medium, Colors.White.BrushWithAlpha(0.35));
        var stack = new StackPanel();
        stack.Children.Add(titleText);
        stack.Children.Add(value);
        return stack;
    }

    public void Update(PhaseSnapshot snapshot)
    {
        var phase = snapshot.Phase;
        var theme = PhaseTheme.For(phase);

        _persona.Text = phase.PersonaName();
        _persona.Foreground = theme.Gradient;
        _latin.Text = phase.LatinLabel();
        _badge.Update(phase);
        _clock.Text = PeakEngine.ToBeijing(snapshot.Date).ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        _clockZone.Text = snapshot.IsLocalBeijing ? "CST" : "北京";
        _multiplier.Text = phase.MultiplierLabel();
        _multiplier.Foreground = theme.Gradient;
        _billing.Text = phase.PriceKindLabel();
        _billing.Foreground = theme.Gradient;
        _timeline.Snapshot = snapshot;
        _countdown.Text = $"距{phase.Opposite().ShortLabel()} " + PeakEngine.CountdownText(snapshot.SecondsToNextBoundary);
    }
}
