using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TimeDetect.Core;

namespace TimeDetect.UI;

/// <summary>小尺寸方形卡片（对应桌面悬浮挂件）。</summary>
public sealed class SmallPhaseCard : Border
{
    private TextBlock _header = null!;
    private PhaseBadge _badge = null!;
    private TextBlock _persona = null!;
    private TextBlock _latin = null!;
    private TextBlock _clock = null!;
    private TextBlock _clockZone = null!;
    private StackPanel _accessoryHost = null!;
    private DayTimelineBar _timeline = null!;
    private TextBlock _countdown = null!;
    private TextBlock _boundary = null!;

    public SmallPhaseCard()
    {
        CornerRadius = new CornerRadius(20);
        BorderThickness = new Thickness(1);
        BorderBrush = Colors.White.BrushWithAlpha(0.10);
        Background = new LinearGradientBrush(
            Color.FromRgb(18, 20, 28).WithAlpha(0.94),
            Color.FromRgb(10, 10, 15).WithAlpha(0.97),
            new Point(0, 0), new Point(1, 1));
        Padding = new Thickness(16, 14, 16, 15);
        Child = Build();
    }

    private UIElement Build()
    {
        _header = Ui.Text("DEEPSEEK", 9, FontWeights.Black, Colors.White.BrushWithAlpha(0.45));
        _badge = new PhaseBadge(compact: true);
        _persona = Ui.Text("", 32, FontWeights.Bold);
        _latin = Ui.Text("", 9, FontWeights.Heavy, Colors.White.BrushWithAlpha(0.32));
        _clock = Ui.Text("", 22, FontWeights.Medium, Colors.White.BrushWithAlpha(0.95), mono: true);
        _clockZone = Ui.Text("北京", 8, FontWeights.SemiBold, Colors.White.BrushWithAlpha(0.35));
        _accessoryHost = new StackPanel();
        _timeline = new DayTimelineBar { Height = 13 };
        _countdown = Ui.Text("", 10, FontWeights.SemiBold, Colors.White.BrushWithAlpha(0.72), mono: true);
        _boundary = Ui.Text("", 9, FontWeights.Medium, Colors.White.BrushWithAlpha(0.38), mono: true);

        var headerRow = new Grid();
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_header, 0);
        Grid.SetColumn(_badge, 2);
        headerRow.Children.Add(_header);
        headerRow.Children.Add(_badge);

        var clockRow = new StackPanel { Orientation = Orientation.Horizontal };
        _clockZone.Margin = new Thickness(5, 0, 0, 0);
        _clockZone.VerticalAlignment = VerticalAlignment.Bottom;
        clockRow.Children.Add(_clock);
        clockRow.Children.Add(_clockZone);

        var footer = new DockPanel();
        DockPanel.SetDock(_boundary, Dock.Right);
        _boundary.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(_boundary);
        footer.Children.Add(_countdown);

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        stack.Children.Add(headerRow);
        stack.Children.Add(Ui.Spacer(8));
        stack.Children.Add(_persona);
        _latin.Margin = new Thickness(0, 3, 0, 0);
        stack.Children.Add(_latin);
        stack.Children.Add(Ui.Spacer(10));
        stack.Children.Add(clockRow);
        _accessoryHost.Margin = new Thickness(0, 10, 0, 0);
        stack.Children.Add(_accessoryHost);
        stack.Children.Add(Ui.Spacer(10));
        stack.Children.Add(_timeline);
        stack.Children.Add(footer);

        return stack;
    }

    public void Update(PhaseSnapshot snapshot)
    {
        var phase = snapshot.Phase;
        var theme = PhaseTheme.For(phase);

        _badge.Update(phase);
        _persona.Text = phase.PersonaName();
        _persona.Foreground = theme.Gradient;
        _latin.Text = phase.LatinLabel();
        _clock.Text = PeakEngine.ToBeijing(snapshot.Date).ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        _clockZone.Text = snapshot.IsLocalBeijing ? "" : "北京";
        _timeline.Snapshot = snapshot;
        _countdown.Text = $"转{phase.Opposite().ShortLabel()} " + PeakEngine.CountdownText(snapshot.SecondsToNextBoundary);
        _boundary.Text = PeakEngine.ClockText((int)PeakEngine.BeijingMinutes(snapshot.NextBoundary));
    }

    public void SetAccessory(UIElement? accessory)
    {
        _accessoryHost.Children.Clear();
        if (accessory != null) _accessoryHost.Children.Add(accessory);
    }
}
