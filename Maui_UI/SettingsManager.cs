
using System.Text.Json;

using ShogiEngine;

namespace MauiUI;

internal class SettingChangedEventArgs
{
    public string SettingName { get; }

    public SettingChangedEventArgs(string settingName) =>
        SettingName = settingName;
}

internal class SettingsManager
{
    public delegate void SettingChangedHandler(object sender, SettingChangedEventArgs e);
    public event SettingChangedHandler? OnSettingChanged;

    public static SettingsManager Default { get; } = new SettingsManager();

    public string PlayerName
    {
        get => Preferences.Default.Get(nameof(PlayerName), "");
        set
        {
            if (value != PlayerName)
            {
                Preferences.Default.Set(nameof(PlayerName), value);
                OnSettingChanged?.Invoke(this, new SettingChangedEventArgs(nameof(PlayerName)));
            }
        }
    }

    public bool AutoRotateBoard
    {
        get => Preferences.Default.Get(nameof(AutoRotateBoard), false);
        set
        {
            Preferences.Default.Set(nameof(AutoRotateBoard), value);
            OnSettingChanged?.Invoke(this, new SettingChangedEventArgs(nameof(AutoRotateBoard)));
        }
    }
}