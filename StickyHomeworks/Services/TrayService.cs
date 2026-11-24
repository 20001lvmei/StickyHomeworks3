using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace StickyHomeworks.Services;

/// <summary>
/// 右下角任务栏通知区域（托盘）图标，菜单与旧版保持一致：
/// 显示主界面（带勾选）/ 设置… / 重启 / 退出。
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public TrayService(Func<bool> isWindowVisible, Action toggleWindow, Action openSettings, Action restart, Action exit)
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "StickyHomeworks3",
            Icon = LoadAppIcon(),
            Visible = true
        };

        var menu = new ContextMenuStrip { ShowImageMargin = true };

        // 顶部标题（应用名 + 图标）
        var header = new ToolStripLabel("StickyHomeworks3")
        {
            Image = LoadAppIconBitmap(),
            Font = new Font(menu.Font, System.Drawing.FontStyle.Bold),
            Margin = new Padding(4, 4, 4, 2),
            ImageScaling = ToolStripItemImageScaling.None
        };
        menu.Items.Add(header);

        // 显示主界面（勾选表示当前可见）
        var showItem = new ToolStripMenuItem("显示主界面", GlyphIcon("\uE7C4"))
        {
            CheckOnClick = false
        };
        showItem.Click += (_, _) => toggleWindow();
        menu.Items.Add(showItem);

        menu.Items.Add(new ToolStripSeparator());

        var settingsItem = new ToolStripMenuItem("设置...", GlyphIcon("\uE713"));
        settingsItem.Click += (_, _) => openSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new ToolStripSeparator());

        var restartItem = new ToolStripMenuItem("重启", GlyphIcon("\uE72C"));
        restartItem.Click += (_, _) => restart();
        menu.Items.Add(restartItem);

        var exitItem = new ToolStripMenuItem("退出", GlyphIcon("\uE7E8"));
        exitItem.Click += (_, _) => exit();
        menu.Items.Add(exitItem);

        menu.Opening += (_, _) =>
        {
            try
            {
                showItem.Checked = isWindowVisible();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Tray] 更新菜单状态失败：{ex.Message}");
            }
        };

        _notifyIcon.ContextMenuStrip = menu;

        // 左键单击：把窗口叫出来
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                toggleWindow();
            }
        };
    }

    /// <summary>
    /// 用 Segoe MDL2 Assets 字形画一个 16×16 菜单图标。
    /// </summary>
    private static Image? GlyphIcon(string glyph)
    {
        try
        {
            var bitmap = new Bitmap(16, 16);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            graphics.Clear(Color.Transparent);
            using var font = new Font("Segoe MDL2 Assets", 10f, System.Drawing.FontStyle.Regular, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(Color.FromArgb(90, 90, 90));
            graphics.DrawString(glyph, font, brush, new PointF(0, 0));
            return bitmap;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Tray] 生成菜单图标失败：{ex.Message}");
            return null;
        }
    }

    private static Icon LoadAppIcon()
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

    private static Image? LoadAppIconBitmap()
    {
        try
        {
            using var icon = LoadAppIcon();
            return icon.ToBitmap();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Tray] 转换程序图标失败：{ex.Message}");
            return null;
        }
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
