using System.IO;
using System.Text.Json;

namespace StickyHomeworks.Models;

/// <summary>
/// 时间机器里的一个备份条目。
/// </summary>
public class BackupEntry
{
    /// <summary>备份文件完整路径。</summary>
    public string Path { get; init; } = "";

    /// <summary>备份时间。</summary>
    public DateTime Time { get; init; }

    /// <summary>该备份里的作业数量。</summary>
    public int HomeworkCount { get; init; }

    /// <summary>存档里的归档作业数量。</summary>
    public int ArchivedCount { get; init; }

    /// <summary>内容预览（前几条作业）。</summary>
    public string Preview { get; init; } = "";

    public string TimeText => Time.ToString("yyyy-MM-dd HH:mm");

    public string Summary => $"{HomeworkCount} 条作业" + (ArchivedCount > 0 ? $"，{ArchivedCount} 条归档" : "");

    /// <summary>
    /// 从备份文件读取概要信息。
    /// </summary>
    public static BackupEntry FromFile(string path)
    {
        var entry = new BackupEntry
        {
            Path = path,
            Time = File.GetLastWriteTime(path)
        };

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("Homeworks", out var homeworks) ||
                homeworks.ValueKind != JsonValueKind.Array)
            {
                return entry;
            }

            var texts = new List<string>();
            foreach (var homework in homeworks.EnumerateArray())
            {
                var subject = homework.TryGetProperty("Subject", out var s) ? s.GetString() ?? "" : "";
                var content = homework.TryGetProperty("Content", out var c) ? c.GetString() ?? "" : "";
                var text = StickyHomeworks.RichTextBoxHelper.GetPlainText(content);
                if (text.Length == 0)
                {
                    continue;
                }

                texts.Add(string.IsNullOrEmpty(subject) ? text : $"[{subject}] {text}");
            }

            var archived = 0;
            if (document.RootElement.TryGetProperty("ArchivedHomeworks", out var archivedElement) &&
                archivedElement.ValueKind == JsonValueKind.Array)
            {
                archived = archivedElement.GetArrayLength();
            }

            var preview = string.Join("；", texts.Take(3));
            if (texts.Count > 3)
            {
                preview += $" 等 {texts.Count} 条";
            }

            return new BackupEntry
            {
                Path = path,
                Time = File.GetLastWriteTime(path),
                HomeworkCount = texts.Count,
                ArchivedCount = archived,
                Preview = preview.Length > 80 ? preview[..80] + "…" : preview
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BackupEntry] 读取备份失败：{path}，{ex.Message}");
            return entry;
        }
    }
}
