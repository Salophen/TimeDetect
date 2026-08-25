using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TimeDetect.Core;
using TimeDetect.Net;
using TimeDetect.Services;

namespace TimeDetect.UI;

/// <summary>托盘详情面板内容：卡片 + 时段说明 + 服务状态 + 余额 + 开关。</summary>
public sealed class PanelContent : Border
{
    private readonly PhaseStore _store;
    private readonly DeepSeekStatusManager _statusManager;
    private readonly DeepSeekBalanceManager _balanceManager;

    private readonly MediumPhaseCard _card;
    private readonly TextBlock _statusOverall;
    private readonly TextBlock _statusApi;
    private readonly TextBlock _statusChat;
    private readonly TextBlock _statusUpdated;
    private readonly TextBlock _balanceValue;
    private readonly TextBlock _balanceState;
    private readonly TextBox _apiKeyInput;
    private readonly StackPanel _apiKeyEditorHost;
    private readonly CheckBox _showWidget;
    private readonly ComboBox _modeCombo;
    private readonly CheckBox _offPeakNotify;
    private readonly CheckBox _advanceNotify;
    private readonly ComboBox _advanceMinutes;
    private readonly CheckBox _launchAtLogin;

    public PanelContent(PhaseStore store, DeepSeekStatusManager statusManager, DeepSeekBalanceManager balanceManager)
    {
        _store = store;
        _statusManager = statusManager;
        _balanceManager = balanceManager;

        Background = Ui.PanelBackground;
        BorderBrush = Color.FromRgb(32, 49, 70).Brush();
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(18);
        Padding = new Thickness(18);

        _card = new MediumPhaseCard { Height = 178, HorizontalAlignment = HorizontalAlignment.Stretch };
        _card.Update(store.Snapshot);

        _statusOverall = Row("DeepSeek 整体服务", "—");
        _statusApi = Row("API 服务", "—");
        _statusChat = Row("网页对话服务", "—");
        _statusUpdated = Ui.Text("", 9, FontWeights.Medium, Colors.White.BrushWithAlpha(0.35));

        _balanceValue = Ui.Text("", 28, FontWeights.Bold, Ui.TextPrimary, mono: true);
        _balanceState = Ui.Text("", 10, FontWeights.SemiBold, Ui.TextSecondary);
        _apiKeyInput = new TextBox { Width = 280 };
        Ui.StyleTextBox(_apiKeyInput);
        _apiKeyEditorHost = new StackPanel();

        _showWidget = Check("显示桌面悬浮挂件", store.FloatingWidgetVisible);
        _modeCombo = new ComboBox { Width = 128 };
        Ui.StyleComboBox(_modeCombo);
        _modeCombo.Items.Add("桌面模式");
        _modeCombo.Items.Add("始终置顶");
        _modeCombo.SelectedIndex = store.FloatingWindowMode == FloatingWindowMode.AlwaysOnTop ? 1 : 0;

        _offPeakNotify = Check("谷时开始提醒", store.OffPeakNotificationEnabled);
        _advanceNotify = Check("提前提醒", store.AdvanceNotificationEnabled);
        _advanceMinutes = new ComboBox { Width = 90 };
        Ui.StyleComboBox(_advanceMinutes);
        foreach (var m in new[] { 5, 10, 15, 30 }) _advanceMinutes.Items.Add($"{m} 分钟");
        _advanceMinutes.SelectedIndex = Array.IndexOf(new[] { 5, 10, 15, 30 }, store.AdvanceNotificationMinutes);
        _launchAtLogin = Check("登录 Windows 时自动启动", LaunchAtLoginService.IsEnabled());

        RefreshNotificationControls();
        Child = Build();
        WireEvents();

        store.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PhaseStore.Snapshot)) _card.Update(store.Snapshot);
            if (e.PropertyName == nameof(PhaseStore.OffPeakNotificationEnabled) ||
                e.PropertyName == nameof(PhaseStore.AdvanceNotificationEnabled))
                RefreshNotificationControls();
        };
        statusManager.PropertyChanged += (_, _) => RefreshStatus();
        balanceManager.PropertyChanged += (_, _) => RefreshBalance();
        RefreshStatus();
        RefreshBalance();
    }

    private UIElement Build()
    {
        var stack = new StackPanel();
        var heading = new Grid { Margin = new Thickness(2, 0, 2, 16) };
        var titleStack = new StackPanel();
        titleStack.Children.Add(Ui.Text("TimeDetect", 17, FontWeights.Bold, Ui.TextPrimary));
        titleStack.Children.Add(Ui.Text("北京时间 · DeepSeek 峰谷时段监测", 10, FontWeights.Medium, Ui.TextMuted));
        heading.Children.Add(titleStack);
        var live = Ui.Text("●  LIVE", 9, FontWeights.Bold, Ui.Cyan);
        live.HorizontalAlignment = HorizontalAlignment.Right;
        live.VerticalAlignment = VerticalAlignment.Center;
        heading.Children.Add(live);
        stack.Children.Add(heading);
        stack.Children.Add(_card);
        stack.Children.Add(Ui.Spacer(14));
        stack.Children.Add(Section("今日时段", "北京时间 / 工作日峰时 09–12、14–18", ScheduleLegend()));
        stack.Children.Add(Ui.Spacer(10));
        stack.Children.Add(Section("DeepSeek 服务", "官方状态页 · 自动检查", StatusSection()));
        stack.Children.Add(Ui.Spacer(10));
        stack.Children.Add(Section("API 账户", "余额仅来自 DeepSeek 官方接口", BalanceSection()));
        stack.Children.Add(Ui.Spacer(10));
        stack.Children.Add(Section("偏好设置", "通知、挂件与启动选项", ControlsSection()));

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 600,
            Content = stack
        };
        scrollViewer.Resources.Add(typeof(ScrollBar), Ui.ScrollBarStyle());
        return scrollViewer;
    }
    private FrameworkElement ScheduleLegend()
    {
        var stack = new StackPanel();
        var windows = PeakEngine.IsWeekday(_store.Snapshot.Date)
            ? PeakEngine.DayWindows
            : new[] { new PhaseWindow(0, 1440, PricePhase.OffPeak) };
        int index = 0;
        foreach (var w in windows)
        {
            var theme = PhaseTheme.For(w.Phase);
            var dot = new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, Fill = theme.Gradient, VerticalAlignment = VerticalAlignment.Center };
            var text = Ui.Text($"{PeakEngine.ClockText(w.StartMinute)}–{PeakEngine.ClockText(w.EndMinute)}",
                11, FontWeights.SemiBold, Ui.TextPrimary, mono: true);
            text.Margin = new Thickness(8, 0, 0, 0);
            var label = Ui.Text(w.Phase.ShortLabel(), 10, FontWeights.Medium, Ui.TextSecondary);
            label.Margin = new Thickness(10, 0, 0, 0);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, index == 0 ? 0 : 8, 0, 0) };
            row.Children.Add(dot);
            row.Children.Add(text);
            row.Children.Add(label);
            stack.Children.Add(row);
            index++;
        }
        return stack;
    }

    private FrameworkElement StatusSection()
    {
        var stack = new StackPanel();
        stack.Children.Add(_statusOverall);
        stack.Children.Add(_statusApi);
        stack.Children.Add(_statusChat);
        _statusUpdated.Margin = new Thickness(0, 4, 0, 0);
        stack.Children.Add(_statusUpdated);
        var refresh = Ui.Button("立即刷新");
        refresh.Click += (_, _) => _statusManager.Refresh();
        refresh.Margin = new Thickness(0, 10, 0, 0);
        stack.Children.Add(refresh);
        return stack;
    }

    private FrameworkElement BalanceSection()
    {
        var stack = new StackPanel();
        stack.Children.Add(_balanceValue);
        _balanceState.Margin = new Thickness(0, 3, 0, 0);
        stack.Children.Add(_balanceState);
        _apiKeyEditorHost.Margin = new Thickness(0, 12, 0, 0);
        stack.Children.Add(_apiKeyEditorHost);
        return stack;
    }

    private FrameworkElement ControlsSection()
    {
        var stack = new StackPanel();
        stack.Children.Add(_showWidget);
        stack.Children.Add(PreferenceRow(
            Ui.Text("挂件层级", 11, FontWeights.SemiBold, Ui.TextPrimary),
            _modeCombo,
            8));
        stack.Children.Add(_offPeakNotify);
        stack.Children.Add(PreferenceRow(_advanceNotify, _advanceMinutes, 4));
        stack.Children.Add(_launchAtLogin);
        return stack;
    }

    private static Grid PreferenceRow(UIElement label, UIElement control, double topMargin)
    {
        var row = new Grid { Margin = new Thickness(0, topMargin, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(label, 0);
        Grid.SetColumn(control, 1);
        row.Children.Add(label);
        row.Children.Add(control);
        return row;
    }

    private void WireEvents()
    {
        _showWidget.Checked += (_, _) => _store.FloatingWidgetVisible = true;
        _showWidget.Unchecked += (_, _) => _store.FloatingWidgetVisible = false;

        _modeCombo.SelectionChanged += (_, _) =>
            _store.FloatingWindowMode = _modeCombo.SelectedIndex == 1 ? FloatingWindowMode.AlwaysOnTop : FloatingWindowMode.Desktop;

        _offPeakNotify.Checked += (_, _) => _store.OffPeakNotificationEnabled = true;
        _offPeakNotify.Unchecked += (_, _) => _store.OffPeakNotificationEnabled = false;

        _advanceNotify.Checked += (_, _) => _store.AdvanceNotificationEnabled = true;
        _advanceNotify.Unchecked += (_, _) => _store.AdvanceNotificationEnabled = false;

        _advanceMinutes.SelectionChanged += (_, _) =>
        {
            int idx = _advanceMinutes.SelectedIndex;
            if (idx >= 0) _store.AdvanceNotificationMinutes = new[] { 5, 10, 15, 30 }[idx];
        };

        _launchAtLogin.Checked += (_, _) => LaunchAtLoginService.SetEnabled(true);
        _launchAtLogin.Unchecked += (_, _) => LaunchAtLoginService.SetEnabled(false);
    }

    private void RefreshNotificationControls()
    {
        bool offPeakEnabled = _store.OffPeakNotificationEnabled;
        bool advanceEnabled = _store.AdvanceNotificationEnabled;

        _offPeakNotify.IsChecked = offPeakEnabled;
        _advanceNotify.IsChecked = advanceEnabled;
        _advanceNotify.IsEnabled = offPeakEnabled;
        _advanceMinutes.IsEnabled = offPeakEnabled && advanceEnabled;
    }
    private void RefreshStatus()
    {
        var s = _statusManager.Snapshot;
        SetRow(_statusOverall, "DeepSeek 整体服务", s?.Overall);
        SetRow(_statusApi, "API 服务", s?.Services.FirstOrDefault(x => x.Kind == MonitoredServiceKind.Api)?.Health);
        SetRow(_statusChat, "网页对话服务", s?.Services.FirstOrDefault(x => x.Kind == MonitoredServiceKind.WebChat)?.Health);
        _statusUpdated.Text = _statusManager.LastUpdated == null
            ? "尚无成功更新"
            : "更新于 " + _statusManager.LastUpdated.Value.LocalDateTime.ToString("HH:mm");
    }

    private void RefreshBalance()
    {
        var balance = _balanceManager.Balance;
        var info = balance == null
            ? null
            : (balance.Balances.FirstOrDefault(b => b.Currency == "CNY") ?? balance.Balances.FirstOrDefault());
        _balanceValue.Text = info == null ? (_balanceManager.IsRefreshing ? "余额 …" : "余额 —") : info.TotalText;
        _balanceState.Text = _balanceManager.State.Message();

        _apiKeyEditorHost.Children.Clear();
        if (_balanceManager.IsConfigured)
        {
            _apiKeyEditorHost.Children.Add(Ui.Text(
                $"已连接  ·  API Key •••• {_balanceManager.KeySuffix ?? ""}", 10, FontWeights.Medium, Ui.TextSecondary));
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            var change = Ui.Button("更换");
            change.Click += (_, _) => ShowKeyEditor();
            var delete = Ui.Button("删除");
            delete.Margin = new Thickness(8, 0, 0, 0);
            delete.Click += async (_, _) => { await _balanceManager.DeleteKeyAsync(); };
            actions.Children.Add(change);
            actions.Children.Add(delete);
            _apiKeyEditorHost.Children.Add(actions);
        }
        else if (_balanceManager.IsDeletingKey)
        {
            _apiKeyEditorHost.Children.Add(Ui.Text("正在删除 API Key…", 10, FontWeights.Medium, Ui.TextMuted));
        }
        else
        {
            ShowKeyEditor();
        }
    }

    private void ShowKeyEditor()
    {
        _apiKeyEditorHost.Children.Clear();
        _apiKeyInput.Clear();
        _apiKeyEditorHost.Children.Add(_apiKeyInput);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        var save = Ui.Button("保存并验证", primary: true);
        save.Click += async (_, _) => { await _balanceManager.SaveAndValidateAsync(_apiKeyInput.Text); };
        row.Children.Add(save);
        _apiKeyEditorHost.Children.Add(row);
    }

    private void SetRow(TextBlock row, string title, ServiceHealth? health)
    {
        if (health == null)
        {
            row.Text = $"○   {title}";
            row.Foreground = Ui.TextMuted;
            return;
        }
        var h = health.Value;
        string mark = h == ServiceHealth.Operational ? "●" : (h == ServiceHealth.Unknown ? "○" : "▲");
        row.Text = $"{mark}   {title}   ·   {h.Title()}";
        row.Foreground = h == ServiceHealth.Operational ? Ui.Cyan
            : h == ServiceHealth.Unknown ? Ui.TextMuted
            : Color.FromRgb(251, 146, 60).Brush();
    }

    private static Border Section(string title, string subtitle, FrameworkElement content)
    {
        var header = Ui.Text(title, 13, FontWeights.Bold, Ui.TextPrimary);
        var description = Ui.Text(subtitle, 9, FontWeights.Medium, Ui.TextMuted);
        description.Margin = new Thickness(0, 2, 0, 0);
        content.Margin = new Thickness(0, 12, 0, 0);
        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(description);
        stack.Children.Add(content);
        return new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(16),
            Background = Ui.CardBackground,
            BorderBrush = Ui.CardBorder,
            BorderThickness = new Thickness(1),
            Child = stack
        };
    }

    private static TextBlock Row(string title, string placeholder)
    {
        var tb = Ui.Text($"{placeholder}   {title}", 11, FontWeights.SemiBold, Ui.TextSecondary);
        tb.Margin = new Thickness(0, 3, 0, 3);
        return tb;
    }

    private static CheckBox Check(string text, bool isChecked)
    {
        var checkBox = new CheckBox
        {
            Content = text,
            Foreground = Ui.TextSecondary,
            FontFamily = Ui.Round,
            FontSize = 11,
            FontWeight = FontWeights.Medium,
            IsChecked = isChecked,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Ui.StyleCheckBox(checkBox);
        return checkBox;
    }

}
