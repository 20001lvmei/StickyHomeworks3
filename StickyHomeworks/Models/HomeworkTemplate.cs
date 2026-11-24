using CommunityToolkit.Mvvm.ComponentModel;

namespace StickyHomeworks.Models;

/// <summary>
/// 作业模板：把常用作业内容存下来，新建作业时一键套用。
/// </summary>
public class HomeworkTemplate : ObservableObject
{
    private string _name = "";
    private string _content = "";

    public string Name
    {
        get => _name;
        set
        {
            if (value == _name) return;
            _name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Preview));
        }
    }

    /// <summary>作业内容（FlowDocument 的 XAML）。</summary>
    public string Content
    {
        get => _content;
        set
        {
            if (value == _content) return;
            _content = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Preview));
        }
    }

    /// <summary>纯文本预览。</summary>
    public string Preview
    {
        get
        {
            var text = StickyHomeworks.RichTextBoxHelper.GetPlainText(Content).Replace('\n', ' ').Replace('\r', ' ');
            return text.Length > 60 ? text[..60] + "…" : text;
        }
    }
}
