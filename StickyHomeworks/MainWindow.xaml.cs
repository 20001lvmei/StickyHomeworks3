using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ElysiaFramework;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors;
using StickyHomeworks.Behaviors;
using StickyHomeworks.Models;
using StickyHomeworks.Services;
using StickyHomeworks.ViewModels;
using StickyHomeworks.Views;
using System.Windows.Automation;
using System.Windows.Forms;
using System.Windows.Threading;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using Path = System.IO.Path;

namespace StickyHomeworks;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; set; } = new MainViewModel();

    public ProfileService ProfileService { get; }

    public SettingsService SettingsService { get; }

    public event EventHandler? OnHomeworkEditorUpdated;

    /// <summary>
    /// 临时浮到最前时置为 true，期间不执行“置底”。
    /// </summary>
    private bool _forceForeground;

    /// <summary>
    /// 是否因为被激活而临时浮到最前（失焦后恢复置底）。
    /// </summary>
    private bool _raisedByActivation;

    public MainWindow(ProfileService profileService,
                      SettingsService settingsService,
                      WindowFocusObserverService focusObserverService)
    {
        ProfileService = profileService;
        SettingsService = settingsService;
        //Automation.AddAutomationFocusChangedEventHandler(OnFocusChangedHandler);
        InitializeComponent();
        focusObserverService.FocusChanged += FocusObserverServiceOnFocusChanged;
        ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
        ViewModel.PropertyChanging += ViewModelOnPropertyChanging;
        DataContext = this;
    }

    private void FocusObserverServiceOnFocusChanged(object? sender, EventArgs e)
    {
        if (!ViewModel.IsDrawerOpened)
            return;
        try
        {
            var hWnd = NativeWindowHelper.GetForegroundWindow();
            NativeWindowHelper.GetWindowThreadProcessId(hWnd, out var id);
            using var proc = Process.GetProcessById(id);
            if (proc.Id != Environment.ProcessId &&
                !new List<string>(["ctfmon", "textinputhost", "chsime"]).Contains(proc.ProcessName.ToLower()))
            {
                Dispatcher.Invoke(() => ExitEditingMode());
            }
        }
        catch
        {
            // ignored
        }
    }

    private void ViewModelOnPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.SelectedHomework))
        {
            ExitEditingMode(true);
        }
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.SelectedListBoxItem))
        {
            RepositionEditingWindow();
        }
        if (e.PropertyName == nameof(ViewModel.SelectedHomework))
        {
            ExitEditingMode(false);
        }
    }
    

    private void ExitEditingMode(bool hard=true)
    {
        if (ViewModel.IsCreatingMode)
        {
            ViewModel.IsCreatingMode = false;

            // 新建后什么也没填，就不要留下一条空作业
            var editing = ViewModel.EditingHomework;
            if (editing != null &&
                string.IsNullOrWhiteSpace(RichTextBoxHelper.GetPlainText(editing.Content)) &&
                editing.Tags.Count == 0)
            {
                ProfileService.Profile.Homeworks.Remove(editing);
                ProfileService.SaveProfile();
            }
            return;
        }
        if (hard)
            MainListView.SelectedIndex = -1;
        ViewModel.IsDrawerOpened = false;
        AppEx.GetService<HomeworkEditWindow>().TryClose();
        AppEx.GetService<ProfileService>().SaveProfile();
    }

    private void SetPos()
    {
        GetCurrentDpi(out var dpi, out _);
        Left = SettingsService.Settings.WindowX / dpi;
        Top = SettingsService.Settings.WindowY / dpi;
        Width = SettingsService.Settings.WindowWidth / dpi;
        Height = SettingsService.Settings.WindowHeight / dpi;
        ClampToScreen();
    }

    /// <summary>
    /// 保证窗口至少有一部分在屏幕可见区域内，避免恢复配置后窗口跑到屏幕外“打不开”。
    /// </summary>
    private void ClampToScreen()
    {
        var work = SystemParameters.WorkArea;
        if (double.IsNaN(Left) || Left < work.Left || Left > work.Right - 120)
        {
            Left = work.Left + 60;
        }

        if (double.IsNaN(Top) || Top < work.Top || Top > work.Bottom - 120)
        {
            Top = work.Top + 60;
        }
    }

    /// <summary>
    /// 被“再次启动”的程序唤醒时调用：把窗口显示到最前。
    /// </summary>
    public void ShowFromBackground()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        ClampToScreen();

        // 冻结（置底）状态下也要先浮到最前，否则用户根本看不到窗口
        _forceForeground = true;
        var hWnd = new WindowInteropHelper(this).Handle;
        NativeWindowHelper.SetWindowPos(hWnd, NativeWindowHelper.HWND_TOP, 0, 0, 0, 0,
            NativeWindowHelper.SWP_NOSIZE | NativeWindowHelper.SWP_NOMOVE);
        Topmost = true;
        Topmost = false;
        Activate();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _forceForeground = false;
            SetBottom();
        };
        timer.Start();
    }

    private void SavePos()
    {
        GetCurrentDpi(out var dpi, out _);
        SettingsService.Settings.WindowX = Left * dpi;
        SettingsService.Settings.WindowY = Top * dpi;
        if (ViewModel.IsExpanded)
        {
            SettingsService.Settings.WindowWidth = Width * dpi;
            SettingsService.Settings.WindowHeight = Height * dpi;
        }
    }

    protected override void OnInitialized(EventArgs e)
    {
        // 默认不保留空白作业：内容为空且没有标签的条目直接去掉
        RemoveEmptyHomeworks();

        // 过期作业不再直接删除，而是归档到 Profile.ArchivedHomeworks 中并保存，避免重启后数据丢失
        ViewModel.ArchivedHomeworks = ProfileService.ArchiveOutdated(SettingsService.Settings.IsAutoArchiveEnabled);
        UpdateArchivedHomeworkState();
        if (ViewModel.ArchivedHomeworks.Count > 0)
        {
            ViewModel.SnackbarMessageQueue.Enqueue($"已将{ViewModel.ArchivedHomeworks.Count}条过期作业归档。",
                "恢复", (o) => { RestoreArchivedHomework(); }, null, false, false, TimeSpan.FromSeconds(30));
        }
        base.OnInitialized(e);
    }

    /// <summary>
    /// 去掉内容为空、也没有标签的作业（例如新建后没填就关掉了）。
    /// </summary>
    private void RemoveEmptyHomeworks()
    {
        var empty = ProfileService.Profile.Homeworks
            .Where(h => string.IsNullOrWhiteSpace(RichTextBoxHelper.GetPlainText(h.Content)) && h.Tags.Count == 0)
            .ToList();

        foreach (var homework in empty)
        {
            ProfileService.Profile.Homeworks.Remove(homework);
        }

        if (empty.Count > 0)
        {
            ProfileService.SaveProfile();
        }
    }

    private void UpdateArchivedHomeworkState()
    {
        ViewModel.HasArchivedHomeworks = ProfileService.Profile.ArchivedHomeworks.Count > 0;
    }

    private void RestoreArchivedHomework()
    {
        var count = ProfileService.RestoreArchived();
        if (count == 0)
        {
            return;
        }

        UpdateArchivedHomeworkState();
        ViewModel.SnackbarMessageQueue.Enqueue($"已恢复{count}条作业。");
    }

    protected override void OnContentRendered(EventArgs e)
    {
        SetBottom();
        SetPos();
        AppEx.GetService<HomeworkEditWindow>().EditingFinished += OnEditingFinished;
        AppEx.GetService<HomeworkEditWindow>().SubjectChanged += OnSubjectChanged;
        base.OnContentRendered(e);
    }

    private void OnSubjectChanged(object? sender, EventArgs e)
    {
        if (ViewModel.IsUpdatingHomeworkSubject)
            return;
        if (ViewModel.SelectedHomework == null)
            return;
        if (!ViewModel.IsDrawerOpened)
            return;
        ViewModel.IsUpdatingHomeworkSubject = true;
        var s = ViewModel.SelectedHomework;
        ProfileService.Profile.Homeworks.Remove(s);
        ProfileService.Profile.Homeworks.Add(s);
        ViewModel.SelectedHomework = s;
        ViewModel.IsUpdatingHomeworkSubject = false;
    }

    private void OnEditingFinished(object? sender, EventArgs e)
    {
        ExitEditingMode();
    }

    private void ButtonCreateHomework_OnClick(object sender, RoutedEventArgs e)
    {
        CreateHomework();
    }

    private void CreateHomework()
    {
        if (!EnsureNotFrozen())
        {
            return;
        }

        ViewModel.IsUpdatingHomeworkSubject = true;
        OnHomeworkEditorUpdated?.Invoke(this ,EventArgs.Empty);
        var lastSubject = ViewModel.EditingHomework.Subject;
        ViewModel.IsCreatingMode = true;
        ViewModel.IsDrawerOpened = true;
        var o = new Homework()
        {
            Subject = lastSubject
        };
        ViewModel.EditingHomework = o;
        ViewModel.SelectedHomework = o;
        ProfileService.Profile.Homeworks.Add(o);
        //ComboBoxSubject.Text = lastSubject;
        SettingsService.SaveSettings();
        ProfileService.SaveProfile();
        ViewModel.IsUpdatingHomeworkSubject = false;
        RepositionEditingWindow();
        AppEx.GetService<HomeworkEditWindow>().TryOpen();
    }

    private void ButtonAddHomeworkCompleted_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ProfileService.Profile.Homeworks.Contains(ViewModel.EditingHomework))
        {
            ProfileService.Profile.Homeworks.Add(ViewModel.EditingHomework);
        }
        ProfileService.SaveProfile();
        ViewModel.IsDrawerOpened = false;
    }

    public void GetCurrentDpi(out double dpiX, out double dpiY)
    {
        var source = PresentationSource.FromVisual(this);

        dpiX = 1.0;
        dpiY = 1.0;

        if (source?.CompositionTarget != null)
        {
            dpiX = 1.0 * source.CompositionTarget.TransformToDevice.M11;
            dpiY = 1.0 * source.CompositionTarget.TransformToDevice.M22;
        }
    }

    private void ButtonSettings_OnClick(object sender, RoutedEventArgs e)
    {
        OpenSettingsWindow();
    }

    private void OpenSettingsWindow()
    {
        var win = AppEx.GetService<SettingsWindow>();
        if (!win.IsOpened)
        {
            //Analytics.TrackEvent("打开设置窗口");
            win.IsOpened = true;
            win.Show();
        }
        else
        {
            if (win.WindowState == WindowState.Minimized)
            {
                win.WindowState = WindowState.Normal;
            }

            win.Activate();
        }
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        // 不再拦截关闭：Alt+F4 / 任务管理器关闭时直接退出并落盘
        SavePos();
        SettingsService.Flush();
        ProfileService.Flush();
    }

    private void ButtonEditTags_OnClick(object sender, RoutedEventArgs e)
    {
        if (!EnsureNotFrozen())
        {
            return;
        }

        OnHomeworkEditorUpdated?.Invoke(this, EventArgs.Empty);
        ViewModel.IsTagEditingPopupOpened = true;
    }

    private void ButtonEditHomework_OnClick(object sender, RoutedEventArgs e)
    {
        if (!EnsureNotFrozen())
        {
            return;
        }

        OnHomeworkEditorUpdated?.Invoke(this, EventArgs.Empty);
        ViewModel.IsCreatingMode = false;
        if (ViewModel.SelectedHomework== null)
            return;
        ViewModel.EditingHomework = ViewModel.SelectedHomework;
        ViewModel.IsDrawerOpened = true;
        RepositionEditingWindow();
        AppEx.GetService<HomeworkEditWindow>().TryOpen();
    }

    private void RepositionEditingWindow()
    {
        if (ViewModel.SelectedListBoxItem == null) 
            return;
        Debug.WriteLine("selected changed");
        try
        {
            GetCurrentDpi(out var dpiX, out var dpiY);
            var p = ViewModel.SelectedListBoxItem.PointToScreen(new Point(ViewModel.SelectedListBoxItem.ActualWidth, 0));
            var screen = Screen.PrimaryScreen!.WorkingArea;
            var homeworkEditWindow = AppEx.GetService<HomeworkEditWindow>();
            homeworkEditWindow.Left = p.X / dpiX;
            homeworkEditWindow.Top = Math.Min(p.Y, screen.Bottom - homeworkEditWindow.ActualHeight * dpiY) / dpiY;
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private void ButtonRemoveHomework_OnClick(object sender, RoutedEventArgs e)
    {
        if (!EnsureNotFrozen())
        {
            return;
        }

        ViewModel.IsUpdatingHomeworkSubject = true;
        if (ViewModel.SelectedHomework == null)
            return;
        ProfileService.Profile.Homeworks.Remove(ViewModel.SelectedHomework);
        ViewModel.IsUpdatingHomeworkSubject = false;
    }

    private void ButtonEditDone_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsDrawerOpened = false;
    }

    private void DragBorder_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.IsUnlocked && e.LeftButton == MouseButtonState.Pressed)
        {
            SetBottom();
            DragMove();
            SetBottom();
        }
    }

    private void SetBottom()
    {
        if (!SettingsService.Settings.IsBottom || _forceForeground)
        {
            return;
        }
        var hWnd = new WindowInteropHelper(this).Handle;
        NativeWindowHelper.SetWindowPos(hWnd, NativeWindowHelper.HWND_BOTTOM, 0, 0, 0, 0, NativeWindowHelper.SWP_NOSIZE | NativeWindowHelper.SWP_NOMOVE | NativeWindowHelper.SWP_NOACTIVATE);
    }

    /// <summary>
    /// 冻结/解冻：冻结后只能查看，不能修改作业。
    /// </summary>
    private void ButtonFreeze_OnClick(object sender, RoutedEventArgs e)
    {
        var frozen = !SettingsService.Settings.IsFrozen;
        SettingsService.Settings.IsFrozen = frozen;
        SettingsService.SaveSettings();

        if (frozen)
        {
            // 冻结时关掉编辑界面，避免正在编辑
            ExitEditingMode();
            ViewModel.SnackbarMessageQueue.Enqueue("已冻结：仅可查看，不能修改作业。");
        }
        else
        {
            ViewModel.SnackbarMessageQueue.Enqueue("已解冻：可以正常修改作业。");
        }
    }

    /// <summary>
    /// 冻结状态下拦截修改操作。
    /// </summary>
    private bool EnsureNotFrozen()
    {
        if (!SettingsService.Settings.IsFrozen)
        {
            return true;
        }

        ViewModel.SnackbarMessageQueue.Enqueue("已冻结，请先点雪花按钮解冻。");
        return false;
    }

    private void MainWindow_OnStateChanged(object? sender, EventArgs e)
    {
        SetBottom();
    }

    private void MainWindow_OnActivated(object? sender, EventArgs e)
    {
        // 置底模式下被激活（点任务栏、点窗口）时先浮到最前，否则点了也没反应；
        // 失去焦点后再回到底层。
        if (SettingsService.Settings.IsBottom && !_forceForeground)
        {
            var hWnd = new WindowInteropHelper(this).Handle;
            NativeWindowHelper.SetWindowPos(hWnd, NativeWindowHelper.HWND_TOP, 0, 0, 0, 0,
                NativeWindowHelper.SWP_NOSIZE | NativeWindowHelper.SWP_NOMOVE);
            _raisedByActivation = true;
            return;
        }

        SetBottom();
    }

    private void ButtonExit_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsClosing = true;
        Close();
    }

    private void ButtonDateSetToday_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.EditingHomework.DueTime = DateTime.Today;
    }

    private void ButtonDateSetWeekends_OnClick(object sender, RoutedEventArgs e)
    {
        var today = DateTime.Today;
        var delta = DayOfWeek.Saturday - today.DayOfWeek + 1;
        ViewModel.EditingHomework.DueTime = today + TimeSpan.FromDays(delta);
    }

    private void ButtonExpandingSwitcher_OnClick(object sender, RoutedEventArgs e)
    {
        SavePos();
        ViewModel.IsExpanded = !ViewModel.IsExpanded;
        if (ViewModel.IsExpanded)
        {
            SizeToContent = SizeToContent.Manual;
            SetPos();
        }
        else
        {
            ViewModel.IsUnlocked = false;
            SizeToContent = SizeToContent.Height;
            Width = Math.Min(ActualWidth, 350);
        }
    }

    private void MainWindow_OnDeactivated(object? sender, EventArgs e)
    {
        if (_raisedByActivation && SettingsService.Settings.IsBottom)
        {
            _raisedByActivation = false;
            SetBottom();
        }
    }

    private async void ButtonExport_OnClick(object sender, RoutedEventArgs e)
    {
        var pictures = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "StickyHomeworks3");
        try
        {
            Directory.CreateDirectory(pictures);
        }
        catch (Exception)
        {
            pictures = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        using var dialog = new System.Windows.Forms.SaveFileDialog
        {
            Filter = "图片 (*.png)|*.png",
            DefaultExt = "png",
            AddExtension = true,
            OverwritePrompt = true,
            InitialDirectory = pictures,
            FileName = $"作业_{DateTime.Now:yyyyMMdd_HHmm}.png"
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        ViewModel.IsWorking = true;
        try
        {
            ExitEditingMode();
            await Dispatcher.Yield(DispatcherPriority.Render);

            var png = RenderHomeworkListToPng();
            var file = dialog.FileName;

            try
            {
                File.WriteAllBytes(file, png);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // 目标目录不可写（常见于 Windows“受控文件夹访问”拦截，或选了系统目录）
                var fallback = Path.Combine(pictures, Path.GetFileName(file));
                try
                {
                    File.WriteAllBytes(fallback, png);
                    ViewModel.SnackbarMessageQueue.Enqueue(
                        $"没有权限写入该位置，已改为保存到：{fallback}", "查看", () => OpenPath(fallback));
                    return;
                }
                catch (Exception fallbackEx)
                {
                    ViewModel.SnackbarMessageQueue.Enqueue(
                        $"保存失败：没有写入权限。请换一个目录（如 D:\\ 或桌面），" +
                        $"或在 Windows 安全中心 → 病毒和威胁防护 → 受控文件夹访问 中允许本程序。{fallbackEx.Message}");
                    Debug.WriteLine($"[Export] 保存失败：{ex.Message}");
                    return;
                }
            }

            ViewModel.SnackbarMessageQueue.Enqueue($"已导出到：{file}", "查看", () => OpenPath(file));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Export] 导出失败：{ex}");
            ViewModel.SnackbarMessageQueue.Enqueue($"导出失败：{ex.Message}");
        }
        finally
        {
            ViewModel.IsWorking = false;
        }
    }

    /// <summary>
    /// 把作业列表渲染成 PNG 字节。
    /// </summary>
    private byte[] RenderHomeworkListToPng()
    {
        var scale = SettingsService.Settings.Scale;
        var width = Math.Max(1, MainListView.ActualWidth);
        var height = Math.Max(1, MainListView.ActualHeight);

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var brush = new VisualBrush(MainListView) { Stretch = Stretch.None };
            var background = (Brush)FindResource("MaterialDesignPaper");
            var rect = new Rect(0, 0, width * scale, height * scale);
            context.DrawRectangle(background, null, rect);
            context.DrawRectangle(brush, null, rect);
        }

        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(width * scale),
            (int)Math.Ceiling(height * scale),
            96d, 96d, PixelFormats.Default);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static void OpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Export] 打开文件失败：{ex.Message}");
        }
    }

    private void DrawerHost_OnDrawerClosing(object? sender, DrawerClosingEventArgs e)
    {
        SettingsService.SaveSettings();
        ProfileService.SaveProfile();
    }

    private void ButtonMore_Click(object sender, RoutedEventArgs e)
    {
        PopupExAdvanced.IsOpen = true;
    }

    private void MainListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        //ExitEditingMode(false);
    }

    public void OnTextBoxEnter()
    {
        CreateHomework();
    }

    private void MenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        PopupExAdvanced.IsOpen = false;
    }

    private void MenuItemRecoverExpiredHomework_OnClick(object sender, RoutedEventArgs e)
    {
        RestoreArchivedHomework();
    }

    /// <summary>
    /// 打开时间机器：列出自动备份，选择一个恢复。
    /// </summary>
    private async void MenuItemTimeMachine_OnClick(object sender, RoutedEventArgs e)
    {
        PopupExAdvanced.IsOpen = false;
        await OpenTimeMachineAsync();
    }

    /// <summary>
    /// 打开“时间机器”对话框（托盘菜单也会调用）。
    /// </summary>
    public async Task OpenTimeMachineAsync()
    {
        ViewModel.Backups.Clear();
        foreach (var entry in AppStorage.ListBackups("Profile.json").Select(BackupEntry.FromFile))
        {
            ViewModel.Backups.Add(entry);
        }

        if (ViewModel.Backups.Count == 0)
        {
            ViewModel.SnackbarMessageQueue.Enqueue("暂时还没有备份，应用会在保存作业时自动创建。");
            return;
        }

        ViewModel.SelectedBackup = ViewModel.Backups.First();
        await DialogHost.Show(FindResource("TimeMachineDialog"), "MainWindow");
    }

    /// <summary>
    /// 托盘菜单：显示窗口。
    /// </summary>
    public void ShowWindowFromTray() => ShowFromBackground();

    /// <summary>
    /// 托盘菜单：隐藏窗口。
    /// </summary>
    public void HideWindowFromTray()
    {
        ExitEditingMode();
        Hide();
    }

    /// <summary>
    /// 托盘菜单：打开设置。
    /// </summary>
    public void OpenSettingsFromTray() => OpenSettingsWindow();

    /// <summary>
    /// 托盘菜单：退出程序。
    /// </summary>
    public void ExitFromTray()
    {
        ViewModel.IsClosing = true;
        Close();
    }

    private void ButtonRestoreBackup_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedBackup is not { } entry)
        {
            ViewModel.SnackbarMessageQueue.Enqueue("请先选择一份备份。");
            return;
        }

        try
        {
            AppStorage.RestoreBackup(entry.Path, "Profile.json");
            ProfileService.Reload();
            RemoveEmptyHomeworks();
            UpdateArchivedHomeworkState();
            DialogHost.Close("MainWindow");
            ViewModel.SnackbarMessageQueue.Enqueue($"已恢复到 {entry.TimeText} 的备份。");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TimeMachine] 恢复失败：{ex}");
            ViewModel.SnackbarMessageQueue.Enqueue($"恢复失败：{ex.Message}");
        }
    }

    private void MainWindow_OnDragOver(object sender, DragEventArgs e)
    {

    }

    private void MainWindow_OnDragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;
        ViewModel.IsExpanded = false;
        ViewModel.IsUnlocked = false;
        SizeToContent = SizeToContent.Height;
        Width = Math.Min(ActualWidth, 350);
    }
}