using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using TimeDetect.Core;
using TimeDetect.Services;

namespace TimeDetect.UI;

/// <summary>全局状态源：1s 本地定时器驱动峰谷快照，设置持久化到 JSON。</summary>
public sealed class PhaseStore : INotifyPropertyChanged
{
    private readonly SettingsStore _settings;
    private DispatcherTimer? _timer;

    private PhaseSnapshot _snapshot = PeakEngine.SnapshotAt(DateTimeOffset.Now);
    private bool _floatingWidgetVisible;
    private FloatingWindowMode _floatingWindowMode;
    private bool _offPeakNotificationEnabled;
    private bool _advanceNotificationEnabled;
    private int _advanceNotificationMinutes;

    public PhaseSnapshot Snapshot
    {
        get => _snapshot;
        private set { _snapshot = value; OnPropertyChanged(nameof(Snapshot)); }
    }

    public bool FloatingWidgetVisible
    {
        get => _floatingWidgetVisible;
        set { if (SetField(ref _floatingWidgetVisible, value)) _settings.Set("floatingWidgetVisible", value); }
    }

    public FloatingWindowMode FloatingWindowMode
    {
        get => _floatingWindowMode;
        set { if (SetField(ref _floatingWindowMode, value)) _settings.Set("floatingWindowMode", value.ToRawValue()); }
    }

    public bool OffPeakNotificationEnabled
    {
        get => _offPeakNotificationEnabled;
        set
        {
            if (!value) AdvanceNotificationEnabled = false;
            if (SetField(ref _offPeakNotificationEnabled, value))
                _settings.Set("offPeakNotificationEnabled", value);
        }
    }

    public bool AdvanceNotificationEnabled
    {
        get => _advanceNotificationEnabled;
        set
        {
            bool effectiveValue = _offPeakNotificationEnabled && value;
            if (SetField(ref _advanceNotificationEnabled, effectiveValue))
                _settings.Set("advanceNotificationEnabled", effectiveValue);
        }
    }

    public int AdvanceNotificationMinutes
    {
        get => _advanceNotificationMinutes;
        set { if (SetField(ref _advanceNotificationMinutes, value)) _settings.Set("advanceNotificationMinutes", value); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public PhaseStore(SettingsStore settings)
    {
        _settings = settings;
        _floatingWidgetVisible = settings.GetBool("floatingWidgetVisible", true);
        _floatingWindowMode = FloatingWindowModeExtensions.FromStored(settings.GetString("floatingWindowMode"));
        _offPeakNotificationEnabled = settings.GetBool("offPeakNotificationEnabled", false);
        bool savedAdvanceEnabled = settings.GetBool("advanceNotificationEnabled", false);
        _advanceNotificationEnabled = _offPeakNotificationEnabled && savedAdvanceEnabled;
        if (savedAdvanceEnabled && !_advanceNotificationEnabled)
            settings.Set("advanceNotificationEnabled", false);
        var minutes = settings.GetInt("advanceNotificationMinutes", 10);
        _advanceNotificationMinutes = Array.IndexOf(new[] { 5, 10, 15, 30 }, minutes) >= 0 ? minutes : 10;
    }

    public void Start()
    {
        if (_timer != null) return;
        _timer = new DispatcherTimer(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Background,
            (_, _) => Tick(),
            Dispatcher.CurrentDispatcher);
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }

    private void Tick() => Snapshot = PeakEngine.SnapshotAt(DateTimeOffset.Now);

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
