using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace StickyHomeworks.Services;

/// <summary>
/// 右下角任务栏通知区域（托盘）图标。桌面作业贴这类常驻程序放托盘更合适，
/// 而不是占据左侧任务栏按钮。
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public TrayService(Action showWindow, Action toggleWindow, Action openSettings, Action openTimeMachine, Action exit)
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "StickyHomeworks3",
            Icon = LoadIcon(),
            Visible = true
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("显示窗口", null, (_, _) => showWindow());
        menu.Items.Add("隐藏窗口", null, (_, _) => toggleWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("时间机器…", null, (_, _) => openTimeMachine());
        menu.Items.Add("设置…", null, (_, _) => openSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());

        _notifyIcon.ContextMenuStrip = menu;

        // 左键单击/双击都只负责“把窗口叫出来”，不切换，避免双击导致忽隐忽现
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                showWindow();
            }
        };
    }

    private static Icon LoadIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
            {
                var icon = Icon.ExtractAssociatedIcon(path);
                if (icon != null)
                {
                    return icon;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Tray] 读取程序图标失败：{ex.Message}");
        }

        return SystemIcons.Application;
    }

    public void ShowBalloon(string title, string text)
    {
        try
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = text;
            _notifyIcon.ShowBalloonTip(3000);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Tray] 显示气泡提示失败：{ex.Message}");
        }
    }

    public void Dispose()
    {
        try
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Tray] 释放托盘图标失败：{ex.Message}");
        }

        GC.SuppressFinalize(this);
    }
}
