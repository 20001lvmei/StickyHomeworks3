using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ElysiaFramework;
using StickyHomeworks.Models;
using StickyHomeworks.Views;

namespace StickyHomeworks.Controls;

/// <summary>
/// HomeworkControl.xaml 的交互逻辑
/// </summary>
public partial class HomeworkControl : UserControl
{
    public static readonly DependencyProperty HomeworkProperty = DependencyProperty.Register(
        nameof(Homework), typeof(Homework), typeof(HomeworkControl), new PropertyMetadata(default(Homework)));

    public Homework Homework
    {
        get { return (Homework)GetValue(HomeworkProperty); }
        set { SetValue(HomeworkProperty, value); }
    }

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(HomeworkControl), new PropertyMetadata(default(bool), (o, args) =>
        {
            var c = o as HomeworkControl;
            c?.IsSelectedChanged((bool)args.NewValue);
        }));

    public bool IsSelected
    {
        get { return (bool)GetValue(IsSelectedProperty); }
        set { SetValue(IsSelectedProperty, value); }
    }

    public static readonly DependencyProperty IsEditingProperty = DependencyProperty.Register(
        nameof(IsEditing), typeof(bool), typeof(HomeworkControl), new PropertyMetadata(default(bool),
            (o, args) =>
            {
                var c = o as HomeworkControl;
                c?.IsEditingChanged((bool)args.NewValue);
            }));


    public bool IsEditing
    {
        get { return (bool)GetValue(IsEditingProperty); }
        set { SetValue(IsEditingProperty, value); }
    }

    /// <summary>
    /// 作业在自己的科目分组里的序号（从 1 开始），显示在内容前面。
    /// </summary>
    public static readonly DependencyProperty NumberProperty = DependencyProperty.Register(
        nameof(Number), typeof(int), typeof(HomeworkControl), new PropertyMetadata(0));

    public int Number
    {
        get { return (int)GetValue(NumberProperty); }
        set { SetValue(NumberProperty, value); }
    }

    public HomeworkControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            RefreshNumber();
            UpdateReadOnly();
            ApplyMarkerStyle();
            HookFrozenChanged();
        };
        LayoutUpdated += (_, _) => RefreshNumber();
        Unloaded += (_, _) => UnhookFrozenChanged();
    }

    /// <summary>
    /// 选中且没冻结时可直接编辑，否则只读。
    /// </summary>
    private void UpdateReadOnly()
    {
        var frozen = false;
        try
        {
            frozen = AppEx.GetService<StickyHomeworks.Services.SettingsService>().Settings.IsFrozen;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HomeworkControl] 读取冻结状态失败：{ex.Message}");
        }

        var readOnly = !(IsSelected && !frozen);
        if (RichTextBox.IsReadOnly != readOnly)
        {
            RichTextBox.IsReadOnly = readOnly;
        }

#if DEBUG
        if (Environment.GetEnvironmentVariable("STICKYHOMEWORKS3_SELFTEST") == "1")
        {
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(StickyHomeworks.Services.AppStorage.DataDirectory, "selftest.log"),
                    $"control selected={IsSelected} frozen={frozen} readOnly={readOnly}{Environment.NewLine}");
            }
            catch
            {
                // 自测日志失败不影响功能
            }
        }
#endif
    }

    private StickyHomeworks.Services.SettingsService? _settingsService;

    private void HookFrozenChanged()
    {
        if (_settingsService != null)
        {
            return;
        }

        try
        {
            _settingsService = AppEx.GetService<StickyHomeworks.Services.SettingsService>();
            _settingsService.Settings.PropertyChanged += OnSettingsPropertyChanged;
            UpdateReadOnly();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HomeworkControl] 监听冻结状态失败：{ex.Message}");
        }
    }

    private void UnhookFrozenChanged()
    {
        if (_settingsService == null)
        {
            return;
        }

        _settingsService.Settings.PropertyChanged -= OnSettingsPropertyChanged;
        _settingsService = null;
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StickyHomeworks.Models.Settings.IsFrozen))
        {
            UpdateReadOnly();
        }

        if (e.PropertyName == nameof(StickyHomeworks.Models.Settings.IsNumberedList))
        {
            ApplyMarkerStyle();
        }
    }

    /// <summary>
    /// 按设置显示数字序号或圆点。
    /// </summary>
    private void ApplyMarkerStyle()
    {
        var numbered = true;
        try
        {
            numbered = AppEx.GetService<StickyHomeworks.Services.SettingsService>().Settings.IsNumberedList;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HomeworkControl] 读取列表标记设置失败：{ex.Message}");
        }

        NumberText.Visibility = numbered ? Visibility.Visible : Visibility.Collapsed;
        DotMarker.Visibility = numbered ? Visibility.Collapsed : Visibility.Visible;

#if DEBUG
        if (Environment.GetEnvironmentVariable("STICKYHOMEWORKS3_SELFTEST") == "1")
        {
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(StickyHomeworks.Services.AppStorage.DataDirectory, "selftest.log"),
                    $"marker numbered={numbered} numberVisible={NumberText.Visibility} dotVisible={DotMarker.Visibility}{Environment.NewLine}");
            }
            catch
            {
                // 自测日志失败不影响功能
            }
        }
#endif
    }

    /// <summary>
    /// 按所在分组面板里的位置算出序号（每个科目从 1 开始）。
    /// </summary>
    private void RefreshNumber()
    {
        try
        {
            var item = FindAncestor<ListBoxItem>(this);
            if (item == null)
            {
                return;
            }

            var panel = item.Parent as Panel ?? VisualTreeHelper.GetParent(item) as Panel;
            if (panel == null)
            {
                return;
            }

            var index = 0;
            foreach (UIElement child in panel.Children)
            {
                if (ReferenceEquals(child, item))
                {
                    break;
                }

                if (child is ListBoxItem)
                {
                    index++;
                }
            }

            var number = index + 1;
            if (Number != number)
            {
                Number = number;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HomeworkControl] 计算序号失败：{ex.Message}");
        }
    }

    private void IsEditingChanged(bool value)
    {
        Debug.WriteLine($"IsEditing changed! {value} {IsSelected}");
        if (IsSelected && value)
        {
            Debug.WriteLine("RelatedRichTextBox updated because IsEditing changed");
            EnterEdit();
        }
    }

    private async void EnterEdit()
    {
        AppEx.GetService<HomeworkEditWindow>().RelatedRichTextBox = RichTextBox;
        await System.Windows.Threading.Dispatcher.Yield();
        RichTextBox.Focus();
        //RichTextBox.Selection.Select(new TextPointer());
        RichTextBox.CaretPosition = RichTextBox.CaretPosition.DocumentEnd;
    }

    private void IsSelectedChanged(bool value)
    {
        UpdateReadOnly();
        if (value && IsEditing)
        {
            EnterEdit();
        }
    }

    private void RichTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            App.GetService<MainWindow>().OnTextBoxEnter();
        }
    }

    /// <summary>
    /// 点作业内容：选中整条作业，并把这次点击继续交给富文本框，
    /// 这样不用先点“编辑”就能直接把光标点到文字上开始输入。
    /// </summary>
    private void RichTextBox_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsSelected)
        {
            return;
        }

        var item = FindAncestor<ListBoxItem>(this);
        if (item != null)
        {
            item.IsSelected = true;
            // 注意：这里不设置 e.Handled，保留光标定位的能力
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
            {
                return match;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    /// <summary>
    /// 点击作业里的链接时用默认浏览器打开（只读状态下也能点）。
    /// </summary>
    private void RichTextBox_OnRequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HomeworkControl] 打开链接失败：{ex.Message}");
        }

        e.Handled = true;
    }
}