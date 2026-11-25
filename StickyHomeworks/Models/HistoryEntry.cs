using CommunityToolkit.Mvvm.ComponentModel;

namespace StickyHomeworks.Models;

/// <summary>
/// 一条历史记录：作业被删除或归档时留下的快照。
/// </summary>
public class HistoryEntry : ObservableObject
{
    private string _subject = "";
    private string _content = "";
    private DateTime _dueTime = DateTime.Today;
    private DateTime _recordedAt = DateTime.Now;
    private string _reason = "";

    /// <summary>科目。</summary>
    public string Subject
    {
        get => _subject;
        set
        {
            if (value == _subject) return;
            _subject = value;
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

    /// <summary>原本的截止日期。</summary>
    public DateTime DueTime
    {
        get => _dueTime;
        set
        {
            if (value == _dueTime) return;
            _dueTime = value;
            OnPropertyChanged();
        }
    }

    /// <summary>进入历史的时间。</summary>
    public DateTime RecordedAt
    {
        get => _recordedAt;
        set
        {
            if (value == _recordedAt) return;
            _recordedAt = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RecordedAtText));
        }
    }

    /// <summary>原因：删除 / 过期归档。</summary>
    public string Reason
    {
        get => _reason;
        set
        {
            if (value == _reason) return;
            _reason = value;
            OnPropertyChanged();
        }
    }

    public string RecordedAtText => RecordedAt.ToString("yyyy-MM-dd HH:mm");

    public string Preview
    {
        get
        {
            var text = StickyHomeworks.RichTextBoxHelper.GetPlainText(Content)
                .Replace('\n', ' ')
                .Replace('\r', ' ');
            return text.Length > 60 ? text[..60] + "…" : text;
        }
    }

    public static HistoryEntry FromHomework(Homework homework, string reason) => new()
    {
        Subject = homework.Subject,
        Content = homework.Content,
        DueTime = homework.DueTime,
        RecordedAt = DateTime.Now,
        Reason = reason
    };

    public Homework ToHomework() => new()
    {
        Subject = Subject,
        Content = Content,
        DueTime = DateTime.Today,
        IsArchiveExempt = true
    };
}
