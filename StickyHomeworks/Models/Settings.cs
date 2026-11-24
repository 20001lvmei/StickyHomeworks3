using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignColors;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Media;
using Microsoft.Win32;
using WindowsShortcutFactory;
using File = System.IO.File;

namespace StickyHomeworks.Models;

public class Settings : ObservableRecipient
{
    private int _selectedPlatteIndex = 0;
    private int _theme = 0;
    /// <summary>默认主题色：蓝色。</summary>
    private Color _primaryColor = Color.FromRgb(37, 99, 235);
    private Color _secondaryColor = Color.FromRgb(59, 130, 246);
    private int _colorSource = 0;
    private ObservableCollection<Color> _wallpaperColorPlatte = new();
    private bool _isWallpaperAutoUpdateEnabled = false;
    private int _wallpaperAutoUpdateIntervalSeconds = 60;
    private string _wallpaperClassName = "";
    private bool _isFallbackModeEnabled = true;
    private double _targetLightValue = 0.6;
    private double _opacity = 0.7;
    private double _scale = 1.5;
    private ObservableCollection<string> _subjects = new();
    private ObservableCollection<string> _tags = new();
    private bool _isDebugOptionsEnabled = false;
    private double _windowX = 0;
    private double _windowY = 0;
    private double _windowWidth = 400;
    private double _windowHeight = 800;
    private bool _isBottom = true;
    private bool _isAutoArchiveEnabled = true;
    private string _title = "作业";
    private double _maxPanelWidth = 350;
    private bool _isDebugShowInTaskBar = false;
    private ObservableCollection<Color> _savedColors = new();

    public double WindowX
    {        get => _windowX;
        set
        {
            if (value == _windowX) return;
            _windowX = value;
            OnPropertyChanged();
        }
    }

    public double WindowY
    {
        get => _windowY;
        set
        {
            if (value == _windowY) return;
            _windowY = value;
            OnPropertyChanged();
        }
    }

    public double WindowWidth
    {
        get => _windowWidth;
        set
        {
            if (value == _windowWidth) return;
            _windowWidth = value;
            OnPropertyChanged();
        }
    }

    public double WindowHeight
    {
        get => _windowHeight;
        set
        {
            if (value == _windowHeight) return;
            _windowHeight = value;
            OnPropertyChanged();
        }
    }

    #region General

    private const string ShortcutFileName = "StickyHomeworks3.lnk";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string RunValueName = "StickyHomeworks3";

    private static readonly string[] LegacyShortcutFileNames = { "StickyHomeworks.lnk" };

    private static string? ExecutablePath =>
        Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;

    private static string StartupShortcutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), ShortcutFileName);

    private static bool RunKeyExists()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(RunValueName) is string value && value.Length > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void SetRunKey(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (key == null)
        {
            throw new InvalidOperationException("无法打开注册表 Run 项。");
        }

        if (enabled)
        {
            var exe = ExecutablePath;
            if (string.IsNullOrWhiteSpace(exe))
            {
                throw new InvalidOperationException("无法获取程序路径。");
            }

            key.SetValue(RunValueName, $"\"{exe}\"");
        }
        else if (key.GetValue(RunValueName) != null)
        {
            key.DeleteValue(RunValueName, false);
        }
    }

    /// <summary>
    /// 是否开机自启。
    /// <para>
    /// 优先在“启动”文件夹创建快捷方式；如果该目录不可写（权限/安全软件/策略限制），
    /// 自动回退到 <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>。
    /// 两者只要有一个生效就视为已启用。
    /// </para>
    /// </summary>
    public bool IsAutoStartEnabled
    {
        get => File.Exists(StartupShortcutPath) || RunKeyExists();
        set
        {
            var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            var path = StartupShortcutPath;
            try
            {
                if (value)
                {
                    var created = false;
                    try
                    {
                        using var shortcut = new WindowsShortcut();
                        shortcut.Path = ExecutablePath;
                        // 工作目录固定为程序目录，避免从不同工作目录启动时读写到不同的数据文件
                        shortcut.WorkingDirectory = AppContext.BaseDirectory;
                        shortcut.Save(path);
                        created = File.Exists(path);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Settings] 创建启动快捷方式失败：{ex.Message}");
                    }

                    if (!created)
                    {
                        // 启动目录不可写时回退到注册表 Run 项
                        SetRunKey(true);
                        Debug.WriteLine("[Settings] 已改用注册表 Run 项实现开机自启。");
                    }

                    // 清掉旧版本留下的自启动快捷方式，避免同时启动两个实例
                    foreach (var legacyName in LegacyShortcutFileNames)
                    {
                        var legacyPath = Path.Combine(startup, legacyName);
                        if (File.Exists(legacyPath))
                        {
                            File.Delete(legacyPath);
                        }
                    }
                }
                else
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    SetRunKey(false);

                    foreach (var legacyName in LegacyShortcutFileNames)
                    {
                        var legacyPath = Path.Combine(startup, legacyName);
                        if (File.Exists(legacyPath))
                        {
                            File.Delete(legacyPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] 设置开机自启失败：{ex.Message}");
            }
            finally
            {
                // 无论成功与否都刷新一次，让开关显示真实状态
                OnPropertyChanged(nameof(IsAutoStartEnabled));
            }
        }
    }

    public bool IsBottom
    {
        get => _isBottom;
        set
        {
            if (value == _isBottom) return;
            _isBottom = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 是否在启动时把已经过期的作业归档（从列表中移除，但保留在 Profile.ArchivedHomeworks 中，可随时恢复）。
    /// 关闭后过期作业会一直显示在列表中。
    /// </summary>
    public bool IsAutoArchiveEnabled
    {
        get => _isAutoArchiveEnabled;
        set
        {
            if (value == _isAutoArchiveEnabled) return;
            _isAutoArchiveEnabled = value;
            OnPropertyChanged();
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            if (value == _title) return;
            _title = value;
            OnPropertyChanged();
        }
    }

    public double MaxPanelWidth
    {
        get => _maxPanelWidth;
        set
        {
            if (value.Equals(_maxPanelWidth)) return;
            _maxPanelWidth = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Appearence

    public int Theme
    {
        get => _theme;
        set
        {
            if (value == _theme) return;
            _theme = value;
            OnPropertyChanged();
        }
    }

    public Color PrimaryColor
    {
        get => _primaryColor;
        set
        {
            if (value.Equals(_primaryColor)) return;
            _primaryColor = value;
            OnPropertyChanged();
        }
    }

    public Color SecondaryColor
    {
        get => _secondaryColor;
        set
        {
            if (value.Equals(_secondaryColor)) return;
            _secondaryColor = value;
            OnPropertyChanged();
        }
    }

    public int ColorSource
    {
        get => _colorSource;
        set
        {
            if (value == _colorSource) return;
            _colorSource = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<Color> WallpaperColorPlatte
    {
        get => _wallpaperColorPlatte;
        set
        {
            if (Equals(value, _wallpaperColorPlatte)) return;
            _wallpaperColorPlatte = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public Color SelectedPlatte => WallpaperColorPlatte.Count < Math.Max(SelectedPlatteIndex, 0) + 1
        ? Colors.DodgerBlue
        : WallpaperColorPlatte[SelectedPlatteIndex];

    public int SelectedPlatteIndex
    {
        get => _selectedPlatteIndex;
        set
        {
            if (value == _selectedPlatteIndex) return;
            _selectedPlatteIndex = value;
            OnPropertyChanged();
        }
    }

    public bool IsWallpaperAutoUpdateEnabled
    {
        get => _isWallpaperAutoUpdateEnabled;
        set
        {
            if (value == _isWallpaperAutoUpdateEnabled) return;
            _isWallpaperAutoUpdateEnabled = value;
            OnPropertyChanged();
        }
    }

    public int WallpaperAutoUpdateIntervalSeconds
    {
        get => _wallpaperAutoUpdateIntervalSeconds;
        set
        {
            if (value == _wallpaperAutoUpdateIntervalSeconds) return;
            _wallpaperAutoUpdateIntervalSeconds = value;
            OnPropertyChanged();
        }
    }

    public string WallpaperClassName
    {
        get => _wallpaperClassName;
        set
        {
            if (value == _wallpaperClassName) return;
            _wallpaperClassName = value;
            OnPropertyChanged();
        }
    }

    public bool IsFallbackModeEnabled
    {
        get => _isFallbackModeEnabled;
        set
        {
            if (value == _isFallbackModeEnabled) return;
            _isFallbackModeEnabled = value;
            OnPropertyChanged();
        }
    }

    public double TargetLightValue
    {
        get => _targetLightValue;
        set
        {
            if (value.Equals(_targetLightValue)) return;
            _targetLightValue = value;
            OnPropertyChanged();
        }
    }

    public double Opacity
    {
        get => _opacity;
        set
        {
            if (value.Equals(_opacity)) return;
            _opacity = value;
            OnPropertyChanged();
        }
    }

    public double Scale
    {
        get => _scale;
        set
        {
            if (value.Equals(_scale)) return;
            _scale = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Subjects

    public ObservableCollection<string> Subjects
    {
        get => _subjects;
        set
        {
            if (Equals(value, _subjects)) return;
            _subjects = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Tags

    public ObservableCollection<string> Tags
    {
        get => _tags;
        set
        {
            if (Equals(value, _tags)) return;
            _tags = value;
            OnPropertyChanged();
        }
    }


    #endregion
    public bool IsDebugOptionsEnabled
    {
        get => _isDebugOptionsEnabled;
        set
        {
            if (value == _isDebugOptionsEnabled) return;
            _isDebugOptionsEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool IsDebugShowInTaskBar
    {
        get => _isDebugShowInTaskBar;
        set
        {
            if (value == _isDebugShowInTaskBar) return;
            _isDebugShowInTaskBar = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<Color> SavedColors
    {
        get => _savedColors;
        set
        {
            if (Equals(value, _savedColors)) return;
            _savedColors = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 修正历史配置里的非法值。旧版本会把主题色的透明度写成 0（完全透明），
    /// 导致标题栏、强调色显示异常，这里统一补成不透明。
    /// </summary>
    public void Normalize()
    {
        if (PrimaryColor.A != 255)
        {
            PrimaryColor = Color.FromArgb(255, PrimaryColor.R, PrimaryColor.G, PrimaryColor.B);
        }

        if (SecondaryColor.A != 255)
        {
            SecondaryColor = Color.FromArgb(255, SecondaryColor.R, SecondaryColor.G, SecondaryColor.B);
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            Title = "作业";
        }
    }

    /// <summary>
    /// 把主题恢复为默认蓝色。
    /// </summary>
    public void ResetThemeToBlue()
    {
        ColorSource = 0;
        PrimaryColor = Color.FromRgb(37, 99, 235);
        SecondaryColor = Color.FromRgb(59, 130, 246);
    }
}