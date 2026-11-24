using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StickyHomeworks.Views;

/// <summary>
/// 简单的文本输入对话框（模板命名、重命名等场景使用）。
/// </summary>
public class TextPromptWindow : Window
{
    private readonly TextBox _textBox;

    private TextPromptWindow(string title, string message, string initial, bool multiline)
    {
        Title = title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI");

        _textBox = new TextBox
        {
            Text = initial,
            Margin = new Thickness(0, 6, 0, 0),
            Padding = new Thickness(6, 4, 6, 4),
            AcceptsReturn = multiline,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = multiline ? 120 : 32,
            VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled
        };

        var ok = new Button
        {
            Content = "确定",
            Width = 72,
            Margin = new Thickness(6, 0, 0, 0),
            IsDefault = true
        };
        ok.Click += (_, _) => DialogResult = true;

        var cancel = new Button
        {
            Content = "取消",
            Width = 72,
            IsCancel = true
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(_textBox);
        panel.Children.Add(buttons);

        Content = panel;
        Loaded += (_, _) =>
        {
            _textBox.Focus();
            _textBox.SelectAll();
        };
    }

    /// <summary>
    /// 弹出输入框，取消时返回 null。
    /// </summary>
    public static string? Show(Window? owner, string title, string message, string initial = "")
    {
        var window = new TextPromptWindow(title, message, initial, false);
        if (owner != null && owner.IsVisible)
        {
            window.Owner = owner;
        }

        return window.ShowDialog() == true ? window._textBox.Text : null;
    }
}
