using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using WindowsShortcutFactory;

namespace StickyHomeworks.Services;

/// <summary>
/// 开机自启的实际落地：优先在“启动”文件夹创建快捷方式，失败时回退到注册表 Run 项。
/// 设置里的开关只保存“用户意愿”，真正的注册在启动时和开关变化时应用，
/// 这样即使某次写入失败，开关也不会自己弹回去。
/// </summary>
public static class AutoStartHelper
{
    private const string ShortcutFileName = "StickyHomeworks3.lnk";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string RunValueName = "StickyHomeworks3";

    private static readonly string[] LegacyShortcutNames = { "StickyHomeworks.lnk" };

    private static string StartupDirectory =>
        Environment.GetFolderPath(Environment.SpecialFolder.Startup);

    private static string ShortcutPath => Path.Combine(StartupDirectory, ShortcutFileName);

    private static string? ExecutablePath =>
        Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;

    /// <summary>
    /// 应用开机自启设置。返回是否成功（关闭时总是成功）。
    /// </summary>
    public static bool Apply(bool enabled)
    {
        try
        {
            return enabled ? Enable() : !Disable();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AutoStart] 应用开机自启失败：{ex.Message}");
            return false;
        }
    }

    private static bool Enable()
    {
        var exe = ExecutablePath;
        if (string.IsNullOrWhiteSpace(exe))
        {
            Debug.WriteLine("[AutoStart] 无法获取程序路径。");
            return false;
        }

        var created = false;
        try
        {
            using var shortcut = new WindowsShortcut();
            shortcut.Path = exe;
            shortcut.WorkingDirectory = AppContext.BaseDirectory;
            shortcut.Save(ShortcutPath);
            created = File.Exists(ShortcutPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AutoStart] 创建启动快捷方式失败：{ex.Message}");
        }

        if (!created)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
                if (key == null)
                {
                    return false;
                }

                key.SetValue(RunValueName, $"\"{exe}\"");
                created = true;
                Debug.WriteLine("[AutoStart] 已改用注册表 Run 项实现开机自启。");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoStart] 写入注册表 Run 项失败：{ex.Message}");
            }
        }

        if (created)
        {
            RemoveLegacyShortcuts();
        }

        return created;
    }

    /// <summary>
    /// 关闭开机自启。返回是否发生错误。
    /// </summary>
    private static bool Disable()
    {
        var failed = false;

        try
        {
            if (File.Exists(ShortcutPath))
            {
                File.Delete(ShortcutPath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AutoStart] 删除启动快捷方式失败：{ex.Message}");
            failed = true;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key?.GetValue(RunValueName) != null)
            {
                key.DeleteValue(RunValueName, false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AutoStart] 删除注册表 Run 项失败：{ex.Message}");
            failed = true;
        }

        RemoveLegacyShortcuts();
        return failed;
    }

    private static void RemoveLegacyShortcuts()
    {
        foreach (var legacyName in LegacyShortcutNames)
        {
            try
            {
                var legacyPath = Path.Combine(StartupDirectory, legacyName);
                if (File.Exists(legacyPath))
                {
                    File.Delete(legacyPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoStart] 清理旧快捷方式失败：{ex.Message}");
            }
        }
    }
}
