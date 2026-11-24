using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StickyHomeworks.Models;

public class Profile : ObservableRecipient
{
    /// <summary>
    /// 当前显示的作业。
    /// </summary>
    public ObservableCollection<Homework> Homeworks { get; set; } = new();

    /// <summary>
    /// 已归档的过期作业。过期作业会被移动到这里而不是直接删除，避免数据丢失。
    /// 旧版本的 Profile.json 中没有这个字段，反序列化后会得到空集合。
    /// </summary>
    public ObservableCollection<Homework> ArchivedHomeworks { get; set; } = new();
}
