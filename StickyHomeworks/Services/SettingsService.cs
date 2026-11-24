using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Hosting;
using StickyHomeworks.Models;

namespace StickyHomeworks.Services;

public class SettingsService : ObservableRecipient, IHostedService
{
    private const string SettingsFileName = "Settings.json";

    /// <summary>
    /// 保存延迟。拖动滑块等操作会连续产生变更，延迟合并写入避免频繁写盘。
    /// </summary>
    private const int SaveDelayMilliseconds = 400;

    private readonly object _saveLock = new();
    private readonly DispatcherTimer _saveTimer;

    private Settings _settings = new();

    public SettingsService(IHostApplicationLifetime applicationLifetime)
    {
        AppStorage.EnsureDataDirectory();
        AppStorage.MigrateLegacyFile(SettingsFileName);

        // 在 UI 线程上做防抖保存
        _saveTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(SaveDelayMilliseconds),
            DispatcherPriority.Background,
            OnSaveTimerTick,
            Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
        _saveTimer.Stop();

        Settings = AppStorage.Load<Settings>(SettingsFileName) ?? new Settings();

        applicationLifetime.ApplicationStopping.Register(Flush);
    }

    private void OnSaveTimerTick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        Flush();
    }

    public event PropertyChangedEventHandler? OnSettingsChanged;

    public void LoadSettings()
    {
        var loaded = AppStorage.Load<Settings>(SettingsFileName);
        if (loaded != null)
        {
            Settings = loaded;
        }
    }

    /// <summary>
    /// 请求保存（防抖）。
    /// </summary>
    public void SaveSettings() => ScheduleSave();

    /// <summary>
    /// 立即写入磁盘。
    /// </summary>
    public void Flush()
    {
        lock (_saveLock)
        {
            if (_saveTimer.Dispatcher.CheckAccess())
            {
                _saveTimer.Stop();
            }

            try
            {
                AppStorage.Save(SettingsFileName, Settings);
            }
            catch (Exception ex)
            {
                // 保存失败不应该让程序崩溃（例如磁盘满、文件被占用）
                Debug.WriteLine($"[SettingsService] 保存失败：{ex.Message}");
            }
        }
    }

    private void ScheduleSave()
    {
        if (!_saveTimer.Dispatcher.CheckAccess())
        {
            _saveTimer.Dispatcher.BeginInvoke(new Action(ScheduleSave));
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Flush();
        return Task.CompletedTask;
    }

    public Settings Settings
    {
        get => _settings;
        set
        {
            if (ReferenceEquals(value, _settings))
            {
                return;
            }

            // 只订阅当前实例，避免旧实例继续触发保存
            _settings.PropertyChanged -= OnSettingsPropertyChanged;
            _settings = value;
            _settings.PropertyChanged += OnSettingsPropertyChanged;
            OnPropertyChanged();
            OnSettingsChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Settings)));
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnSettingsChanged?.Invoke(sender, e);
        ScheduleSave();
    }
}
