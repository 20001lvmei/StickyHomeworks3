using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace StickyHomeworks.Services;

/// <summary>
/// 统一管理应用的数据目录与文件读写。
/// <para>
/// 数据文件固定保存在用户数据目录（<c>%LOCALAPPDATA%\StickyHomeworks3</c>）中，
/// 而不是相对于当前工作目录。这样可以避免因工作目录变化（例如开机自启动的快捷方式、
/// 从不同路径启动程序）导致读到另一份空数据，表现为“重启后作业消失”。
/// </para>
/// <para>写入采用“临时文件 + 替换”的方式，并保留一份 <c>.bak</c> 备份，防止意外断电导致数据文件损坏。</para>
/// </summary>
public static class AppStorage
{
    /// <summary>
    /// 用于覆盖数据目录的环境变量（便于测试或便携使用）。
    /// </summary>
    public const string DataDirectoryVariable = "STICKYHOMEWORKS3_DATA_DIR";

    /// <summary>
    /// 旧版本使用的环境变量，仍然兼容。
    /// </summary>
    public const string LegacyDataDirectoryVariable = "STICKYHOMEWORKS_DATA_DIR";

    private const string DataDirectoryName = "StickyHomeworks3";

    private static readonly string[] LegacyDataDirectoryNames = { "StickyHomeworks" };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly object WriteLock = new();

    /// <summary>
    /// 数据目录的绝对路径。启动时由 <see cref="EnsureDataDirectory"/> 选定第一个可写的候选目录。
    /// </summary>
    public static string DataDirectory { get; private set; } = ResolveDataDirectory();

    /// <summary>
    /// 获取数据目录下某个文件的完整路径。
    /// </summary>
    public static string GetPath(string fileName) => Path.Combine(DataDirectory, fileName);

    /// <summary>
    /// 确保数据目录存在（首次运行时会从旧版本的数据目录迁移数据）。
    /// 如果首选目录不可写（权限、只读配置、安全软件拦截等），会依次回退到其它可用目录，
    /// 保证应用总能启动，而不是直接崩溃。
    /// </summary>
    public static void EnsureDataDirectory()
    {
        if (_prepared)
        {
            return;
        }

        foreach (var candidate in CandidateDirectories())
        {
            try
            {
                if (!Directory.Exists(candidate))
                {
                    TryMigrateLegacyDirectory(candidate);
                }

                Directory.CreateDirectory(candidate);

                // 真正写一个文件确认可写（仅判断目录是否存在并不可靠）
                var probe = Path.Combine(candidate, ".write-test");
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);

                if (!string.Equals(candidate, DataDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"[AppStorage] 数据目录回退为：{candidate}");
                }

                DataDirectory = candidate;
                _prepared = true;
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                Debug.WriteLine($"[AppStorage] 数据目录不可用：{candidate}，{ex.Message}");
            }
        }

        Debug.WriteLine("[AppStorage] 没有可用的数据目录，数据将只保存在内存中。");
        _prepared = true;
    }

    private static bool _prepared;

    /// <summary>
    /// 候选数据目录，按优先级排列。
    /// </summary>
    private static IEnumerable<string> CandidateDirectories()
    {
        // 1. 环境变量显式指定
        foreach (var variable in new[] { DataDirectoryVariable, LegacyDataDirectoryVariable })
        {
            var overridden = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(overridden))
            {
                continue;
            }

            string path;
            try
            {
                path = Path.GetFullPath(overridden);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                Debug.WriteLine($"[AppStorage] 数据目录覆盖值无效：{overridden}");
                continue;
            }

            yield return path;
        }

        // 2. 常规位置
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            yield return Path.Combine(localAppData, DataDirectoryName);
        }

        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(roamingAppData))
        {
            yield return Path.Combine(roamingAppData, DataDirectoryName);
        }

        // 3. 兜底位置
        yield return Path.Combine(Path.GetTempPath(), DataDirectoryName);
        yield return Path.Combine(AppContext.BaseDirectory, "data");
    }

    /// <summary>
    /// 把旧版本遗留的数据文件（工作目录或程序目录下的同名文件）复制到数据目录。
    /// 仅在目标文件不存在时执行，且不会删除旧文件。
    /// </summary>
    public static void MigrateLegacyFile(string fileName)
    {
        var target = GetPath(fileName);
        if (File.Exists(target))
        {
            return;
        }

        foreach (var legacy in EnumerateLegacyPaths(fileName))
        {
            if (legacy == null || !File.Exists(legacy))
            {
                continue;
            }

            try
            {
                EnsureDataDirectory();
                File.Copy(legacy, target, false);
                Debug.WriteLine($"[AppStorage] 已迁移数据文件：{legacy} -> {target}");
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppStorage] 迁移数据文件失败：{legacy}，{ex.Message}");
            }
        }
    }

    /// <summary>
    /// 加载 JSON 数据。主文件不可用时自动回退到 <c>.bak</c> 备份；
    /// 内容损坏的文件会被重命名保留，方便排查（读取失败如文件被占用则不处理，避免误删数据）。
    /// </summary>
    /// <returns>反序列化结果；文件不存在或全部损坏时返回 <c>null</c>。</returns>
    public static T? Load<T>(string fileName) where T : class
    {
        var path = GetPath(fileName);
        var status = TryRead<T>(path, out var value, out var error);

        if (status == ReadStatus.Ok)
        {
            return value;
        }

        if (status == ReadStatus.Corrupt)
        {
            Debug.WriteLine($"[AppStorage] 数据文件损坏：{error}");
            Quarantine(path);
        }
        else if (status == ReadStatus.Unreadable)
        {
            // 文件可能存在但暂时读不到（被占用/权限问题）。此时不要动它，也不要报告为损坏。
            Debug.WriteLine($"[AppStorage] 无法读取数据文件：{error}");
        }

        if (TryRead<T>(path + ".bak", out var backup, out _) == ReadStatus.Ok)
        {
            Debug.WriteLine($"[AppStorage] 已从备份恢复数据文件：{path}.bak");
            return backup;
        }

        return null;
    }

    /// <summary>
    /// 原子地保存 JSON 数据。
    /// </summary>
    public static void Save<T>(string fileName, T value)
    {
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        var path = GetPath(fileName);

        lock (WriteLock)
        {
            EnsureDataDirectory();
            WriteAtomic(path, json);
        }
    }

    private enum ReadStatus
    {
        Missing,
        Ok,
        Corrupt,
        Unreadable
    }

    private static ReadStatus TryRead<T>(string path, out T? value, out string? error) where T : class
    {
        value = null;
        error = null;

        string text;
        try
        {
            if (!File.Exists(path))
            {
                return ReadStatus.Missing;
            }

            text = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error = ex.Message;
            return ReadStatus.Unreadable;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "文件内容为空";
            return ReadStatus.Corrupt;
        }

        try
        {
            value = JsonSerializer.Deserialize<T>(text, SerializerOptions);
            if (value == null)
            {
                error = "反序列化结果为空";
                return ReadStatus.Corrupt;
            }

            return ReadStatus.Ok;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return ReadStatus.Corrupt;
        }
    }

    private static void Quarantine(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var backup = $"{path}.corrupt-{DateTime.Now:yyyyMMddHHmmss}";
            File.Move(path, backup, true);
            Debug.WriteLine($"[AppStorage] 数据文件损坏，已保留为：{backup}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppStorage] 保留损坏文件失败：{ex.Message}");
        }
    }

    private static void WriteAtomic(string path, string content)
    {
        var bytes = new UTF8Encoding(false).GetBytes(content);
        var temp = path + ".tmp";

        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }

        if (File.Exists(path))
        {
            try
            {
                // 替换的同时把旧文件保存为 .bak
                File.Replace(temp, path, path + ".bak", true);
                return;
            }
            catch (Exception ex) when (ex is IOException or PlatformNotSupportedException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"[AppStorage] 原子替换失败，回退为直接覆盖：{ex.Message}");
            }
        }

        File.Move(temp, path, true);

        try
        {
            File.Copy(path, path + ".bak", true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppStorage] 写入备份失败：{ex.Message}");
        }
    }

    private static IEnumerable<string?> EnumerateLegacyPaths(string fileName)
    {
        yield return SafeCombine(Environment.CurrentDirectory, fileName);
        yield return SafeCombine(AppContext.BaseDirectory, fileName);
    }

    private static string? SafeCombine(string? directory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        try
        {
            return Path.Combine(directory, fileName);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string ResolveDataDirectory()
    {
        // 环境变量优先，其次用户数据目录，最后兜底到临时目录
        foreach (var candidate in CandidateDirectories())
        {
            return candidate;
        }

        return Path.Combine(Path.GetTempPath(), DataDirectoryName);
    }

    /// <summary>
    /// 把旧版本（改名/换数据目录之前）的数据目录整体迁移到目标目录。仅在目标目录不存在时执行。
    /// </summary>
    private static void TryMigrateLegacyDirectory(string target)
    {
        if (Directory.Exists(target))
        {
            return;
        }

        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
            {
                return;
            }

            foreach (var legacyName in LegacyDataDirectoryNames)
            {
                var legacy = Path.Combine(appData, legacyName);
                if (!Directory.Exists(legacy))
                {
                    continue;
                }

                Directory.CreateDirectory(target);
                foreach (var file in Directory.GetFiles(legacy, "*", SearchOption.TopDirectoryOnly))
                {
                    File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
                }

                Debug.WriteLine($"[AppStorage] 已迁移数据目录：{legacy} -> {target}");
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppStorage] 迁移数据目录失败：{ex.Message}");
        }
    }
}
