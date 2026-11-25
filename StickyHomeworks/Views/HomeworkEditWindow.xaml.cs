using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ElysiaFramework;
using StickyHomeworks.Models;
using StickyHomeworks.Services;
using StickyHomeworks.ViewModels;
using Path = System.IO.Path;

namespace StickyHomeworks.Views;

/// <summary>
/// HomeworkEditWindow.xaml 的交互逻辑
/// </summary>
public partial class HomeworkEditWindow : Window, INotifyPropertyChanged
{
    private RichTextBox _relatedRichTextBox = new();
    public MainWindow MainWindow { get; }
    public SettingsService SettingsService { get; }

    public HomeworkEditViewModel ViewModel { get; } = new();

    public bool IsOpened { get; set; } = false;

    public event EventHandler? EditingFinished;

    public event EventHandler? SubjectChanged;

    public void TryOpen()
    {
        if (IsOpened)
            return;
        Show();
        Activate();
        IsOpened = true;
    }

    public void TryClose()
    {
        if (!IsOpened)
            return;
        IsOpened = false;
        Hide();
    }

    public RichTextBox RelatedRichTextBox
    {
        get => _relatedRichTextBox;
        set
        {
            UnregisterOldTextBox(_relatedRichTextBox);
            RegisterNewTextBox(value);
            _relatedRichTextBox = value;
            OnPropertyChanged();
        }
    }

    public HomeworkEditWindow(MainWindow mainWindow, SettingsService settingsService)
    {
        MainWindow = mainWindow;
        SettingsService = settingsService;
        DataContext = this;
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
    }

    protected override void OnInitialized(EventArgs e)
    {
        ViewModel.FontFamilies =
            new ObservableCollection<FontFamily>(from i in Fonts.SystemFontFamilies orderby i.ToString() select i)
                { (FontFamily)FindResource("HarmonyOsSans") };
        base.OnInitialized(e);
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel.IsRestoringSelection)
        {
            return;
        }
        var s = RelatedRichTextBox.Selection;
        switch (e.PropertyName)
        {
            case nameof(ViewModel.TextColor):
                s.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(ViewModel.TextColor));
                break;
            case nameof(ViewModel.Font):
                s.ApplyPropertyValue(TextElement.FontFamilyProperty, ViewModel.Font);
                break;
            case nameof(ViewModel.FontSize):
                s.ApplyPropertyValue(TextElement.FontSizeProperty, Math.Max(ViewModel.FontSize, 8));
                break;
        }
    }

    private void RegisterNewTextBox(RichTextBox richTextBox)
    {
        richTextBox.TextChanged += RichTextBoxOnTextChanged;
        richTextBox.SelectionChanged += RichTextBoxOnSelectionChanged;
    }

    private void RichTextBoxOnSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsRestoringSelection)
            return;
        Debug.WriteLine("selection changed!");
        if (RelatedRichTextBox.Selection.Start.Paragraph != null)
            ViewModel.SelectedParagraph = RelatedRichTextBox.Selection.Start.Paragraph;
        // Update selection
        var s = RelatedRichTextBox.Selection;
        if (!MainWindow.IsActive)
        {
            if (ViewModel is not { BeforeTextPointerStart: not null, BeforeTextPointerEnd: not null })
                return;
            ViewModel.IsRestoringSelection = true;
            RelatedRichTextBox.Selection.Select(ViewModel.BeforeTextPointerStart, ViewModel.BeforeTextPointerEnd);
            ViewModel.IsRestoringSelection = false;
            return;
        }
        ViewModel.Selection = s;
        ViewModel.BeforeTextPointerStart = s.Start;
        ViewModel.BeforeTextPointerEnd = s.End;
        ViewModel.IsRestoringSelection = true;
        Debug.WriteLine("selection updated!");
        var w = s.GetPropertyValue(TextElement.FontWeightProperty);
        if (w is FontWeight weight)
        {
            ViewModel.IsBold = weight >= FontWeights.Bold;
        }

        ViewModel.IsItalic = Equals(s.GetPropertyValue(TextElement.FontStyleProperty), FontStyles.Italic);
        if (s.GetPropertyValue(Paragraph.TextDecorationsProperty) is TextDecorationCollection decorations)
        {
            ViewModel.IsUnderlined = decorations.Contains(TextDecorations.Underline[0]);
            ViewModel.IsStrikeThrough = decorations.Contains(TextDecorations.Strikethrough[0]);
        }

        if (s.GetPropertyValue(TextElement.ForegroundProperty) is SolidColorBrush fg)
        {
            ViewModel.TextColor = fg.Color;
        }
        if (s.GetPropertyValue(TextElement.FontFamilyProperty) is FontFamily font)
        {
            ViewModel.Font = font;
        }

        if (s.GetPropertyValue(TextElement.FontSizeProperty) is double fontSize)
        {
            ViewModel.FontSize = fontSize;
        }
        ViewModel.IsRestoringSelection = false;
    }

    private void RichTextBoxOnTextChanged(object sender, TextChangedEventArgs e)
    {
    }

    private void UnregisterOldTextBox(RichTextBox richTextBox)
    {
        richTextBox.TextChanged -= RichTextBoxOnTextChanged;
        richTextBox.SelectionChanged -= RichTextBoxOnSelectionChanged;
    }

    private void ListBoxTextStyles_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.IsRestoringSelection)
            return;
        var s = RelatedRichTextBox.Selection;
        s.ApplyPropertyValue(TextElement.FontWeightProperty, ViewModel.IsBold? FontWeights.Bold : FontWeights.Regular);
        s.ApplyPropertyValue(TextElement.FontStyleProperty, ViewModel.IsItalic ? FontStyles.Italic : FontStyles.Normal);
        var decorations = new TextDecorationCollection();
        if (ViewModel.IsUnderlined)
            decorations.Add(TextDecorations.Underline);
        if (ViewModel.IsStrikeThrough)
            decorations.Add(TextDecorations.Strikethrough);
        s.ApplyPropertyValue(Paragraph.TextDecorationsProperty, decorations);
        RelatedRichTextBox.Focus();
    }

    private void ButtonClearColor_OnClick(object sender, RoutedEventArgs e)
    {
        var s = RelatedRichTextBox.Selection;
        s.ApplyPropertyValue(TextElement.ForegroundProperty, GetValue(TextElement.ForegroundProperty));
    }

    private void ButtonFontSizeDecrease_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.FontSize -= 2;
    }

    private void ButtonFontSizeIncrease_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.FontSize += 2;
    }

    private void ButtonTemplates_OnClick(object sender, RoutedEventArgs e)
    {
        var templates = SettingsService.Settings.Templates;
        if (templates.Count == 0)
        {
            MessageBox.Show("还没有模板，可以先写好一条作业再点旁边的「存为模板」。", "套用模板",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var menu = new ContextMenu();
        foreach (var template in templates)
        {
            var item = new MenuItem { Header = template.Name };
            item.Click += (_, _) => ApplyTemplate(template);
            menu.Items.Add(item);
        }

        menu.PlacementTarget = sender as UIElement;
        menu.IsOpen = true;
    }

    private void ApplyTemplate(HomeworkTemplate template)
    {
        try
        {
            var homework = MainWindow.ViewModel.SelectedHomework ?? MainWindow.ViewModel.EditingHomework;
            homework.Content = template.Content;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Editor] 套用模板失败：{ex}");
            MessageBox.Show($"套用模板失败：{ex.Message}", "套用模板",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ButtonSaveAsTemplate_OnClick(object sender, RoutedEventArgs e)
    {
        var homework = MainWindow.ViewModel.SelectedHomework ?? MainWindow.ViewModel.EditingHomework;
        if (string.IsNullOrWhiteSpace(RichTextBoxHelper.GetPlainText(homework.Content)))
        {
            MessageBox.Show("当前作业还是空的，先写点内容再存模板吧。", "存为模板",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var name = TextPromptWindow.Show(this, "存为模板", "给这个模板起个名字：",
            $"模板 {SettingsService.Settings.Templates.Count + 1}");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        SettingsService.Settings.Templates.Add(new HomeworkTemplate
        {
            Name = name.Trim(),
            Content = homework.Content
        });
        SettingsService.SaveSettings();
    }

    private static readonly string[] Emojis =
    {
        "😀", "😄", "😁", "😆", "😅", "😂", "🙂", "😉", "😊", "😍",
        "😘", "😜", "😎", "🤔", "😐", "😴", "😭", "😱", "😡", "🥺",
        "👍", "👎", "👏", "🙏", "💪", "✌️", "👌", "🤝", "❤️", "💔",
        "⭐", "🌟", "✨", "🔥", "🎉", "🎁", "📚", "📝", "✏️", "📌",
        "✅", "❌", "⚠️", "❓", "❗", "💡", "⏰", "☀️", "🌙", "🍀"
    };

    private void ButtonEmoji_OnClick(object sender, RoutedEventArgs e)
    {
        var panel = new WrapPanel { Width = 280, Background = Brushes.White };

        foreach (var emoji in Emojis)
        {
            var button = new Button
            {
                Content = emoji,
                Width = 32,
                Height = 32,
                FontSize = 17,
                Padding = new Thickness(0),
                Margin = new Thickness(1),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            button.Click += (_, _) =>
            {
                InsertText(emoji);
                EmojiPopup.IsOpen = false;
            };
            panel.Children.Add(button);
        }

        EmojiPopup.Child = new Border
        {
            Child = panel,
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
            BorderThickness = new Thickness(1)
        };
        EmojiPopup.PlacementTarget = sender as UIElement;
        EmojiPopup.IsOpen = true;
    }

    /// <summary>
    /// 在光标处插入一段文字。
    /// </summary>
    private void InsertText(string text)
    {
        try
        {
            var caret = RelatedRichTextBox.CaretPosition;
            caret.InsertTextInRun(text);
            RelatedRichTextBox.Focus();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Editor] 插入文字失败：{ex.Message}");
        }
    }

    private void ButtonLink_OnClick(object sender, RoutedEventArgs e)
    {
        var url = TextPromptWindow.Show(this, "插入链接", "链接地址：", "https://");
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        url = url.Trim();
        var text = TextPromptWindow.Show(this, "插入链接", "显示的文字（留空则直接显示网址）：", url);
        var display = string.IsNullOrWhiteSpace(text) ? url : text.Trim();

        try
        {
            var uri = new Uri(url);
            var paragraph = RelatedRichTextBox.CaretPosition.Paragraph;
            if (paragraph == null)
            {
                return;
            }

            var hyperlink = new Hyperlink(new Run(display)) { NavigateUri = uri, ToolTip = url };
            hyperlink.RequestNavigate += (_, args) =>
            {
                OpenUrl(args.Uri.AbsoluteUri);
                args.Handled = true;
            };
            paragraph.Inlines.Add(hyperlink);
            RelatedRichTextBox.Focus();
        }
        catch (UriFormatException)
        {
            MessageBox.Show("链接地址格式不对，需要以 http:// 或 https:// 开头。", "插入链接",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ButtonImage_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "插入图片",
            Filter = "图片 (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            // 复制到数据目录，避免原图被移动/删除后作业里显示不出来
            var imageDirectory = Path.Combine(AppStorage.DataDirectory, "images");
            Directory.CreateDirectory(imageDirectory);
            var target = Path.Combine(imageDirectory, $"{Guid.NewGuid():N}{Path.GetExtension(dialog.FileName)}");
            File.Copy(dialog.FileName, target, true);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(target);
            bitmap.EndInit();

            var image = new Image
            {
                Source = bitmap,
                MaxWidth = 240,
                MaxHeight = 240,
                Stretch = Stretch.Uniform
            };

            var paragraph = RelatedRichTextBox.CaretPosition.Paragraph;
            if (paragraph == null)
            {
                return;
            }

            paragraph.Inlines.Add(new InlineUIContainer(image));
            RelatedRichTextBox.Focus();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Editor] 插入图片失败：{ex}");
            MessageBox.Show($"插入图片失败：{ex.Message}", "插入图片",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Editor] 打开链接失败：{ex.Message}");
        }
    }

    private void ButtonEditingDone_OnClick(object sender, RoutedEventArgs e)
    {
        EditingFinished?.Invoke(this, EventArgs.Empty);
        AppEx.GetService<ProfileService>().SaveProfile();
    }

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SubjectChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ButtonAddToColor_OnClick(object sender, RoutedEventArgs e)
    {
        if (SettingsService.Settings.SavedColors.Contains(ViewModel.TextColor))
            return;
        SettingsService.Settings.SavedColors.Insert(0, ViewModel.TextColor);
        while (SettingsService.Settings.SavedColors.Count > 6)
        {
            SettingsService.Settings.SavedColors.RemoveAt(6);
        }
        SettingsService.SaveSettings();
    }

    private void ListBoxColors_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.IsUpdatingColor)
            return;
        ViewModel.IsUpdatingColor = true;
        foreach (var i in e.AddedItems)
        {
            if (i is Color c)
                ViewModel.TextColor = c;
        }

        if (sender is ListBox l)
            l.SelectedIndex = -1;
        ViewModel.IsUpdatingColor = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}