namespace StickyHomeworks.Models;

/// <summary>
/// History.json 的存储结构。
/// </summary>
public class HistoryStore
{
    public List<HistoryEntry> Entries { get; set; } = new();
}
