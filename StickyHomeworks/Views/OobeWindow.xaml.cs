using System.Windows;
using System.Windows.Controls;
using StickyHomeworks.Services;

namespace StickyHomeworks.Views;

/// <summary>
/// 首次启动引导（OOBE）：选科目、决定是否开机自启。
/// </summary>
public partial class OobeWindow : Window
{
    private static readonly string[] PresetSubjects =
    {
        "语文", "数学", "外语", "物理", "化学", "生物", "政治", "历史", "地理", "技术", "体育", "美术", "音乐"
    };

    private readonly SettingsService _settingsService;

    public OobeWindow(SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;

        foreach (var subject in PresetSubjects)
        {
            SubjectsPanel.Children.Add(new CheckBox
            {
                Content = subject,
                IsChecked = true,
                FontSize = 14,
                Margin = new Thickness(0, 0, 18, 12)
            });
        }

        AutoStartCheck.IsChecked = settingsService.Settings.IsAutoStartEnabled;
    }

    private void ButtonStart_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = SubjectsPanel.Children
            .OfType<CheckBox>()
            .Where(c => c.IsChecked == true)
            .Select(c => c.Content?.ToString() ?? "")
            .Where(s => s.Length > 0)
            .ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show("请至少选择一个科目。", "StickyHomeworks3",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var settings = _settingsService.Settings;
        settings.Subjects.Clear();
        foreach (var subject in selected)
        {
            settings.Subjects.Add(subject);
        }

        // 新建作业时默认用“其它”作为科目
        if (!settings.Subjects.Contains("其它"))
        {
            settings.Subjects.Add("其它");
        }

        settings.IsAutoStartEnabled = AutoStartCheck.IsChecked == true;
        settings.IsFirstRunCompleted = true;

        _settingsService.SaveSettings();
        DialogResult = true;
    }
}
