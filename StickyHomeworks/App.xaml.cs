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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StickyHomeworks.Core.Context;
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
        if (!createNew)
        {
            MessageBox.Show("应用已经在运行中，请勿重复启动第二个实例。");
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
                services.AddDbContext<AppDbContext>();
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
        try
        {
            // 表情包相关的数据库现在保存在用户数据目录中，首次启动时创建表结构
            GetService<AppDbContext>().Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] 初始化数据库失败：{ex.Message}");
        }
        MainWindow = GetService<MainWindow>();
        GetService<MainWindow>().Show();
        base.OnStartup(e);
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
