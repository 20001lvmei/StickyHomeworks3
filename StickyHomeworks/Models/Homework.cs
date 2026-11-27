using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StickyHomeworks.Models;

public class Homework : ObservableRecipient
{
    private string _content = "";
    private string _subject = "";
    private DateTime _dueTime = DateTime.Today;
    private ObservableCollection<string> _tags = new();
    private bool _isArchiveExempt = false;
    private Guid _id = Guid.NewGuid();

    /// <summary>
    /// 稳定标识，网页版/接口用它定位作业。
    /// 旧数据里没有这个字段时会在加载时随机生成。
    /// </summary>
    public Guid Id
    {
        get => _id;
        set
        {
            if (value == _id) return;
            _id = value;
            OnPropertyChanged();
        }
    }

    public string Content
    {
        get => _content;
        set
        {
            if (value == _content) return;
            _content = value;
            OnPropertyChanged();
        }
    }

    public string Subject
    {
        get => _subject;
        set
        {
            if (value == _subject) return;
            _subject = value;
            OnPropertyChanged();
        }
    }

    public DateTime DueTime
    {
        get => _dueTime;
        set
        {
            if (value.Equals(_dueTime)) return;
            _dueTime = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<string> Tags
    {
        get => _tags;
        set
        {
            if (Equals(value, _tags)) return;
            _tags = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 旧版本留下的“豁免自动归档”标记。现在自动归档不再看它（只要过期就归档），
    /// 保留字段只是为了兼容旧数据、原样写回，不做任何判断。
    /// </summary>
    public bool IsArchiveExempt
    {
        get => _isArchiveExempt;
        set
        {
            if (value == _isArchiveExempt) return;
            _isArchiveExempt = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 保留旧版本作业里本版本还不认识的字段（例如 IsExpired、FirstExpiredShowTime），
    /// 保存时原样写回，避免旧版本再打开时丢数据。
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraData { get; set; }
}