using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using StickyHomeworks.Models;

namespace StickyHomeworks.Services;

/// <summary>
/// 作业历史：作业被删除或过期归档时保存快照，可随时找回来。
/// </summary>
public class HistoryService
{
    private const string FileName = "History.json";

    private const int MaxEntries = 300;

    public ObservableCollection<HistoryEntry> Entries { get; } = new();

    public HistoryService()
    {
        AppStorage.EnsureDataDirectory();
        var loaded = AppStorage.Load<HistoryStore>(FileName);
        if (loaded?.Entries != null)
        {
            foreach (var entry in loaded.Entries)
            {
                Entries.Add(entry);
            }
        }
    }

    /// <summary>
    /// 记一条历史。
    /// </summary>
    public void Add(Homework homework, string reason)
    {
        if (string.IsNullOrWhiteSpace(RichTextBoxHelper.GetPlainText(homework.Content)))
        {
            return;
        }

        Entries.Insert(0, HistoryEntry.FromHomework(homework, reason));
        while (Entries.Count > MaxEntries)
        {
            Entries.RemoveAt(Entries.Count - 1);
        }

        Save();
    }

    /// <summary>
    /// 把历史条目恢复成一条新作业（截止日期设为今天）。
    /// </summary>
    public Homework Restore(HistoryEntry entry)
    {
        var homework = entry.ToHomework();
        Entries.Remove(entry);
        Save();
        return homework;
    }

    public void Clear()
    {
        Entries.Clear();
        Save();
    }

    public void Save()
    {
        try
        {
            AppStorage.Save(FileName, new HistoryStore { Entries = Entries.ToList() });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[History] 保存历史失败：{ex.Message}");
        }
    }
}
