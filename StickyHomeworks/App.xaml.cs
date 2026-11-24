using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using ClassIsland.Services;
using ElysiaFramework;
using ElysiaFramework.Interfaces;
using ElysiaFramework.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StickyHomeworks.Services;
using StickyHomeworks.Views;
using MessageBox = System.Windows.MessageBox;

namespace StickyHomeworks;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : AppEx
{
    private static Mutex? Mutex;

    private static TrayService? _tray;

    /// <summary>
    /// 用于让“再次启动”的实例通知已运行的实例把窗口显示到最前。
    /// </summary>
    private static EventWaitHandle? ShowWindowEvent;

    private const string ShowWindowEventName = "StickyHomeworks3.ShowWindow";

    public static string AppVersion => Assembly.GetExecutingAssembly().GetName().Version!.ToString();

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            StartupCore(e);
        }
        catch (Exception ex)
        {
            // 启动阶段的异常不会走到 DispatcherUnhandledException，这里单独处理并给出提示
            WriteCrashLog(ex);
            MessageBox.Show(
                $"应用启动失败：{ex.Message}{Environment.NewLine}{Environment.NewLine}" +
                $"详细信息已写入：{Path.Combine(AppStorage.DataDirectory, "crash.log")}",
                "StickyHomeworks3",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private void StartupCore(StartupEventArgs e)
    {
        Mutex = new Mutex(true, "StickyHomeworks3.Lock", out var createNew);
        try
        {
            ShowWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] 创建窗口唤醒事件失败：{ex.Message}");
        }

        if (!createNew)
        {
            // 已经有实例在运行：通知它把窗口显示到最前，然后自己退出
            try
            {
                ShowWindowEvent?.Set();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] 唤醒已有实例失败：{ex.Message}");
            }

            Environment.Exit(0);
        }

        // 先确定可写的数据目录，并告知其它项目（数据库使用同一个目录）
        AppStorage.EnsureDataDirectory();
        Environment.SetEnvironmentVariable(AppStorage.DataDirectoryVariable, AppStorage.DataDirectory);

        Host = Microsoft.Extensions.Hosting.Host.
            CreateDefaultBuilder().
            UseContentRoot(AppContext.BaseDirectory).
            ConfigureServices((context, services) =>
            {
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<ProfileService>();
                services.AddSingleton<SettingsService>();
                services.AddSingleton<SettingsWindow>();
                services.AddSingleton<WallpaperPickingService>();
                services.AddHostedService<ThemeBackgroundService>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<HomeworkEditWindow>();
                services.AddSingleton<CrashWindow>();
                services.AddSingleton<WindowFocusObserverService>();
            }).
            Build();
        _ = Host.StartAsync();
        // 先把主窗口创建出来，确保 Application.MainWindow 指向它；
        // 否则 OOBE 窗口会成为 MainWindow，关闭后触发 ShutdownMode=OnMainWindowClose 直接退出。
        MainWindow = GetService<MainWindow>();

        var settingsService = GetService<SettingsService>();
        if (!settingsService.Settings.IsFirstRunCompleted)
        {
            var oobe = new OobeWindow(settingsService);
            oobe.ShowDialog();

            if (!settingsService.Settings.IsFirstRunCompleted)
            {
                // 用户直接关掉了引导窗口：给一套默认科目，避免每次启动都弹
                foreach (var subject in new[] { "语文", "数学", "外语", "其它" })
                {
                    if (!settingsService.Settings.Subjects.Contains(subject))
                    {
                        settingsService.Settings.Subjects.Add(subject);
                    }
                }

                settingsService.Settings.IsFirstRunCompleted = true;
                settingsService.SaveSettings();
            }
        }

        GetService<MainWindow>().Show();
        StartShowWindowListener();
        StartTrayIcon();
        base.OnStartup(e);
    }

    /// <summary>
    /// 创建右下角托盘图标。
    /// </summary>
    private void StartTrayIcon()
    {
        if (MainWindow is not MainWindow window)
        {
            return;
        }

        try
        {
            _tray = new TrayService(
                isWindowVisible: () => window.IsVisible,
                toggleWindow: () => Dispatcher.Invoke(() => window.ToggleWindowFromTray()),
                openSettings: () => Dispatcher.Invoke(() => window.OpenSettingsFromTray()),
                restart: () => Dispatcher.Invoke(() => window.RestartFromTray()),
                exit: () => Dispatcher.Invoke(() => window.ExitFromTray()));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] 创建托盘图标失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 后台等待“再次启动”的信号，收到后把主窗口显示到最前。
    /// 这样即使窗口被冻结在桌面底层，用户再点一次 exe 也能把它叫出来。
    /// </summary>
    private void StartShowWindowListener()
    {
        if (ShowWindowEvent == null)
        {
            return;
        }

        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    ShowWindowEvent.WaitOne();
                    Dispatcher.Invoke(() =>
                    {
                        if (MainWindow is MainWindow window)
                        {
                            window.ShowFromBackground();
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[App] 显示窗口失败：{ex.Message}");
                }
            }
        })
        {
            IsBackground = true,
            Name = "ShowWindowListener"
        };
        thread.Start();
    }

    private void App_OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        WriteCrashLog(e.Exception);
        var cw = GetService<CrashWindow>();
        cw.CrashInfo = e.Exception.ToString();
        cw.Exception = e.Exception;
        cw.OpenWindow();
    }

    /// <summary>
    /// 把未处理异常写入数据目录下的 crash.log，方便用户反馈问题时附上。
    /// </summary>
    private static void WriteCrashLog(Exception exception)
    {
        try
        {
            var path = Path.Combine(AppStorage.DataDirectory, "crash.log");
            Directory.CreateDirectory(AppStorage.DataDirectory);
            File.AppendAllText(path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}{Environment.NewLine}",
                Encoding.UTF8);
            Debug.WriteLine($"[App] 崩溃日志已写入：{path}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] 写入崩溃日志失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 系统关机/注销时触发。这里必须把数据立即写入磁盘，否则关机会直接结束进程导致数据丢失。
    /// </summary>
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        if (MainWindow is MainWindow window)
        {
            // 允许主窗口正常关闭，否则 MainWindow_OnClosing 会取消关闭
            window.ViewModel.IsClosing = true;
        }

        FlushStorage();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _tray?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] 释放托盘图标失败：{ex.Message}");
        }

        FlushStorage();
        base.OnExit(e);
    }

    private static void FlushStorage()
    {
        try
        {
            GetService<ProfileService>().Flush();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] 保存作业数据失败：{ex.Message}");
        }

        try
        {
            GetService<SettingsService>().Flush();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] 保存设置失败：{ex.Message}");
        }
    }

    public static void ReleaseLock()
    {
        Mutex?.ReleaseMutex();
    }
}
