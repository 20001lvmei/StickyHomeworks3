using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignColors;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
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
    private double _opacity = 1;
    private double _scale = 1.5;
    private ObservableCollection<string> _subjects = new();
    private ObservableCollection<string> _tags = new();
    private ObservableCollection<HomeworkTemplate> _templates = new();
    private bool _isFirstRunCompleted = false;
    private bool _isDebugOptionsEnabled = false;
    private double _windowX = 0;
    private double _windowY = 0;
    private double _windowWidth = 400;
    private double _windowHeight = 800;
    private bool _isBottom = true;
    private bool _isFrozen = false;
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

    private bool _isAutoStartEnabled;

    /// <summary>
    /// 是否开机自启（保存的是用户意愿，真正的注册由 <c>AutoStartHelper</c> 在启动时应用）。
    /// 这样即使某次写入快捷方式/注册表失败，开关也不会自己弹回去。
    /// </summary>
    public bool IsAutoStartEnabled
    {
        get => _isAutoStartEnabled;
        set
        {
            if (value == _isAutoStartEnabled) return;
            _isAutoStartEnabled = value;
            OnPropertyChanged();
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
    /// 是否冻结（只能查看，不能修改作业）。
    /// </summary>
    public bool IsFrozen
    {
        get => _isFrozen;
        set
        {
            if (value == _isFrozen) return;
            _isFrozen = value;
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

    private bool _isNumberedList = true;

    /// <summary>
    /// 作业前面的标记：true 用数字序号（1. 2.），false 用圆点。
    /// </summary>
    public bool IsNumberedList
    {
        get => _isNumberedList;
        set
        {
            if (value == _isNumberedList) return;
            _isNumberedList = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ListMarkerStyleIndex));
        }
    }

    /// <summary>
    /// 下拉框用的索引：0 = 数字序号，1 = 圆点。只用于界面绑定，不单独存储。
    /// </summary>
    [JsonIgnore]
    public int ListMarkerStyleIndex
    {
        get => _isNumberedList ? 0 : 1;
        set
        {
            var numbered = value == 0;
            if (numbered == _isNumberedList) return;
            IsNumberedList = numbered;
            OnPropertyChanged();
        }
    }

    private bool _isClassIslandLinkEnabled;
    private string _classIslandDataDirectory = "";

    /// <summary>
    /// 是否启用 ClassIsland 联动：上课自动隐藏窗口，下课自动显示。
    /// </summary>
    public bool IsClassIslandLinkEnabled
    {
        get => _isClassIslandLinkEnabled;
        set
        {
            if (value == _isClassIslandLinkEnabled) return;
            _isClassIslandLinkEnabled = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// ClassIsland 数据目录，留空则自动检测。
    /// </summary>
    public string ClassIslandDataDirectory
    {
        get => _classIslandDataDirectory;
        set
        {
            if (value == _classIslandDataDirectory) return;
            _classIslandDataDirectory = value ?? "";
            OnPropertyChanged();
        }
    }

    private bool _isWebServerEnabled;
    private int _webServerPort = 8787;

    /// <summary>
    /// 是否启用网页版（浏览器访问 127.0.0.1:端口 查看/编辑作业）。
    /// </summary>
    public bool IsWebServerEnabled
    {
        get => _isWebServerEnabled;
        set
        {
            if (value == _isWebServerEnabled) return;
            _isWebServerEnabled = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 网页版端口。
    /// </summary>
    public int WebServerPort
    {
        get => _webServerPort;
        set
        {
            var port = value is < 1024 or > 65535 ? 8787 : value;
            if (port == _webServerPort) return;
            _webServerPort = port;
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

    /// <summary>
    /// 作业模板。
    /// </summary>
    public ObservableCollection<HomeworkTemplate> Templates
    {
        get => _templates;
        set
        {
            if (Equals(value, _templates)) return;
            _templates = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 是否已完成首次启动引导（OOBE）。
    /// </summary>
    public bool IsFirstRunCompleted
    {
        get => _isFirstRunCompleted;
        set
        {
            if (value == _isFirstRunCompleted) return;
            _isFirstRunCompleted = value;
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
    /// 保留旧版本配置里本版本还不认识的字段（例如 ClassIsland 联动、标题颜色等），
    /// 保存时原样写回，避免新版本把旧配置“吞掉”。
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraData { get; set; }

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