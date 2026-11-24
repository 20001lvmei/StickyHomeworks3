using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignColors;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Media;
using WindowsShortcutFactory;
using File = System.IO.File;

namespace StickyHomeworks.Models;

public class Settings : ObservableRecipient
{
    private int _selectedPlatteIndex = 0;
    private int _theme = 0;
    private Color _primaryColor = Color.FromRgb(34, 209, 236);
    private Color _secondaryColor  = Color.FromRgb(34, 209, 236);
    private int _colorSource = 1;
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
    {
        get => _windowX;
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

    private static readonly string[] LegacyShortcutFileNames = { "StickyHomeworks.lnk" };

    public bool IsAutoStartEnabled
    {
        get => File.Exists(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), ShortcutFileName));
        set
        {
            var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            var path = Path.Combine(startup, ShortcutFileName);
            try
            {
                if (value)
                {
                    using var shortcut = new WindowsShortcut();
                    shortcut.Path = Environment.ProcessPath;
                    // 工作目录固定为程序目录，避免从不同工作目录启动时读写到不同的数据文件
                    shortcut.WorkingDirectory = AppContext.BaseDirectory;
                    shortcut.Save(path);

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
                    File.Delete(path);
                    foreach (var legacyName in LegacyShortcutFileNames)
                    {
                        var legacyPath = Path.Combine(startup, legacyName);
                        if (File.Exists(legacyPath))
                        {
                            File.Delete(legacyPath);
                        }
                    }
                }
                OnPropertyChanged();
            }
            catch (Exception)
            {
                // ignored
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
}