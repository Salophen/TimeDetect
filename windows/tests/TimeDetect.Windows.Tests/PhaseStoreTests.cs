using System;
using System.IO;
using TimeDetect.Services;
using TimeDetect.UI;
using Xunit;

namespace TimeDetect.Windows.Tests;

public sealed class PhaseStoreTests
{
    [Fact]
    public void DisablingOffPeakNotificationAlsoDisablesAdvanceNotification()
    {
        WithSettings((settings, path) =>
        {
            var store = new PhaseStore(settings)
            {
                OffPeakNotificationEnabled = true,
                AdvanceNotificationEnabled = true
            };

            store.OffPeakNotificationEnabled = false;

            Assert.False(store.OffPeakNotificationEnabled);
            Assert.False(store.AdvanceNotificationEnabled);
            Assert.False(new SettingsStore(path).GetBool("advanceNotificationEnabled", true));
        });
    }

    [Fact]
    public void AdvanceNotificationCannotBeEnabledWhileOffPeakNotificationIsDisabled()
    {
        WithSettings((settings, _) =>
        {
            var store = new PhaseStore(settings);

            store.AdvanceNotificationEnabled = true;

            Assert.False(store.AdvanceNotificationEnabled);
        });
    }

    [Fact]
    public void EnablingOffPeakNotificationDoesNotRestorePreviousAdvanceSelection()
    {
        WithSettings((settings, _) =>
        {
            var store = new PhaseStore(settings)
            {
                OffPeakNotificationEnabled = true,
                AdvanceNotificationEnabled = true
            };

            store.OffPeakNotificationEnabled = false;
            store.OffPeakNotificationEnabled = true;

            Assert.True(store.OffPeakNotificationEnabled);
            Assert.False(store.AdvanceNotificationEnabled);
        });
    }

    [Fact]
    public void LoadingInconsistentSettingsRepairsAdvanceNotificationState()
    {
        WithSettings((settings, path) =>
        {
            settings.Set("offPeakNotificationEnabled", false);
            settings.Set("advanceNotificationEnabled", true);

            var store = new PhaseStore(settings);

            Assert.False(store.AdvanceNotificationEnabled);
            Assert.False(new SettingsStore(path).GetBool("advanceNotificationEnabled", true));
        });
    }

    private static void WithSettings(Action<SettingsStore, string> test)
    {
        string directory = Path.Combine(Path.GetTempPath(), "TimeDetect.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "settings.json");
        try
        {
            test(new SettingsStore(path), path);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}