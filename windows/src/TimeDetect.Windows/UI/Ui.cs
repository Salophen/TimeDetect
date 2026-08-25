using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TimeDetect.UI;

internal static class Ui
{
    public static readonly FontFamily Round = new FontFamily("Segoe UI");
    public static readonly FontFamily Mono = new FontFamily("Consolas");
    public static readonly SolidColorBrush TextPrimary = Color.FromRgb(241, 245, 249).Brush();
    public static readonly SolidColorBrush TextSecondary = Color.FromRgb(148, 163, 184).Brush();
    public static readonly SolidColorBrush TextMuted = Color.FromRgb(100, 116, 139).Brush();
    public static readonly SolidColorBrush PanelBackground = Color.FromRgb(9, 15, 27).Brush();
    public static readonly SolidColorBrush CardBackground = Color.FromRgb(16, 24, 39).Brush();
    public static readonly SolidColorBrush CardBorder = Color.FromRgb(38, 55, 78).Brush();
    public static readonly SolidColorBrush Cyan = Color.FromRgb(45, 215, 194).Brush();
    public static readonly SolidColorBrush ControlBackground = Color.FromRgb(24, 36, 54).Brush();
    public static readonly SolidColorBrush ControlBorder = Color.FromRgb(58, 83, 113).Brush();
    public static readonly SolidColorBrush ControlHover = Color.FromRgb(31, 48, 70).Brush();
    public static readonly SolidColorBrush ButtonBackground = Color.FromRgb(27, 42, 61).Brush();
    public static readonly SolidColorBrush ButtonBorder = Color.FromRgb(64, 91, 122).Brush();

    public static TextBlock Text(string text, double size, FontWeight weight, Brush? foreground = null, bool mono = false)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            FontFamily = mono ? Mono : Round,
            Foreground = foreground ?? Brushes.White
        };
    }

    public static FrameworkElement Spacer(double height) => new FrameworkElement { Height = height };

    public static Border Card(FrameworkElement content, double radius = 14, Thickness? padding = null)
    {
        return new Border
        {
            Padding = padding ?? new Thickness(14),
            CornerRadius = new CornerRadius(radius),
            Background = CardBackground,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            Child = content
        };
    }

    public static TextBlock Eyebrow(string text) =>
        Text(text.ToUpperInvariant(), 10, FontWeights.Bold, TextMuted);

    public static Button Button(string text, bool primary = false)
    {
        var button = new Button
        {
            Content = text,
            FontFamily = Round,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = primary ? Color.FromRgb(5, 23, 28).Brush() : TextSecondary,
            Background = primary ? PrimaryButtonBrush() : ButtonBackground,
            BorderBrush = primary ? Cyan : ButtonBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 8, 14, 8),
            MinHeight = 34,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Template = ButtonTemplate()
        };
        return button;
    }

    public static void StyleTextBox(TextBox textBox)
    {
        textBox.FontFamily = Mono;
        textBox.FontSize = 11;
        textBox.Foreground = TextPrimary;
        textBox.Background = Color.FromRgb(9, 15, 27).Brush();
        textBox.BorderBrush = Color.FromRgb(51, 72, 98).Brush();
        textBox.BorderThickness = new Thickness(1);
        textBox.Padding = new Thickness(10, 8, 10, 8);
        textBox.Template = TextBoxTemplate();
    }

    public static void StyleComboBox(ComboBox comboBox)
    {
        comboBox.FontFamily = Round;
        comboBox.FontSize = 11;
        comboBox.Foreground = TextPrimary;
        comboBox.Background = ControlBackground;
        comboBox.BorderBrush = ControlBorder;
        comboBox.BorderThickness = new Thickness(1);
        comboBox.Padding = new Thickness(10, 0, 6, 0);
        comboBox.Height = 34;
        comboBox.HorizontalContentAlignment = HorizontalAlignment.Left;
        comboBox.VerticalContentAlignment = VerticalAlignment.Center;
        comboBox.Template = ComboBoxTemplate();
        comboBox.ItemContainerStyle = ComboBoxItemStyle();
    }

    public static void StyleCheckBox(CheckBox checkBox)
    {
        checkBox.Template = CheckBoxTemplate();
        checkBox.Padding = new Thickness(0);
        checkBox.Height = 28;
        checkBox.HorizontalContentAlignment = HorizontalAlignment.Left;
        checkBox.VerticalContentAlignment = VerticalAlignment.Center;
    }

    public static Style ScrollBarStyle()
    {
        var style = new Style(typeof(ScrollBar));
        style.Setters.Add(new Setter(Control.WidthProperty, 7d));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.TemplateProperty, ScrollBarTemplate()));
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(Control.OpacityProperty, 0.35));
        style.Triggers.Add(disabled);
        return style;
    }

    public static Ellipse Dot(Brush fill, double size = 8) =>
        new Ellipse { Width = size, Height = size, Fill = fill, VerticalAlignment = VerticalAlignment.Center };

    private static LinearGradientBrush PrimaryButtonBrush() =>
        new LinearGradientBrush(
            Color.FromRgb(72, 229, 207),
            Color.FromRgb(39, 190, 187),
            new Point(0, 0), new Point(0, 1));

    private static ControlTemplate ButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        presenter.SetValue(ContentPresenter.ContentSourceProperty, "Content");
        border.AppendChild(presenter);
        template.VisualTree = border;
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BorderBrushProperty, Cyan));
        hover.Setters.Add(new Setter(Control.BackgroundProperty, ControlHover));
        template.Triggers.Add(hover);
        var pressed = new Trigger { Property = System.Windows.Controls.Button.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(Control.OpacityProperty, 0.78));
        template.Triggers.Add(pressed);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(Control.OpacityProperty, 0.42));
        template.Triggers.Add(disabled);
        return template;
    }

    private static ControlTemplate TextBoxTemplate()
    {
        var template = new ControlTemplate(typeof(TextBox));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
        scroll.Name = "PART_ContentHost";
        border.AppendChild(scroll);
        template.VisualTree = border;
        var focused = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        focused.Setters.Add(new Setter(Control.BorderBrushProperty, Cyan));
        template.Triggers.Add(focused);
        return template;
    }

    private static ControlTemplate CheckBoxTemplate()
    {
        var template = new ControlTemplate(typeof(CheckBox));
        var root = new FrameworkElementFactory(typeof(StackPanel));
        root.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var box = new FrameworkElementFactory(typeof(Border));
        box.Name = "PART_Box";
        box.SetValue(FrameworkElement.WidthProperty, 19d);
        box.SetValue(FrameworkElement.HeightProperty, 19d);
        box.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        box.SetValue(Border.BackgroundProperty, PanelBackground);
        box.SetValue(Border.BorderBrushProperty, ControlBorder);
        box.SetValue(Border.BorderThicknessProperty, new Thickness(1.2));
        box.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        var check = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
        check.Name = "PART_CheckMark";
        check.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M 3,9 L 7.2,13.2 L 16,4.5"));
        check.SetValue(System.Windows.Shapes.Path.StrokeProperty, Color.FromRgb(5, 23, 28).Brush());
        check.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty, 2.1d);
        check.SetValue(System.Windows.Shapes.Path.StrokeStartLineCapProperty, PenLineCap.Round);
        check.SetValue(System.Windows.Shapes.Path.StrokeEndLineCapProperty, PenLineCap.Round);
        check.SetValue(System.Windows.Shapes.Path.VisibilityProperty, Visibility.Collapsed);
        box.AppendChild(check);
        root.AppendChild(box);

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.MarginProperty, new Thickness(9, 0, 0, 0));
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetValue(ContentPresenter.ContentSourceProperty, "Content");
        root.AppendChild(content);
        template.VisualTree = root;

        var checkedTrigger = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        checkedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, Cyan, "PART_Box"));
        checkedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, Cyan, "PART_Box"));
        checkedTrigger.Setters.Add(new Setter(System.Windows.Shapes.Path.VisibilityProperty, Visibility.Visible, "PART_CheckMark"));
        template.Triggers.Add(checkedTrigger);

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, Cyan, "PART_Box"));
        template.Triggers.Add(hoverTrigger);

        var disabledTrigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabledTrigger.Setters.Add(new Setter(Control.OpacityProperty, 0.45));
        template.Triggers.Add(disabledTrigger);
        return template;
    }

    private static ControlTemplate ComboBoxTemplate()
    {
        var template = new ControlTemplate(typeof(ComboBox));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "PART_Border";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));

        var grid = new FrameworkElementFactory(typeof(Grid));
        var contentColumn = new FrameworkElementFactory(typeof(ColumnDefinition));
        contentColumn.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
        grid.AppendChild(contentColumn);
        var arrowColumn = new FrameworkElementFactory(typeof(ColumnDefinition));
        arrowColumn.SetValue(ColumnDefinition.WidthProperty, new GridLength(28));
        grid.AppendChild(arrowColumn);

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(Grid.ColumnProperty, 0);
        content.SetValue(ContentPresenter.ContentSourceProperty, "SelectionBoxItem");
        content.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemTemplateProperty));
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetValue(FrameworkElement.MarginProperty, new Thickness(10, 0, 3, 0));
        grid.AppendChild(content);

        var toggle = new FrameworkElementFactory(typeof(ToggleButton));
        toggle.Name = "PART_ToggleButton";
        toggle.SetValue(Grid.ColumnProperty, 1);
        toggle.SetValue(Control.FocusableProperty, false);
        toggle.SetBinding(ToggleButton.IsCheckedProperty, DropDownBinding());
        toggle.SetValue(ToggleButton.TemplateProperty, ComboArrowTemplate());
        grid.AppendChild(toggle);
        border.AppendChild(grid);

        var popup = new FrameworkElementFactory(typeof(Popup));
        popup.Name = "PART_Popup";
        popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
        popup.SetValue(Popup.AllowsTransparencyProperty, true);
        popup.SetValue(Popup.FocusableProperty, false);
        popup.SetBinding(Popup.IsOpenProperty, DropDownBinding());
        popup.SetBinding(Popup.PlacementTargetProperty, new Binding
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        popup.SetValue(Popup.HorizontalOffsetProperty, -1d);
        var popupBorder = new FrameworkElementFactory(typeof(Border));
        popupBorder.SetValue(Border.BackgroundProperty, CardBackground);
        popupBorder.SetValue(Border.BorderBrushProperty, ControlBorder);
        popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        popupBorder.SetValue(Border.PaddingProperty, new Thickness(3));
        var items = new FrameworkElementFactory(typeof(ItemsPresenter));
        items.Name = "ItemsPresenter";
        popupBorder.AppendChild(items);
        popup.AppendChild(popupBorder);

        var root = new FrameworkElementFactory(typeof(Grid));
        root.AppendChild(border);
        root.AppendChild(popup);
        template.VisualTree = root;

        var focused = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        focused.Setters.Add(new Setter(Border.BorderBrushProperty, Cyan, "PART_Border"));
        template.Triggers.Add(focused);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(Control.OpacityProperty, 0.45));
        template.Triggers.Add(disabled);
        return template;
    }

    private static ControlTemplate ComboArrowTemplate()
    {
        var template = new ControlTemplate(typeof(ToggleButton));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var arrow = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
        arrow.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M 3,5 L 7,9 L 11,5"));
        arrow.SetValue(System.Windows.Shapes.Path.StrokeProperty, TextSecondary);
        arrow.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty, 1.6d);
        arrow.SetValue(System.Windows.Shapes.Path.StrokeStartLineCapProperty, PenLineCap.Round);
        arrow.SetValue(System.Windows.Shapes.Path.StrokeEndLineCapProperty, PenLineCap.Round);
        arrow.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        arrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(arrow);
        template.VisualTree = border;
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(System.Windows.Shapes.Path.StrokeProperty, Cyan));
        template.Triggers.Add(hover);
        return template;
    }

    private static Binding DropDownBinding() => new Binding(nameof(ComboBox.IsDropDownOpen))
    {
        RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent),
        Mode = BindingMode.TwoWay
    };

    private static Style ComboBoxItemStyle()
    {
        var style = new Style(typeof(ComboBoxItem));
        style.Setters.Add(new Setter(Control.ForegroundProperty, TextPrimary));
        style.Setters.Add(new Setter(Control.FontFamilyProperty, Round));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 11d));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 7, 9, 7)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.TemplateProperty, ComboBoxItemTemplate()));
        return style;
    }

    private static ControlTemplate ComboBoxItemTemplate()
    {
        var template = new ControlTemplate(typeof(ComboBoxItem));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "PART_ItemBorder";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.ContentSourceProperty, "Content");
        border.AppendChild(content);
        template.VisualTree = border;
        var highlight = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
        highlight.Setters.Add(new Setter(Control.BackgroundProperty, ControlHover));
        template.Triggers.Add(highlight);
        var selected = new Trigger { Property = Selector.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.BackgroundProperty, Color.FromRgb(34, 67, 78).Brush()));
        template.Triggers.Add(selected);
        return template;
    }

    private static ControlTemplate ScrollBarTemplate()
    {
        // Track.Thumb is a normal CLR property (not a dependency property), so
        // construct the property-element form through XAML instead of SetValue.
        const string templateXaml = """
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             TargetType="{x:Type ScrollBar}">
              <Grid Background="Transparent">
                <Track x:Name="PART_Track" Margin="1,0,1,0" Orientation="{TemplateBinding Orientation}" IsDirectionReversed="True">
                  <Track.DecreaseRepeatButton>
                    <RepeatButton Command="ScrollBar.PageUpCommand" Background="Transparent" BorderThickness="0" />
                  </Track.DecreaseRepeatButton>
                  <Track.Thumb>
                    <Thumb Background="#405A78" BorderBrush="Transparent" BorderThickness="0">
                      <Thumb.Template>
                        <ControlTemplate TargetType="{x:Type Thumb}">
                          <Border CornerRadius="4" Margin="0,1"
                                  Background="{TemplateBinding Background}"
                                  BorderBrush="{TemplateBinding BorderBrush}"
                                  BorderThickness="{TemplateBinding BorderThickness}" />
                          <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                              <Setter Property="Background" Value="#2DD7C2" />
                            </Trigger>
                            <Trigger Property="IsDragging" Value="True">
                              <Setter Property="Background" Value="#55E6D0" />
                            </Trigger>
                          </ControlTemplate.Triggers>
                        </ControlTemplate>
                      </Thumb.Template>
                    </Thumb>
                  </Track.Thumb>
                  <Track.IncreaseRepeatButton>
                    <RepeatButton Command="ScrollBar.PageDownCommand" Background="Transparent" BorderThickness="0" />
                  </Track.IncreaseRepeatButton>
                </Track>
              </Grid>
            </ControlTemplate>
            """;
        return (ControlTemplate)System.Windows.Markup.XamlReader.Parse(templateXaml);
    }

}
