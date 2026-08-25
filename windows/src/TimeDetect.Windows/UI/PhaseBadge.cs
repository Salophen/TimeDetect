using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TimeDetect.Core;

namespace TimeDetect.UI;

/// <summary>呼吸状态徽章：● 谷时 · 5 折</summary>
public sealed class PhaseBadge : Border
{
    private readonly Ellipse _dot;
    private readonly TextBlock _label;
    private readonly bool _compact;

    public PhaseBadge(bool compact = false)
    {
        _compact = compact;
        CornerRadius = new CornerRadius(999);
        BorderThickness = new Thickness(0.8);

        _dot = new Ellipse
        {
            Width = compact ? 5 : 6,
            Height = compact ? 5 : 6,
            VerticalAlignment = VerticalAlignment.Center
        };
        _label = Ui.Text("", compact ? 10 : 11, FontWeights.SemiBold, Colors.White.BrushWithAlpha(0.92));
        _label.Margin = new Thickness(compact ? 4 : 5, 0, 0, 0);
        _label.VerticalAlignment = VerticalAlignment.Center;

        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        stack.Children.Add(_dot);
        stack.Children.Add(_label);

        Padding = new Thickness(compact ? 7 : 9, compact ? 3 : 4, compact ? 7 : 9, compact ? 3 : 4);
        Child = stack;
    }

    public void Update(PricePhase phase)
    {
        var theme = PhaseTheme.For(phase);
        _dot.Fill = theme.Gradient;
        Background = theme.TrackDim;
        BorderBrush = theme.Glow.WithAlpha(0.35);
        _label.Text = $"{phase.ShortLabel()} · {(_compact ? phase.MultiplierLabel() : phase.PriceLabel())}";
    }
}
